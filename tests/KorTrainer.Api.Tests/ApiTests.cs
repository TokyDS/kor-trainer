using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Api.Tests;

public sealed class ApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = CreateJson();
    private readonly KorTrainerApiFactory _factory = new();
    private HttpClient _client = default!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Today_ReturnsDayOneWithEnumsAsStrings()
    {
        var raw = await _client.GetStringAsync("/api/today", Ct);
        var today = JsonSerializer.Deserialize<TodayDto>(raw, Json)!;

        raw.Should().Contain("\"stage\":\"Hangul\"");
        today.DayNumber.Should().Be(1);
        today.Tasks.Should().Contain(t => t.Route == "/hangul/letter-to-sound?level=1");
    }

    [Fact]
    public async Task Attempts_AreReflectedInTodayProgress()
    {
        var attempts = new[] { new AttemptDto(StudyModule.Hangul, ExerciseTypes.LetterToSound, "letter:ㄱ", true, 1200) };

        var post = await _client.PostAsJsonAsync("/api/attempts", attempts, Json, Ct);
        var today = await _client.GetFromJsonAsync<TodayDto>("/api/today", Json, Ct);

        post.StatusCode.Should().Be(HttpStatusCode.OK);
        today!.Tasks.Single(t => t.Key == ExerciseTypes.LetterToSound).Progress.Should().Be(1);
        today.StudiedToday.Should().BeTrue();
    }

    [Fact]
    public async Task HangulExercises_ReturnRequestedSet()
    {
        var set = await _client.GetFromJsonAsync<ExerciseSetDto>("/api/hangul/exercises/decompose?level=3&count=7", Json, Ct);

        set!.Module.Should().Be(StudyModule.Hangul);
        set.Items.Should().HaveCount(7);
    }

    [Fact]
    public async Task InvalidInput_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/api/hangul/exercises/unknown?level=9", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.Should().Contain("\"errors\"").And.Contain("level");
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequestWithoutInternals()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/vocab/reviews", content, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain("   at ");
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404ProblemInsteadOfSpaShell()
    {
        var response = await _client.GetAsync("/api/does-not-exist", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task VocabularyFlow_ReviewsANewCardOnDayFour()
    {
        await MoveToPlanDayAsync(4);

        var queue = await _client.GetFromJsonAsync<VocabularyQueueDto>("/api/vocab/queue", Json, Ct);
        var card = queue!.Cards[0];
        var review = await _client.PostAsJsonAsync("/api/vocab/reviews", new ReviewRequest(card.WordId, Grade.Good, 3000), Json, Ct);
        var result = await review.Content.ReadFromJsonAsync<ReviewResultDto>(Json, Ct);
        var summary = await _client.GetFromJsonAsync<VocabularySummaryDto>("/api/vocab/summary", Json, Ct);

        card.Korean.Should().Be("안녕하세요");
        result!.Phase.Should().Be(CardPhase.Learning);
        summary!.NewToday.Should().Be(1);
    }

    [Fact]
    public async Task Settings_RejectInvalidUpdate()
    {
        var settings = await _client.GetFromJsonAsync<SettingsDto>("/api/settings", Json, Ct);

        var response = await _client.PutAsJsonAsync("/api/settings", settings! with { MaxReviewsPerDay = 1 }, Json, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProgressExportAndImport_RoundTripThroughHttp()
    {
        await MoveToPlanDayAsync(4);
        var card = (await _client.GetFromJsonAsync<VocabularyQueueDto>("/api/vocab/queue", Json, Ct))!.Cards[0];
        await _client.PostAsJsonAsync("/api/vocab/reviews", new ReviewRequest(card.WordId, Grade.Easy, 1000), Json, Ct);

        var export = await _client.GetAsync("/api/progress/export", Ct);
        var file = await export.Content.ReadAsStringAsync(Ct);
        using var content = new StringContent(file, System.Text.Encoding.UTF8, "application/json");
        var import = await _client.PostAsync("/api/progress/import", content, Ct);
        var result = await import.Content.ReadFromJsonAsync<ImportResultDto>(Json, Ct);

        export.Content.Headers.ContentDisposition!.FileName.Should().StartWith("kortrainer-progress-");
        import.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Words.Should().Be(1);
    }

    [Fact]
    public async Task Plan_ExposesStagesTowardsTopik()
    {
        var plan = await _client.GetFromJsonAsync<PlanDto>("/api/plan", Json, Ct);

        plan!.Stages.Select(s => s.Stage).Should().Contain([PlanStage.Level1, PlanStage.MockExam, PlanStage.TopikPrep]);
    }

    private async Task MoveToPlanDayAsync(int day)
    {
        var settings = await _client.GetFromJsonAsync<SettingsDto>("/api/settings", Json, Ct);
        var response = await _client.PutAsJsonAsync("/api/settings", settings! with { StartDate = new DateOnly(2026, 10, 13).AddDays(1 - day) }, Json, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
