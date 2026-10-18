using KorTrainer.Application.Sentences;
using KorTrainer.Application.Hanja;
using System.Text.Json;
using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
using KorTrainer.Application.Listening;
using KorTrainer.Application.Numbers;
using KorTrainer.Application.Stats;
using KorTrainer.Application.Topik;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Progress;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Today;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace KorTrainer.Api.Endpoints;

public static class ApiEndpoints
{
    public const string RateLimitPolicy = "api";

    public static IEndpointRouteBuilder MapKorTrainerApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting(RateLimitPolicy);

        api.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

        MapSettings(api.MapGroup("/settings").WithTags("Settings"));
        MapToday(api.MapGroup(string.Empty).WithTags("Today"));
        MapHangul(api.MapGroup("/hangul").WithTags("Hangul"));
        MapVocabulary(api.MapGroup("/vocab").WithTags("Vocabulary"));
        MapGrammar(api.MapGroup("/grammar").WithTags("Grammar"));
        api.MapGroup("/numbers").WithTags("Numbers")
            .MapGet("/exercises/{type}", (string type, int? count, NumbersService service) => service.GetExercises(type, count ?? 20));
        MapAudio(api.MapGroup("/audio").WithTags("Audio"));
        api.MapGroup("/listening").WithTags("Listening")
            .MapGet("/exercises/{type}", (string type, int? count, ListeningService service, CancellationToken ct) => service.GetExercisesAsync(type, count ?? 20, ct));
        var hanja = api.MapGroup("/hanja").WithTags("Hanja");
        hanja.MapGet("/roots", (HanjaService service, CancellationToken ct) => service.GetRootsAsync(ct));
        hanja.MapGet("/roots/{character}", (string character, HanjaService service, CancellationToken ct) => service.GetRootAsync(character, ct));
        hanja.MapGet("/exercises/{type}", (string type, int? count, HanjaService service, CancellationToken ct) => service.GetExercisesAsync(type, count ?? 20, ct));
        var words = api.MapGroup("/vocab").WithTags("Vocabulary");
        words.MapGet("/words/{id:int}/details", (int id, WordDetailsService service, CancellationToken ct) => service.GetAsync(id, ct));
        words.MapPut("/words/{id:int}/note", async (int id, SaveNoteRequest request, WordDetailsService service, CancellationToken ct) =>
        {
            await service.SaveNoteAsync(id, request, ct);
            return Results.NoContent();
        });
        words.MapGet("/hard", (WordDetailsService service, CancellationToken ct) => service.GetHardWordsAsync(ct));
        words.MapGet("/hard/exercises/{type}", (string type, int? count, WordDetailsService service, CancellationToken ct) => service.GetHardExercisesAsync(type, count ?? 20, ct));
        api.MapGroup("/sentences").WithTags("Sentences")
            .MapGet("/{type}", (string type, int? count, SentenceService service, CancellationToken ct) => service.GetPracticeAsync(type, count ?? 10, ct));
        MapTopik(api.MapGroup("/topik").WithTags("TOPIK"));
        MapStats(api.MapGroup("/stats").WithTags("Statistics"));
        MapActivity(api.MapGroup(string.Empty).WithTags("Activity"));
        MapProgress(api.MapGroup("/progress").WithTags("Progress"));

        // Unknown API routes must not fall through to the SPA's index.html.
        api.MapFallback(() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Неизвестный адрес API."));
        return app;
    }

    private static void MapSettings(RouteGroupBuilder group)
    {
        group.MapGet("/", (SettingsService service, CancellationToken ct) => service.GetAsync(ct));
        group.MapPut("/", (SettingsDto request, SettingsService service, CancellationToken ct) => service.UpdateAsync(request, ct));
    }

    private static void MapToday(RouteGroupBuilder group)
    {
        group.MapGet("/today", (string? step, TodayService service, CancellationToken ct) => service.GetStepAsync(step, ct));
        group.MapGet("/plan", (TodayService service, CancellationToken ct) => service.GetPlanAsync(ct));
        group.MapPost("/plan/advance", (AdvanceRequest request, TodayService service, CancellationToken ct) => service.AdvanceAsync(request, ct));
        group.MapGet("/reminder", (ReminderService service, CancellationToken ct) => service.GetAsync(ct));
        group.MapPut("/today/tasks/{taskKey}", async (string taskKey, SetTaskDoneRequest request, TodayService service, CancellationToken ct) =>
        {
            await service.SetTaskDoneAsync(taskKey, request, ct);
            return Results.NoContent();
        });
    }

