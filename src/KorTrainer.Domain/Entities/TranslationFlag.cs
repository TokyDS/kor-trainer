namespace KorTrainer.Domain.Entities;

public static class TranslationFlagLimits
{
    public const int MaxNoteLength = 300;

    public const int MaxTranslationLength = 256;
}

/// <summary>
/// A translation the learner doubts, kept for a later fix of the deck. The translation is remembered as it was,
/// so a flag whose word has been corrected since can be told apart.
/// </summary>
public sealed class TranslationFlag
{
    public int WordId { get; set; }

    public Word? Word { get; set; }

    public required string Russian { get; set; }

    /// <summary>What the learner thinks it should be; optional.</summary>
    public string? Note { get; set; }

    public DateTime CreatedUtc { get; set; }
}
