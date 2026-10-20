using KorTrainer.Contracts;

namespace KorTrainer.Application.Common;

/// <summary>An audio file ready to stream; the caller disposes the content.</summary>
public sealed record AudioFile(Stream Content, string ContentType);

/// <summary>Native-speaker recordings of deck words (Lingua Libre), downloaded on demand and cached.</summary>
public interface IAudioLibrary
{
    /// <summary>Words that have a recording.</summary>
    IReadOnlySet<string> Words { get; }

    /// <summary>Author and license of every recording, for attribution.</summary>
    IReadOnlyList<AudioCreditDto> Credits { get; }

    /// <summary>Returns the recording of the word, or null if there is none or it cannot be fetched right now.</summary>
    Task<AudioFile?> OpenAsync(string word, CancellationToken cancellationToken);
}
