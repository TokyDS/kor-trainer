using KorTrainer.Application.Common;
using KorTrainer.Contracts;

namespace KorTrainer.Application.Tests;

/// <summary>In-memory audio library: every listed word "has" a tiny recording.</summary>
public sealed class FakeAudioLibrary(params string[] words) : IAudioLibrary
{
    public IReadOnlySet<string> Words { get; } = words.ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<AudioCreditDto> Credits =>
        Words.Select(w => new AudioCreditDto(w, "tester", "CC0", "https://creativecommons.org/publicdomain/zero/1.0/", "https://example.org")).ToList();

    public Task<AudioFile?> OpenAsync(string word, CancellationToken cancellationToken) =>
        Task.FromResult(Words.Contains(word) ? new AudioFile(new MemoryStream([1, 2, 3]), "audio/mpeg") : null);
}
