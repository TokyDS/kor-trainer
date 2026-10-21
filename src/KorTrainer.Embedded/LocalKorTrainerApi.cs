using KorTrainer.Application.Mistakes;
using KorTrainer.Application.Reading;
using KorTrainer.Application.Typing;
using KorTrainer.Application.Sentences;
using KorTrainer.Application.Hanja;
using System.Text.Json;
using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Listening;
using KorTrainer.Application.Numbers;
using KorTrainer.Application.Progress;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Stats;
using KorTrainer.Application.Today;
using KorTrainer.Application.Topik;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;
using KorTrainer.Infrastructure.Persistence;
using KorTrainer.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KorTrainer.Embedded;

/// <summary>
/// The pages' back end without a server: each call runs the application service in its own DI scope
/// (a fresh <c>DbContext</c>, like one HTTP request) and reports failures with the server's messages.
/// </summary>
public sealed class LocalKorTrainerApi(IServiceScopeFactory scopes, ILogger<LocalKorTrainerApi> logger) : IKorTrainerApi
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public Task<SettingsDto> GetSettingsAsync() => Run<SettingsService, SettingsDto>((s, ct) => s.GetAsync(ct));

    public Task<SettingsDto> SaveSettingsAsync(SettingsDto settings) => Run<SettingsService, SettingsDto>((s, ct) => s.UpdateAsync(settings, ct));

    public Task<TodayDto> GetTodayAsync(string? step = null) => Run<TodayService, TodayDto>((s, ct) => s.GetStepAsync(step, ct));

    public Task<TodayDto> AdvanceAsync(string from) => Run<TodayService, TodayDto>((s, ct) => s.AdvanceAsync(new AdvanceRequest(from), ct));

    public Task<ReminderDto> GetReminderAsync() => Run<ReminderService, ReminderDto>((s, ct) => s.GetAsync(ct));

    public Task<PlanDto> GetPlanAsync() => Run<TodayService, PlanDto>((s, ct) => s.GetPlanAsync(ct));

    public Task SetTaskDoneAsync(string taskKey, string step, bool done) =>
        Run<TodayService>((s, ct) => s.SetTaskDoneAsync(taskKey, new SetTaskDoneRequest(step, done), ct));

    public Task<ExerciseSetDto> GetHangulExercisesAsync(string type, int level, int count) =>
        Run<HangulService, ExerciseSetDto>((s, ct) => s.GetExercisesAsync(type, level, count, ct));

    public Task<IReadOnlyList<ReadingItemDto>> GetReadingAsync(int level, int count) =>
        Run<HangulService, IReadOnlyList<ReadingItemDto>>((s, ct) => s.GetReadingAsync(level, count, ct));

    public Task RecordReadingTrialAsync(ReadingTrialDto trial) => Run<ActivityService>((s, ct) => s.RecordReadingTrialAsync(trial, ct));

    public Task<ReadingStatsDto> GetReadingStatsAsync() => Run<HangulService, ReadingStatsDto>((s, ct) => s.GetReadingStatsAsync(ct));

    public Task<HangulAnalysisDto> AnalyzeAsync(string text) => Compute(() => HangulService.Analyze(text));

    public Task<HangulGuideDto> GetGuideAsync(int level) => Compute(() => HangulService.GetGuide(level));

    public Task<IReadOnlyList<GrammarTopicDto>> GetGrammarTopicsAsync() =>
        Run<GrammarService, IReadOnlyList<GrammarTopicDto>>((s, ct) => s.GetTopicsAsync(ct));

    public Task<ExerciseSetDto> GetGrammarExercisesAsync(string type, int count) =>
        Run<GrammarService, ExerciseSetDto>((s, ct) => s.GetExercisesAsync(type, count, ct));

    public Task<ExerciseSetDto> GetListeningExercisesAsync(string type, int count) =>
        Run<ListeningService, ExerciseSetDto>((s, ct) => s.GetExercisesAsync(type, count, ct));

    public Task<ExerciseSetDto> GetNumberExercisesAsync(string type, int count) =>
        Run<NumbersService, ExerciseSetDto>((s, _) => Task.FromResult(s.GetExercises(type, count)));

    public Task<IReadOnlyList<string>> GetRecordedWordsAsync() =>
        Run<IAudioLibrary, IReadOnlyList<string>>((audio, _) => Task.FromResult<IReadOnlyList<string>>([.. audio.Words.Order(StringComparer.Ordinal)]));

    public Task<IReadOnlyList<AudioCreditDto>> GetAudioCreditsAsync() =>
        Run<IAudioLibrary, IReadOnlyList<AudioCreditDto>>((audio, _) => Task.FromResult(audio.Credits));

    public Task<WritingGuideDto> GetWritingGuideAsync() => Compute(TopikService.GetGuide);

    public Task<IReadOnlyList<WritingDraftDto>> GetDraftsAsync(int? task = null) =>
        Run<TopikService, IReadOnlyList<WritingDraftDto>>((s, ct) => s.ListDraftsAsync(task, ct));

    public Task<WritingDraftDto> SaveDraftAsync(SaveDraftRequest request) => Run<TopikService, WritingDraftDto>((s, ct) => s.SaveDraftAsync(request, ct));

    public Task<ForecastDto> GetForecastAsync() => Run<StatsService, ForecastDto>((s, ct) => s.GetForecastAsync(ct));

    public Task<StatsOverviewDto> GetStatsOverviewAsync() => Run<StatsService, StatsOverviewDto>((s, ct) => s.GetOverviewAsync(ct));

    public Task<IReadOnlyList<DailyStatDto>> GetDailyStatsAsync(int days) =>
        Run<StatsService, IReadOnlyList<DailyStatDto>>((s, ct) => s.GetDailyAsync(days, ct));

    public Task<VocabularyQueueDto> GetQueueAsync(int limit = 50) => Run<VocabularyService, VocabularyQueueDto>((s, ct) => s.GetQueueAsync(limit, ct));

    public Task<ReviewResultDto> ReviewAsync(ReviewRequest request) => Run<VocabularyService, ReviewResultDto>((s, ct) => s.ReviewAsync(request, ct));

    public Task<VocabularySummaryDto> GetVocabularySummaryAsync() => Run<VocabularyService, VocabularySummaryDto>((s, ct) => s.GetSummaryAsync(ct));

    public Task<IReadOnlyList<TopicDto>> GetTopicsAsync() => Run<VocabularyService, IReadOnlyList<TopicDto>>((s, ct) => s.GetTopicsAsync(ct));

    public Task<WordPageDto> GetWordsAsync(string? topic, PlanStage? stage, WordStatus? status, string? search, int page, int pageSize) =>
        Run<VocabularyService, WordPageDto>((s, ct) => s.ListWordsAsync(topic, stage, status, search, page, pageSize, ct));

    public Task RecordAttemptsAsync(IReadOnlyList<AttemptDto> attempts) => Run<ActivityService>((s, ct) => s.RecordAttemptsAsync(attempts, ct));

    public Task RecordSessionAsync(SessionDto session) => Run<ActivityService>((s, ct) => s.RecordSessionAsync(session, ct));

    public Task<IReadOnlyList<HanjaRootDto>> GetHanjaRootsAsync() => Run<HanjaService, IReadOnlyList<HanjaRootDto>>((s, ct) => s.GetRootsAsync(ct));

    public Task<HanjaRootDetailDto> GetHanjaRootAsync(string character) => Run<HanjaService, HanjaRootDetailDto>((s, ct) => s.GetRootAsync(character, ct));

    public Task<ExerciseSetDto> GetHanjaExercisesAsync(string type, int count) =>
        Run<HanjaService, ExerciseSetDto>((s, ct) => s.GetExercisesAsync(type, count, ct));

    public Task<WordDetailsDto> GetWordDetailsAsync(int wordId) => Run<WordDetailsService, WordDetailsDto>((s, ct) => s.GetAsync(wordId, ct));

    public Task SaveWordNoteAsync(int wordId, string? text) => Run<WordDetailsService>((s, ct) => s.SaveNoteAsync(wordId, new SaveNoteRequest(text), ct));

    public Task<IReadOnlyList<HardWordDto>> GetHardWordsAsync() => Run<WordDetailsService, IReadOnlyList<HardWordDto>>((s, ct) => s.GetHardWordsAsync(ct));

    public Task<ExerciseSetDto> GetHardExercisesAsync(string type, int count) =>
        Run<WordDetailsService, ExerciseSetDto>((s, ct) => s.GetHardExercisesAsync(type, count, ct));

    public Task<SentenceSetDto> GetSentencePracticeAsync(string type, int count) =>
        Run<SentenceService, SentenceSetDto>((s, ct) => s.GetPracticeAsync(type, count, ct));

    public Task<TypingSetDto> GetTypingSetAsync(int level, int count) => Run<TypingService, TypingSetDto>((s, ct) => s.GetSetAsync(level, count, ct));

    public Task<IReadOnlyList<TextSummaryDto>> GetTextsAsync() => Run<ReadingService, IReadOnlyList<TextSummaryDto>>((s, ct) => s.GetTextsAsync(ct));

    public Task<ReadingTextDto> GetTextAsync(string id) => Run<ReadingService, ReadingTextDto>((s, ct) => s.GetTextAsync(id, ct));

    public Task<bool> AddWordToStudyAsync(int wordId) => Run<VocabularyService, bool>((s, ct) => s.AddToStudyAsync(wordId, ct));

    public Task<ExerciseSetDto> GetTopikReadingAsync(string format, int count) =>
        Run<TopikReadingService, ExerciseSetDto>((s, ct) => s.GetExercisesAsync(format, count, ct));

    public Task<MistakeSummaryDto> GetMistakeSummaryAsync() => Run<MistakeService, MistakeSummaryDto>((s, ct) => s.GetSummaryAsync(ct));

    public Task<ExerciseSetDto> GetMistakesAsync(int count) => Run<MistakeService, ExerciseSetDto>((s, ct) => s.GetDueAsync(count, ct));

    public async Task<ImportResultDto> ImportProgressAsync(Stream json, CancellationToken cancellationToken)
    {
        ProgressExport snapshot;
        try
        {
            snapshot = await JsonSerializer.DeserializeAsync<ProgressExport>(json, KorTrainerJson.Options, cancellationToken)
                ?? throw new JsonException("Empty backup.");
        }
        catch (JsonException)
        {
            throw new ApiException("Файл не похож на резервную копию KorTrainer.", 400);
        }

        return await Run<ProgressTransferService, ImportResultDto>((s, ct) => s.ImportAsync(snapshot, ct), cancellationToken);
    }

    /// <summary>The whole progress as indented JSON, for the share sheet.</summary>
    public Task<byte[]> ExportProgressAsync() =>
        Run<ProgressTransferService, byte[]>(async (s, ct) => JsonSerializer.SerializeToUtf8Bytes(await s.ExportAsync(ct), KorTrainerJson.Indented));

    /// <summary>Creates or migrates the database and loads the decks; runs once, before the first call.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private Task Run<TService>(Func<TService, CancellationToken, Task> action)
        where TService : notnull =>
        Run<TService, bool>(async (service, ct) =>
        {
            await action(service, ct);
            return true;
        });

    private async Task<TResult> Run<TService, TResult>(Func<TService, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
        where TService : notnull
    {
        try
        {
            // SQLite's async methods complete synchronously: run the work off the UI thread so the
            // pages keep rendering (the first start creates the database and loads the decks).
            return await Task.Run(
                async () =>
                {
                    await InitializeAsync(cancellationToken);
                    await using var scope = scopes.CreateAsyncScope();
                    return await action(scope.ServiceProvider.GetRequiredService<TService>(), cancellationToken);
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ApiException))
        {
            throw Translate(ex);
        }
    }

    private Task<TResult> Compute<TResult>(Func<TResult> compute)
    {
        try
        {
            return Task.FromResult(compute());
        }
        catch (Exception ex)
        {
            return Task.FromException<TResult>(Translate(ex));
        }
    }

    /// <summary>Same wording as the server's problem details, so pages show identical messages.</summary>
    internal ApiException Translate(Exception exception)
    {
        switch (exception)
        {
            case ValidationFailedException validation:
                return new ApiException(Join("Проверьте введённые данные.", validation.Errors.SelectMany(e => e.Value)), 400);
            case NotFoundException notFound:
                return new ApiException(Join("Не найдено.", [notFound.Message]), 404);
            case ConflictException conflict:
                return new ApiException(Join("Конфликт.", [conflict.Message]), 409);
            default:
                logger.LogError(exception, "Unhandled exception in the embedded back end");
                return new ApiException("Внутренняя ошибка приложения.", 500);
        }
    }

    private static string Join(string title, IEnumerable<string> details) =>
        string.Join(" ", details.Prepend(title).Where(s => !string.IsNullOrWhiteSpace(s)));
}
