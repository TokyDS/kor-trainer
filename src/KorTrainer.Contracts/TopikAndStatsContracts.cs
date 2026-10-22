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

/// <param name="Route">Where to drill the topic, e.g. <c>/grammar/past</c>.</param>
public sealed record WeakTopicDto(StudyModule Module, string Type, string Title, int Attempts, int AccuracyPercent, string Route);

public sealed record StatsOverviewDto(
    VocabularySummaryDto Vocabulary,
    IReadOnlyList<ModuleAccuracyDto> Accuracy,
    int TotalStudyMinutes,
    int ActiveDays,
    int ActiveDaysThisWeek,
    int ActiveDaysThisMonth,
    double? ReadingMsPerSyllable,
    int WritingDrafts);

/// <param name="ExpectedOn">When the goal is reached at the recent pace; null when reached or without a pace.</param>
public sealed record GoalForecastDto(string Title, int Goal, bool Reached, DateOnly? ExpectedOn);

/// <param name="WordsStudied">Words introduced so far (any SRS state).</param>
/// <param name="PacePerDay">Average new words per day recently.</param>
/// <param name="WordsByExam">Words in the deck by <paramref name="ExamDate"/> at this pace.</param>
public sealed record ForecastDto(
    int WordsStudied,
    double PacePerDay,
    int NewWordsPerDaySetting,
    IReadOnlyList<GoalForecastDto> Goals,
    DateOnly ExamDate,
    int WordsByExam);
