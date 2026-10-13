using KorTrainer.Domain.Common;

namespace KorTrainer.Contracts;

public sealed record TemplatePhraseDto(string Part, string Korean, string Russian);

public sealed record WritingTaskDto(
    int Number,
    string Title,
    string Format,
    int RecommendedMinutes,
    int? MinChars,
    int? MaxChars,
    IReadOnlyList<string> Tips,
    IReadOnlyList<TemplatePhraseDto> Template);

public sealed record WritingGuideDto(string PastPapersUrl, int SectionMinutes, IReadOnlyList<WritingTaskDto> Tasks);

public sealed record SaveDraftRequest(int TaskNumber, DateTime StartedUtc, int DurationSeconds, string Text);

public sealed record WritingDraftDto(long Id, int TaskNumber, DateTime StartedUtc, int DurationSeconds, int CharCount, string Text);

public sealed record ModuleAccuracyDto(StudyModule Module, int Answers, int Correct, int AnswersLast7Days, int CorrectLast7Days);

/// <param name="StudyMinutes">Minutes from exercise sessions and TOPIK writing.</param>
public sealed record DailyStatDto(DateOnly Date, int StudyMinutes, int Answers, int Correct, int Reviews, int NewWords);

public sealed record StatsOverviewDto(
    VocabularySummaryDto Vocabulary,
    IReadOnlyList<ModuleAccuracyDto> Accuracy,
    int TotalStudyMinutes,
    int ActiveDays,
    int CurrentStreak,
    int LongestStreak,
    double? ReadingMsPerSyllable,
    int WritingDrafts);
