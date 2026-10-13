using KorTrainer.Domain.Common;

namespace KorTrainer.Contracts;

public sealed record ChoiceOptionDto(string Text, string? Hint, string? Romanization);

public sealed record ChoiceExerciseDto(
    string Type,
    string ItemKey,
    string Prompt,
    string? PromptNote,
    string? PromptRomanization,
    IReadOnlyList<ChoiceOptionDto> Options,
    int CorrectIndex,
    string Explanation,
    string? SpeakText,
    bool AutoSpeak);

public sealed record ExerciseSetDto(StudyModule Module, string Type, string Title, IReadOnlyList<ChoiceExerciseDto> Items);

public sealed record AttemptDto(StudyModule Module, string ExerciseType, string ItemKey, bool IsCorrect, int ResponseMs);

public sealed record SessionDto(StudyModule Module, DateTime StartedUtc, int DurationSeconds);
