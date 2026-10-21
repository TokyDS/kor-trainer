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
    public async Task SynthesizedSpeech_IsServedForKnownTextsOnly()
    {
        var known = await _client.GetAsync($"/api/audio/tts?text={Uri.EscapeDataString(" 학교 ")}", Ct);
        var unknown = await _client.GetAsync($"/api/audio/tts?text={Uri.EscapeDataString("없는말")}", Ct);
        var empty = await _client.GetAsync("/api/audio/tts", Ct);

        known.StatusCode.Should().Be(HttpStatusCode.OK);
        known.Content.Headers.ContentType!.MediaType.Should().Be("audio/ogg");
        (await known.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(0x4F, 0x67, 0x67, 0x53);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Today_ReturnsTheFirstStepWithEnumsAsStrings()
    {
        var raw = await _client.GetStringAsync("/api/today", Ct);
        var today = JsonSerializer.Deserialize<TodayDto>(raw, Json)!;

        raw.Should().Contain("\"stage\":\"Hangul\"").And.Contain("\"state\":\"Current\"");
        today.Step.Key.Should().Be("lesson-1");
        today.Step.Tasks.Should().Contain(t => t.Route == "/hangul/letter-to-sound?level=1");
    }

    [Fact]
    public async Task Advance_MovesToTheNextStep_AndRejectsAStaleRequest()
    {
        var first = await _client.PostAsJsonAsync("/api/plan/advance", new AdvanceRequest("lesson-1"), Json, Ct);
        var again = await _client.PostAsJsonAsync("/api/plan/advance", new AdvanceRequest("lesson-1"), Json, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<TodayDto>(Json, Ct))!.Step.Key.Should().Be("lesson-2");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Today_UnknownStep_Is404()
    {
        var response = await _client.GetAsync("/api/today?step=day-3", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Attempts_AreReflectedInTodayProgress()
    {
        var attempts = new[] { new AttemptDto(StudyModule.Hangul, ExerciseTypes.LetterToSound, "letter:ㄱ", true, 1200) };

        var post = await _client.PostAsJsonAsync("/api/attempts", attempts, Json, Ct);
        var today = await _client.GetFromJsonAsync<TodayDto>("/api/today", Json, Ct);

        post.StatusCode.Should().Be(HttpStatusCode.OK);
        today!.Step.Tasks.Single(t => t.Key == ExerciseTypes.LetterToSound).Progress.Should().Be(1);
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
    public async Task GrammarExercises_ReturnCopulaDrill()
    {
        var set = await _client.GetFromJsonAsync<ExerciseSetDto>("/api/grammar/exercises/copula?count=5", Json, Ct);
        var topics = await _client.GetFromJsonAsync<GrammarTopicDto[]>("/api/grammar/topics", Json, Ct);

        set!.Module.Should().Be(StudyModule.Grammar);
        set.Items.Should().HaveCount(5).And.OnlyContain(i => i.Options.Count == 2);
        topics.Should().Contain(t => t.Type == ExerciseTypes.Copula);
    }

    [Fact]
    public async Task NumberExercises_ReturnMixedDrillAndRejectUnknownType()
    {
        var set = await _client.GetFromJsonAsync<ExerciseSetDto>("/api/numbers/exercises/mixed?count=12", Json, Ct);
        var unknown = await _client.GetAsync("/api/numbers/exercises/roman", Ct);

        set!.Module.Should().Be(StudyModule.Numbers);
        set.Items.Should().HaveCount(12);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopikDraftsAndStats_RoundTrip()
    {
        var guide = await _client.GetFromJsonAsync<WritingGuideDto>("/api/topik/writing", Json, Ct);
        var save = await _client.PostAsJsonAsync("/api/topik/drafts", new SaveDraftRequest(54, _factory.Time.GetUtcNow().UtcDateTime, 600, "최근 관심이 높아지고 있다."), Json, Ct);
        var drafts = await _client.GetFromJsonAsync<WritingDraftDto[]>("/api/topik/drafts?task=54", Json, Ct);
        var overview = await _client.GetFromJsonAsync<StatsOverviewDto>("/api/stats/overview", Json, Ct);
        var daily = await _client.GetFromJsonAsync<DailyStatDto[]>("/api/stats/daily?days=14", Json, Ct);

        guide!.Tasks.Should().HaveCount(4);
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        drafts.Should().ContainSingle();
        overview!.TotalStudyMinutes.Should().Be(10);
        overview.WritingDrafts.Should().Be(1);
        daily!.Should().HaveCount(14);
        daily[^1].StudyMinutes.Should().Be(10);
    }

    [Fact]
    public async Task Audio_ListsWordsServesRecordingsAndCredits()
    {
        var words = await _client.GetFromJsonAsync<string[]>("/api/audio/words", Json, Ct);
        var credits = await _client.GetFromJsonAsync<AudioCreditDto[]>("/api/audio/credits", Json, Ct);
        var recording = await _client.GetAsync($"/api/audio/{Uri.EscapeDataString("바다")}", Ct);
        var missing = await _client.GetAsync($"/api/audio/{Uri.EscapeDataString("없는단어")}", Ct);

        words.Should().Contain("바다").And.HaveCountGreaterThan(900);
        credits.Should().HaveCount(words!.Length);
        recording.StatusCode.Should().Be(HttpStatusCode.OK);
        recording.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
        recording.Headers.CacheControl!.MaxAge.Should().BeGreaterThan(TimeSpan.FromDays(1));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task VocabularyFlow_ReviewsANewCardInLessonFour()
    {
        await MoveToLessonAsync(4);

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
        await MoveToLessonAsync(4);
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

    private async Task MoveToLessonAsync(int lesson)
    {
        for (int day = 1; day < lesson; day++)
        {
            var response = await _client.PostAsJsonAsync("/api/plan/advance", new AdvanceRequest($"lesson-{day}"), Json, Ct);
            response.EnsureSuccessStatusCode();
        }
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
