using KorTrainer.Domain.Common;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Contracts;

/// <summary>
/// Portable snapshot of everything the learner produced. Words are referenced by deck key,
/// not database id, so a backup can be restored into a freshly seeded database.
/// </summary>
public sealed record ProgressExport(
    int SchemaVersion,
    DateTime ExportedUtc,
    SettingsDto Settings,
    IReadOnlyList<WordProgressExport> Words,
    IReadOnlyList<ReviewLogExport> Reviews,
    IReadOnlyList<AttemptExport> Attempts,
    IReadOnlyList<ReadingTrialExport> ReadingTrials,
    IReadOnlyList<SessionExport> Sessions,
    IReadOnlyList<WritingDraftExport> WritingDrafts,
    IReadOnlyList<PlanStepExport>? PlanSteps = null,
    IReadOnlyList<StepTaskCompletionExport>? StepTaskCompletions = null,
    IReadOnlyList<TaskCompletionExport>? TaskCompletions = null)
{
    /// <summary>2: self-paced plan steps instead of per-day task ticks.</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Calendar-plan backups (per-day <see cref="TaskCompletions"/>) still import.</summary>
    public const int LegacySchemaVersion = 1;
}

public sealed record WordProgressExport(
    string DeckKey,
    CardPhase Phase,
    double Ease,
    double IntervalDays,
    int Repetitions,
    int Lapses,
    int Step,
    DateTime DueUtc,
    DateTime IntroducedUtc,
    DateTime LastReviewedUtc);

public sealed record ReviewLogExport(
    string DeckKey,
    DateTime ReviewedUtc,
    DateOnly LocalDate,
    Grade Grade,
    CardPhase PhaseBefore,
    double IntervalBefore,
    double IntervalAfter,
    int DurationMs);

public sealed record AttemptExport(
    StudyModule Module,
    string ExerciseType,
    string ItemKey,
    bool IsCorrect,
    int ResponseMs,
    DateTime AnsweredUtc,
    DateOnly LocalDate);

public sealed record ReadingTrialExport(string Text, int SyllableCount, int ElapsedMs, bool ReadCorrectly, DateTime CompletedUtc, DateOnly LocalDate);

public sealed record SessionExport(StudyModule Module, DateTime StartedUtc, int DurationSeconds, DateOnly LocalDate);

/// <summary>A manual tick of the old calendar plan (schema version 1).</summary>
public sealed record TaskCompletionExport(DateOnly Date, string TaskKey, DateTime CompletedUtc);

public sealed record PlanStepExport(string StepKey, DateTime StartedUtc, DateTime? CompletedUtc);

public sealed record StepTaskCompletionExport(string StepKey, string TaskKey, DateTime CompletedUtc);

public sealed record WritingDraftExport(int TaskNumber, DateTime StartedUtc, int DurationSeconds, string Text, DateOnly LocalDate);

public sealed record ImportResultDto(int Words, int SkippedWords, int Reviews, int Attempts, int ReadingTrials, int Sessions, int PlanSteps, int WritingDrafts);
