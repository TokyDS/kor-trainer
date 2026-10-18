using System.Net.Http.Json;
using System.Text.Json;
using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;
using KorTrainer.UI.Services;

namespace KorTrainer.Web.Services;

/// <summary>Typed client for the KorTrainer HTTP API; errors arrive as RFC 7807 problem details.</summary>
public sealed class HttpKorTrainerApi(HttpClient http) : IKorTrainerApi
{
    private static JsonSerializerOptions JsonOptions => KorTrainerJson.Options;

    public Task<SettingsDto> GetSettingsAsync() => GetAsync<SettingsDto>("api/settings");

    public Task<SettingsDto> SaveSettingsAsync(SettingsDto settings) => SendAsync<SettingsDto>(HttpMethod.Put, "api/settings", settings);

    public Task<TodayDto> GetTodayAsync(string? step = null) =>
        GetAsync<TodayDto>(step is null ? "api/today" : $"api/today?step={Uri.EscapeDataString(step)}");

    public Task<TodayDto> AdvanceAsync(string from) => SendAsync<TodayDto>(HttpMethod.Post, "api/plan/advance", new AdvanceRequest(from));

    public Task<ReminderDto> GetReminderAsync() => GetAsync<ReminderDto>("api/reminder");

    public Task<PlanDto> GetPlanAsync() => GetAsync<PlanDto>("api/plan");

    public Task SetTaskDoneAsync(string taskKey, string step, bool done) =>
        SendAsync(HttpMethod.Put, $"api/today/tasks/{Uri.EscapeDataString(taskKey)}", new SetTaskDoneRequest(step, done));

    public Task<ExerciseSetDto> GetHangulExercisesAsync(string type, int level, int count) =>
        GetAsync<ExerciseSetDto>($"api/hangul/exercises/{Uri.EscapeDataString(type)}?level={level}&count={count}");

    public Task<IReadOnlyList<ReadingItemDto>> GetReadingAsync(int level, int count) =>
        GetAsync<IReadOnlyList<ReadingItemDto>>($"api/hangul/reading?level={level}&count={count}");

    public Task RecordReadingTrialAsync(ReadingTrialDto trial) => SendAsync(HttpMethod.Post, "api/hangul/reading-trials", trial);

    public Task<ReadingStatsDto> GetReadingStatsAsync() => GetAsync<ReadingStatsDto>("api/hangul/reading-stats");

    public Task<HangulAnalysisDto> AnalyzeAsync(string text) =>
        GetAsync<HangulAnalysisDto>($"api/hangul/analyze?text={Uri.EscapeDataString(text)}");

    public Task<HangulGuideDto> GetGuideAsync(int level) => GetAsync<HangulGuideDto>($"api/hangul/guide/{level}");

    public Task<IReadOnlyList<GrammarTopicDto>> GetGrammarTopicsAsync() => GetAsync<IReadOnlyList<GrammarTopicDto>>("api/grammar/topics");

    public Task<ExerciseSetDto> GetGrammarExercisesAsync(string type, int count) =>
        GetAsync<ExerciseSetDto>($"api/grammar/exercises/{Uri.EscapeDataString(type)}?count={count}");

    public Task<ExerciseSetDto> GetListeningExercisesAsync(string type, int count) =>
        GetAsync<ExerciseSetDto>($"api/listening/exercises/{Uri.EscapeDataString(type)}?count={count}");

    public Task<ExerciseSetDto> GetNumberExercisesAsync(string type, int count) =>
        GetAsync<ExerciseSetDto>($"api/numbers/exercises/{Uri.EscapeDataString(type)}?count={count}");

    public Task<IReadOnlyList<string>> GetRecordedWordsAsync() => GetAsync<IReadOnlyList<string>>("api/audio/words");

    public Task<IReadOnlyList<AudioCreditDto>> GetAudioCreditsAsync() => GetAsync<IReadOnlyList<AudioCreditDto>>("api/audio/credits");

    public Task<WritingGuideDto> GetWritingGuideAsync() => GetAsync<WritingGuideDto>("api/topik/writing");

    public Task<IReadOnlyList<WritingDraftDto>> GetDraftsAsync(int? task = null) =>
        GetAsync<IReadOnlyList<WritingDraftDto>>(task is { } t ? $"api/topik/drafts?task={t}" : "api/topik/drafts");

    public Task<WritingDraftDto> SaveDraftAsync(SaveDraftRequest request) => SendAsync<WritingDraftDto>(HttpMethod.Post, "api/topik/drafts", request);

