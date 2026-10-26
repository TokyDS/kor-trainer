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

/// <param name="Type">The grammar or number topic.</param>
/// <param name="Box">Checks passed in a row; 0 until the first one.</param>
/// <param name="IsDue">The check is due today or was due earlier.</param>
/// <param name="Module">Grammar or Numbers: where the check's exercises come from.</param>
public sealed record RuleReviewTopicDto(string Type, string Title, int Box, DateOnly Due, bool IsDue, StudyModule Module = StudyModule.Grammar);

/// <param name="Due">Rules to check today.</param>
/// <param name="Topics">Every practised rule, the due ones first.</param>
public sealed record RuleReviewSummaryDto(int Due, IReadOnlyList<RuleReviewTopicDto> Topics);

/// <param name="Correct">Answers right at the first try, without a hint.</param>
public sealed record RuleCheckRequest(int Correct, int Total);

public sealed record SessionDto(StudyModule Module, DateTime StartedUtc, int DurationSeconds);
