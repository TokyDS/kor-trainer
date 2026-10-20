using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KorTrainer.Infrastructure.Audio;

/// <summary>
/// Downloads every recording once in the background, one per second, so audio also works offline.
/// Gives up quietly after repeated failures (e.g. no internet) and tries again on the next start.
/// </summary>
public sealed class AudioPrefetchService(
    LinguaLibreAudioLibrary library,
    IOptions<AudioOptions> options,
    ILogger<AudioPrefetchService> logger) : BackgroundService
{
    private const int MaxConsecutiveFailures = 5;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Prefetch)
        {
            return;
        }

        var missing = library.Words.Where(w => !library.IsCached(w)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        await Task.Delay(StartupDelay, stoppingToken);
        logger.LogInformation("Prefetching {Count} native-speaker recordings", missing.Count);
        int downloaded = 0;
        int failures = 0;
        foreach (string word in missing)
        {
            if (await library.DownloadAsync(word, stoppingToken))
            {
                downloaded++;
                failures = 0;
            }
            else if (++failures >= MaxConsecutiveFailures)
            {
                logger.LogWarning("Stopped prefetching recordings after {Failures} failures in a row; will retry on next start", failures);
                break;
            }

            // Back off after failures (e.g. HTTP 429 from Wikimedia): 1 s, then 30 s, 60 s, …
            var delay = failures == 0 ? options.Value.PrefetchDelay : FailureBackoff * failures;
            await Task.Delay(delay, stoppingToken);
        }

        logger.LogInformation("Prefetched {Downloaded} of {Count} recordings", downloaded, missing.Count);
    }
}