    public Task<ForecastDto> GetForecastAsync() => GetAsync<ForecastDto>("api/stats/forecast");

    public Task<StatsOverviewDto> GetStatsOverviewAsync() => GetAsync<StatsOverviewDto>("api/stats/overview");

    public Task<IReadOnlyList<DailyStatDto>> GetDailyStatsAsync(int days) => GetAsync<IReadOnlyList<DailyStatDto>>($"api/stats/daily?days={days}");

    public Task<VocabularyQueueDto> GetQueueAsync(int limit = 50) => GetAsync<VocabularyQueueDto>($"api/vocab/queue?limit={limit}");

    public Task<ReviewResultDto> ReviewAsync(ReviewRequest request) => SendAsync<ReviewResultDto>(HttpMethod.Post, "api/vocab/reviews", request);

    public Task<VocabularySummaryDto> GetVocabularySummaryAsync() => GetAsync<VocabularySummaryDto>("api/vocab/summary");

    public Task<IReadOnlyList<TopicDto>> GetTopicsAsync() => GetAsync<IReadOnlyList<TopicDto>>("api/vocab/topics");

    public Task<WordPageDto> GetWordsAsync(string? topic, PlanStage? stage, WordStatus? status, string? search, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(topic))
        {
            query.Add($"topic={Uri.EscapeDataString(topic)}");
        }

        if (stage is { } s)
        {
            query.Add($"stage={s}");
        }

        if (status is { } st)
        {
            query.Add($"status={st}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        return GetAsync<WordPageDto>($"api/vocab/words?{string.Join('&', query)}");
    }

    public Task RecordAttemptsAsync(IReadOnlyList<AttemptDto> attempts) => SendAsync(HttpMethod.Post, "api/attempts", attempts);

    public Task RecordSessionAsync(SessionDto session) => SendAsync(HttpMethod.Post, "api/sessions", session);

    public Task<IReadOnlyList<HanjaRootDto>> GetHanjaRootsAsync() => GetAsync<IReadOnlyList<HanjaRootDto>>("api/hanja/roots");

    public Task<HanjaRootDetailDto> GetHanjaRootAsync(string character) => GetAsync<HanjaRootDetailDto>($"api/hanja/roots/{Uri.EscapeDataString(character)}");

    public Task<ExerciseSetDto> GetHanjaExercisesAsync(string type, int count) =>
        GetAsync<ExerciseSetDto>($"api/hanja/exercises/{Uri.EscapeDataString(type)}?count={count}");

    public Task<WordDetailsDto> GetWordDetailsAsync(int wordId) => GetAsync<WordDetailsDto>($"api/vocab/words/{wordId}/details");

    public Task SaveWordNoteAsync(int wordId, string? text) => SendAsync(HttpMethod.Put, $"api/vocab/words/{wordId}/note", new SaveNoteRequest(text));

    public Task<IReadOnlyList<HardWordDto>> GetHardWordsAsync() => GetAsync<IReadOnlyList<HardWordDto>>("api/vocab/hard");

    public Task<ExerciseSetDto> GetHardExercisesAsync(string type, int count) =>
        GetAsync<ExerciseSetDto>($"api/vocab/hard/exercises/{Uri.EscapeDataString(type)}?count={count}");

    public async Task<ImportResultDto> ImportProgressAsync(Stream json, CancellationToken cancellationToken)
    {
        using var content = new StreamContent(json);
        content.Headers.ContentType = new("application/json");
        using var response = await http.PostAsync("api/progress/import", content, cancellationToken);
        return await ReadAsync<ImportResultDto>(response);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await http.GetAsync(url);
        return await ReadAsync<T>(response);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: JsonOptions) };
        using var response = await http.SendAsync(request);
        return await ReadAsync<T>(response);
    }

    private async Task SendAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: JsonOptions) };
        using var response = await http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
            ?? throw new ApiException("Сервер вернул пустой ответ.", (int)response.StatusCode);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = $"Ошибка сервера ({(int)response.StatusCode}).";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);
            if (problem is not null)
            {
                var details = problem.Errors?.SelectMany(e => e.Value) ?? [];
                message = string.Join(" ", new[] { problem.Title, problem.Detail }.Concat(details).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        catch (JsonException)
        {
            // Not a problem+json body; keep the generic message.
        }

        throw new ApiException(message, (int)response.StatusCode);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
