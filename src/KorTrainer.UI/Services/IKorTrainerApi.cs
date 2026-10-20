using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;

namespace KorTrainer.UI.Services;

/// <summary>Error with a user-facing (Russian) message; <see cref="StatusCode"/> follows HTTP semantics.</summary>
public sealed class ApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// Everything the pages need from the trainer's back end. The web client talks HTTP to the server;
/// the Android app runs the same application services in-process.
/// </summary>
/// <remarks>Implementations report failures as <see cref="ApiException"/> (or <see cref="HttpRequestException"/> when offline).</remarks>
public interface IKorTrainerApi
{
    Task<SettingsDto> GetSettingsAsync();

    Task<SettingsDto> SaveSettingsAsync(SettingsDto settings);

    /// <summary>The current plan step, or another step by key when browsing.</summary>
    Task<TodayDto> GetTodayAsync(string? step = null);

    /// <summary>Finishes step <paramref name="from"/> and opens the next one.</summary>
    Task<TodayDto> AdvanceAsync(string from);

    Task<ReminderDto> GetReminderAsync();

    Task<PlanDto> GetPlanAsync();

    Task SetTaskDoneAsync(string taskKey, string step, bool done);

    Task<ExerciseSetDto> GetHangulExercisesAsync(string type, int level, int count);

    Task<IReadOnlyList<ReadingItemDto>> GetReadingAsync(int level, int count);

    Task RecordReadingTrialAsync(ReadingTrialDto trial);

    Task<ReadingStatsDto> GetReadingStatsAsync();

    Task<HangulAnalysisDto> AnalyzeAsync(string text);

    Task<HangulGuideDto> GetGuideAsync(int level);

    Task<IReadOnlyList<GrammarTopicDto>> GetGrammarTopicsAsync();

    Task<ExerciseSetDto> GetGrammarExercisesAsync(string type, int count);

    Task<ExerciseSetDto> GetListeningExercisesAsync(string type, int count);

    Task<ExerciseSetDto> GetNumberExercisesAsync(string type, int count);

    Task<IReadOnlyList<string>> GetRecordedWordsAsync();

    Task<IReadOnlyList<AudioCreditDto>> GetAudioCreditsAsync();

    Task<WritingGuideDto> GetWritingGuideAsync();

    Task<IReadOnlyList<WritingDraftDto>> GetDraftsAsync(int? task = null);

    Task<WritingDraftDto> SaveDraftAsync(SaveDraftRequest request);

    Task<ForecastDto> GetForecastAsync();

    Task<StatsOverviewDto> GetStatsOverviewAsync();

    Task<IReadOnlyList<DailyStatDto>> GetDailyStatsAsync(int days);

    Task<VocabularyQueueDto> GetQueueAsync(int limit = 50);

    Task<ReviewResultDto> ReviewAsync(ReviewRequest request);

    Task<VocabularySummaryDto> GetVocabularySummaryAsync();

    Task<IReadOnlyList<TopicDto>> GetTopicsAsync();

    Task<WordPageDto> GetWordsAsync(string? topic, PlanStage? stage, WordStatus? status, string? search, int page, int pageSize);

    Task RecordAttemptsAsync(IReadOnlyList<AttemptDto> attempts);

    Task RecordSessionAsync(SessionDto session);

    Task<IReadOnlyList<HanjaRootDto>> GetHanjaRootsAsync();

    Task<HanjaRootDetailDto> GetHanjaRootAsync(string character);

    Task<ExerciseSetDto> GetHanjaExercisesAsync(string type, int count);

    Task<WordDetailsDto> GetWordDetailsAsync(int wordId);

    Task SaveWordNoteAsync(int wordId, string? text);

    Task<IReadOnlyList<HardWordDto>> GetHardWordsAsync();

    Task<ExerciseSetDto> GetHardExercisesAsync(string type, int count);

    Task<SentenceSetDto> GetSentencePracticeAsync(string type, int count);

    Task<TypingSetDto> GetTypingSetAsync(int level, int count);

    Task<IReadOnlyList<TextSummaryDto>> GetTextsAsync();

    Task<ReadingTextDto> GetTextAsync(string id);

    Task<bool> AddWordToStudyAsync(int wordId);

    Task<ExerciseSetDto> GetTopikReadingAsync(string format, int count);

    Task<ImportResultDto> ImportProgressAsync(Stream json, CancellationToken cancellationToken);
}
