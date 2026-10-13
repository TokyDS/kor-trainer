using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Web.Services;

/// <summary>Error returned by the API as RFC 7807 problem details, with a user-facing message.</summary>
public sealed class ApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Typed client for the KorTrainer HTTP API.</summary>
public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<SettingsDto> GetSettingsAsync() => GetAsync<SettingsDto>("api/settings");

    public Task<SettingsDto> SaveSettingsAsync(SettingsDto settings) => SendAsync<SettingsDto>(HttpMethod.Put, "api/settings", settings);

    public Task<TodayDto> GetTodayAsync() => GetAsync<TodayDto>("api/today");

    public Task<PlanDto> GetPlanAsync() => GetAsync<PlanDto>("api/plan");

    public Task SetTaskDoneAsync(string taskKey, DateOnly date, bool done) =>
        SendAsync(HttpMethod.Put, $"api/today/tasks/{Uri.EscapeDataString(taskKey)}", new SetTaskDoneRequest(date, done));

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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
