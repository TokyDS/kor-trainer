using KorTrainer.Domain.Common;

namespace KorTrainer.Contracts;

/// <param name="Why">For a wrong option: why it is wrong for this word, shown when the learner picks it.</param>
public sealed record ChoiceOptionDto(string Text, string? Hint, string? Romanization, string? Why = null);

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
    bool AutoSpeak,
    StudyModule? Module = null);

public sealed record ExerciseSetDto(StudyModule Module, string Type, string Title, IReadOnlyList<ChoiceExerciseDto> Items);

/// <param name="IsOpen">The plan has already introduced the topic.</param>
public sealed record GrammarTopicDto(string Type, string Title, string Description, KorTrainer.Domain.Plan.PlanStage Stage, bool IsOpen);

/// <param name="Exercise">The exercise itself, sent with a wrong first answer so it can come back later.</param>
public sealed record AttemptDto(StudyModule Module, string ExerciseType, string ItemKey, bool IsCorrect, int ResponseMs, ChoiceExerciseDto? Exercise = null);

/// <param name="Due">Mistakes to review today.</param>
/// <param name="Open">Mistakes not yet closed, due or not.</param>
/// <param name="Resolved">Mistakes closed after three correct reviews.</param>
public sealed record MistakeSummaryDto(int Due, int Open, int Resolved);

public sealed record SessionDto(StudyModule Module, DateTime StartedUtc, int DurationSeconds);
