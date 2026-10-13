using System.Text.Json;
using KorTrainer.Application.Activity;
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
    public static IEndpointRouteBuilder MapKorTrainerApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

        MapSettings(api.MapGroup("/settings").WithTags("Settings"));
        MapToday(api.MapGroup(string.Empty).WithTags("Today"));
        MapHangul(api.MapGroup("/hangul").WithTags("Hangul"));
        MapVocabulary(api.MapGroup("/vocab").WithTags("Vocabulary"));
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
        group.MapGet("/today", (TodayService service, CancellationToken ct) => service.GetTodayAsync(ct));
        group.MapGet("/plan", (TodayService service, CancellationToken ct) => service.GetPlanAsync(ct));
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
