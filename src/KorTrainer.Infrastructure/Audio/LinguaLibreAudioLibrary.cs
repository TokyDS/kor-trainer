using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KorTrainer.Infrastructure.Audio;

/// <summary>
/// Serves Lingua Libre recordings listed in the embedded manifest. A file is downloaded from Wikimedia
/// the first time it is needed and then served from the local cache. Only manifest URLs are ever
/// fetched — callers pass a word, never a URL.
/// </summary>
public sealed class LinguaLibreAudioLibrary : IAudioLibrary
{
    public const string HttpClientName = "lingualibre";

    private const string ManifestResource = "KorTrainer.Infrastructure.Seed.audio-lingualibre.json";
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private const string ContentType = "audio/mpeg";

    private readonly Dictionary<string, Recording> _recordings;
    private readonly IHttpClientFactory _httpClients;
    private readonly ILogger<LinguaLibreAudioLibrary> _logger;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _downloads = new(2);

    public LinguaLibreAudioLibrary(IHttpClientFactory httpClients, IOptions<AudioOptions> options, ILogger<LinguaLibreAudioLibrary> logger)
    {
        _httpClients = httpClients;
        _logger = logger;
        _cacheDirectory = Path.GetFullPath(options.Value.CacheDirectory);
        _recordings = LoadManifest().ToDictionary(r => r.Word, StringComparer.Ordinal);
        Words = _recordings.Keys.ToHashSet(StringComparer.Ordinal);
        Credits = _recordings.Values
            .OrderBy(r => r.Word, StringComparer.Ordinal)
            .Select(r => new AudioCreditDto(r.Word, r.Speaker, r.License, r.LicenseUrl, r.Page))
            .ToList();
    }

    public IReadOnlySet<string> Words { get; }

    public IReadOnlyList<AudioCreditDto> Credits { get; }

    public bool IsCached(string word) => File.Exists(CachePath(word));

    public async Task<AudioFile?> OpenAsync(string word, CancellationToken cancellationToken)
    {
        if (!_recordings.TryGetValue(word, out var recording))
        {
            return null;
        }

        string path = CachePath(word);
        if (!File.Exists(path) && !await DownloadAsync(recording, path, cancellationToken))
        {
            return null;
        }

        return new AudioFile(File.OpenRead(path), ContentType);
    }

    /// <summary>Downloads a recording into the cache; returns false when it cannot be fetched now.</summary>
    public async Task<bool> DownloadAsync(string word, CancellationToken cancellationToken) =>
        _recordings.TryGetValue(word, out var recording) && await DownloadAsync(recording, CachePath(word), cancellationToken);

    private async Task<bool> DownloadAsync(Recording recording, string path, CancellationToken cancellationToken)
    {
        await _downloads.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                return true;
            }

            using var response = await _httpClients.CreateClient(HttpClientName).GetAsync(recording.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!response.IsSuccessStatusCode || !mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Recording of {Word} unavailable: HTTP {Status}, {MediaType}", recording.Word, (int)response.StatusCode, mediaType);
                return false;
            }

            Directory.CreateDirectory(_cacheDirectory);
            string temp = path + ".part";
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(temp))
            {
                if (!await CopyLimitedAsync(source, target, cancellationToken))
                {
                    _logger.LogWarning("Recording of {Word} is larger than {Limit} bytes; skipped", recording.Word, MaxFileBytes);
                    target.Close();
                    File.Delete(temp);
                    return false;
                }
            }

            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not download the recording of {Word}", recording.Word);
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Download of the recording of {Word} timed out", recording.Word);
            return false;
        }
        finally
        {
            _downloads.Release();
        }
    }

    private static async Task<bool> CopyLimitedAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxFileBytes)
            {
                return false;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    // Hashed names: no Korean or user-controlled text in file paths.
    private string CachePath(string word) =>
        Path.Combine(_cacheDirectory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(word)))[..32].ToLowerInvariant() + ".mp3");

    private static IReadOnlyList<Recording> LoadManifest()
    {
        using var stream = typeof(LinguaLibreAudioLibrary).Assembly.GetManifestResourceStream(ManifestResource)
            ?? throw new InvalidOperationException($"Embedded audio manifest '{ManifestResource}' was not found.");
        var manifest = JsonSerializer.Deserialize<Manifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The audio manifest is empty.");
        return manifest.Recordings;
    }

    private sealed record Manifest(IReadOnlyList<Recording> Recordings);

    private sealed record Recording(string Word, string Speaker, string License, string LicenseUrl, string Page, string Url);
}
