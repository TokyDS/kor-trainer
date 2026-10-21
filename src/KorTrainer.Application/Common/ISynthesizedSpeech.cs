namespace KorTrainer.Application.Common;

/// <summary>
/// Pre-generated neural speech (MeloTTS) for deck words, example sentences and reading texts —
/// clearer than the device's speech synthesis and the same on every device.
/// </summary>
public interface ISynthesizedSpeech
{
    /// <summary>The speed the audio was generated at (1 = normal); players scale their rate by it.</summary>
    double GeneratedSpeed { get; }

    bool Has(string text);

    /// <summary>The audio of the text, or null when it was not generated.</summary>
    Task<AudioFile?> OpenAsync(string text, CancellationToken cancellationToken);
}