    private static void MapHangul(RouteGroupBuilder group)
    {
        group.MapGet("/exercises/{type}", (string type, int? level, int? count, HangulService service, CancellationToken ct) =>
            service.GetExercisesAsync(type, level ?? 1, count ?? 20, ct));
        group.MapGet("/reading", (int? level, int? count, HangulService service, CancellationToken ct) =>
            service.GetReadingAsync(level ?? 3, count ?? 10, ct));
        group.MapPost("/reading-trials", async (ReadingTrialDto trial, ActivityService service, CancellationToken ct) =>
        {
            await service.RecordReadingTrialAsync(trial, ct);
            return Results.NoContent();
        });
        group.MapGet("/reading-stats", (HangulService service, CancellationToken ct) => service.GetReadingStatsAsync(ct));
        group.MapGet("/analyze", (string? text) => HangulService.Analyze(text));
        group.MapGet("/guide/{level:int}", (int level) => HangulService.GetGuide(level));
    }

    private static void MapVocabulary(RouteGroupBuilder group)
    {
        group.MapGet("/queue", (int? limit, VocabularyService service, CancellationToken ct) =>
            service.GetQueueAsync(limit ?? VocabularyService.DefaultQueueSize, ct));
        group.MapPost("/reviews", (ReviewRequest request, VocabularyService service, CancellationToken ct) => service.ReviewAsync(request, ct));
        group.MapGet("/summary", (VocabularyService service, CancellationToken ct) => service.GetSummaryAsync(ct));
        group.MapGet("/topics", (VocabularyService service, CancellationToken ct) => service.GetTopicsAsync(ct));
        group.MapGet("/words", (string? topic, PlanStage? stage, WordStatus? status, string? search, int? page, int? pageSize, VocabularyService service, CancellationToken ct) =>
            service.ListWordsAsync(topic, stage, status, search, page ?? 1, pageSize ?? 50, ct));
    }

    private static void MapGrammar(RouteGroupBuilder group)
    {
        group.MapGet("/topics", (GrammarService service, CancellationToken ct) => service.GetTopicsAsync(ct));
        group.MapGet("/exercises/{type}", (string type, int? count, GrammarService service, CancellationToken ct) =>
            service.GetExercisesAsync(type, count ?? 20, ct));
    }

    private static void MapAudio(RouteGroupBuilder group)
    {
        group.MapGet("/words", (IAudioLibrary audio) => audio.Words.Order(StringComparer.Ordinal));
        group.MapGet("/credits", (IAudioLibrary audio) => audio.Credits);
        group.MapGet("/{word}", async (string word, IAudioLibrary audio, HttpContext http, CancellationToken ct) =>
        {
            if (!audio.Words.Contains(word))
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Для этого слова нет записи.");
            }

            var file = await audio.OpenAsync(word, ct);
            if (file is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Запись пока недоступна (нет связи с Wikimedia).");
            }

            // Recordings never change for a word: let the browser keep them.
            http.Response.Headers.CacheControl = "public, max-age=2592000";
            return Results.Stream(file.Content, file.ContentType, enableRangeProcessing: true);
        });
    }

    private static void MapTopik(RouteGroupBuilder group)
    {
        group.MapGet("/writing", () => TopikService.GetGuide());
        group.MapGet("/drafts", (int? task, TopikService service, CancellationToken ct) => service.ListDraftsAsync(task, ct));
        group.MapPost("/drafts", (SaveDraftRequest request, TopikService service, CancellationToken ct) => service.SaveDraftAsync(request, ct));
    }

    private static void MapStats(RouteGroupBuilder group)
    {
        group.MapGet("/overview", (StatsService service, CancellationToken ct) => service.GetOverviewAsync(ct));
        group.MapGet("/daily", (int? days, StatsService service, CancellationToken ct) => service.GetDailyAsync(days ?? 30, ct));
        group.MapGet("/forecast", (StatsService service, CancellationToken ct) => service.GetForecastAsync(ct));
    }

    private static void MapActivity(RouteGroupBuilder group)
    {
        group.MapPost("/attempts", async (AttemptDto[] attempts, ActivityService service, CancellationToken ct) =>
            Results.Ok(new { recorded = await service.RecordAttemptsAsync(attempts, ct) }));
        group.MapPost("/sessions", async (SessionDto session, ActivityService service, CancellationToken ct) =>
        {
            await service.RecordSessionAsync(session, ct);
            return Results.NoContent();
        });
    }

    private static void MapProgress(RouteGroupBuilder group)
    {
        group.MapGet("/export", async (ProgressTransferService service, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var snapshot = await service.ExportAsync(ct);
            var options = new JsonSerializerOptions(json.Value.SerializerOptions) { WriteIndented = true };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, options);
            return Results.File(bytes, "application/json", $"kortrainer-progress-{snapshot.ExportedUtc:yyyy-MM-dd}.json");
        });
        group.MapPost("/import", (ProgressExport snapshot, ProgressTransferService service, CancellationToken ct) => service.ImportAsync(snapshot, ct));
    }
}
