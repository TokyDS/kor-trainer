namespace KorTrainer.Infrastructure.Audio;

public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>Where downloaded recordings are kept (a volume in Docker, so they survive restarts).</summary>
    public string CacheDirectory { get; set; } = Path.Combine("data", "audio");

    /// <summary>Download all recordings in the background after start, so they also work offline.</summary>
    public bool Prefetch { get; set; } = true;

    /// <summary>Pause between background downloads (Wikimedia asks clients to be gentle).</summary>
    public TimeSpan PrefetchDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Wikimedia's User-Agent policy asks for a client name and a contact URL or e-mail; requests
    /// without one get HTTP 429 ("bot traffic"). Put your own contact here via <c>Audio__UserAgent</c>.
    /// </summary>
    public string UserAgent { get; set; } = "KorTrainer/1.0 (+https://lingualibre.org; self-hosted Korean learning app) dotnet-HttpClient/10";
}
