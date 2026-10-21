using KorTrainer.Infrastructure.Audio;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace KorTrainer.Api.Tests;

/// <summary>Runs the real API against a throwaway SQLite file and a fake clock (day 1 of the plan, 09:00 Moscow).</summary>
public sealed class KorTrainerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kortrainer-test-{Guid.NewGuid():N}.db");
    private readonly string _audioDirectory = Path.Combine(Path.GetTempPath(), $"kortrainer-test-audio-{Guid.NewGuid():N}");

    /// <summary>Holds one generated file: 학교 → "abc123.opus".</summary>
    public string SynthesizedDirectory { get; } = CreateSynthesizedSpeech();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 13, 6, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:KorTrainer", $"Data Source={_databasePath};Pooling=False");
        builder.UseSetting("Audio:CacheDirectory", _audioDirectory);
        builder.UseSetting("Audio:Prefetch", "false");
        builder.UseSetting("Audio:SynthesizedDirectory", SynthesizedDirectory);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            // Never reach Wikimedia from tests: recordings come from a fake handler.
            services.AddHttpClient(LinguaLibreAudioLibrary.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new FakeAudioHandler());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_databasePath);
        if (Directory.Exists(_audioDirectory))
        {
            Directory.Delete(_audioDirectory, recursive: true);
        }

        Directory.Delete(SynthesizedDirectory, recursive: true);
    }

    private sealed class FakeAudioHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent([0x49, 0x44, 0x33, 4, 0, 0]);
            content.Headers.ContentType = new("audio/mpeg");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
        }
    }

    private static string CreateSynthesizedSpeech()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"kortrainer-test-tts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), """{"speed": 0.9, "items": {"학교": "abc123"}}""");
        File.WriteAllBytes(Path.Combine(directory, "abc123.opus"), [0x4F, 0x67, 0x67, 0x53]);
        return directory;
    }
}
