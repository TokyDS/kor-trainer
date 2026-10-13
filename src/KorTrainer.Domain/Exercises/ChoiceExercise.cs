namespace KorTrainer.Domain.Exercises;

/// <param name="Hint">Always visible helper text (e.g. "с придыханием").</param>
/// <param name="Romanization">Shown only when the learner enables romanization.</param>
public sealed record ChoiceOption(string Text, string? Hint = null, string? Romanization = null);

/// <summary>
/// A multiple-choice question. <see cref="SpeakText"/> is voiced by the browser:
/// automatically when <see cref="AutoSpeak"/> is set (listening drills), otherwise on demand.
/// </summary>
public sealed record ChoiceExercise(
    string Type,
    string ItemKey,
    string Prompt,
    string? PromptNote,
    IReadOnlyList<ChoiceOption> Options,
    int CorrectIndex,
    string Explanation,
    string? SpeakText = null,
    bool AutoSpeak = false,
    string? PromptRomanization = null)
{
    public ChoiceOption Correct => Options[CorrectIndex];
}

/// <summary>A word or pseudo-word for timed reading.</summary>
public sealed record ReadingItem(string Text, int SyllableCount, string Pronounced, string? Translation);
