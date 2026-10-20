using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Contracts;

public enum WordStatus
{
    Locked = 0,
    New = 1,
    Learning = 2,
    Young = 3,
    Mature = 4,
}

public sealed record GradePreviewDto(Grade Grade, string Label, string Interval);

public sealed record CardDto(
    int WordId,
    string Korean,
    string Russian,
    string Topic,
    PartOfSpeech PartOfSpeech,
    CardPhase Phase,
    string Pronounced,
    DateTime DueUtc,
    IReadOnlyList<GradePreviewDto> Previews);

/// <param name="NextLearningDueUtc">When the next learning card becomes due, if nothing is due now.</param>
public sealed record VocabularyQueueDto(
    IReadOnlyList<CardDto> Cards,
    int DueCount,
    int NewRemainingToday,
    DateTime? NextLearningDueUtc);

public sealed record ReviewRequest(int WordId, Grade Grade, int DurationMs);

public sealed record ReviewResultDto(int WordId, CardPhase Phase, DateTime DueUtc, double IntervalDays);

public sealed record WordDto(
    int Id,
    string Korean,
    string Russian,
    string Topic,
    PartOfSpeech PartOfSpeech,
    PlanStage Stage,
    int? LessonDay,
    WordStatus Status,
    DateTime? DueUtc);

public sealed record WordPageDto(IReadOnlyList<WordDto> Items, int Total, int Page, int PageSize);

public sealed record TopicDto(string Topic, int Total, int Learned);

/// <param name="Unseen">Unlocked words that have not been introduced yet.</param>
public sealed record VocabularySummaryDto(
    int Total,
    int Unlocked,
    int Unseen,
    int Learning,
    int Young,
    int Mature,
    int DueNow,
    int NewToday,
    int ReviewsToday);

/// <param name="FamilySize">Deck words that share this character.</param>
public sealed record HanjaPartDto(string Syllable, string Character, string? Hun, string? Russian, int FamilySize);

public sealed record ExampleDto(int Id, string Korean, string Russian, bool MachineTranslated, string Author);

/// <summary>Everything that helps to remember a word: roots, examples, the learner's note, why it is hard.</summary>
public sealed record WordDetailsDto(
    int WordId,
    string Korean,
    string Russian,
    string? Hanja,
    IReadOnlyList<HanjaPartDto> Roots,
    IReadOnlyList<ExampleDto> Examples,
    string? Note,
    string? HardReason);

public sealed record SaveNoteRequest(string? Text);

public sealed record HardWordDto(int WordId, string Korean, string Russian, string Reason, int Lapses, string? Note, DateTime DueUtc);
