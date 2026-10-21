using KorTrainer.Application.Mistakes;
using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Hanja;
using KorTrainer.Infrastructure.Content;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Progress;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Stats;
using KorTrainer.Application.Topik;
using KorTrainer.Application.Today;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Srs;
using KorTrainer.Infrastructure.Persistence;
using KorTrainer.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KorTrainer.Application.Tests;

/// <summary>
/// A migrated in-memory SQLite database seeded with the bundled deck, plus the application
/// services wired to a controllable clock. One instance per test.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>09:00 in Moscow on the default start date (day 1 of the plan).</summary>
    public static readonly DateTimeOffset DayOneMorning = new(2026, 10, 13, 6, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    private TestDatabase(SqliteConnection connection, AppDbContext db, FakeTimeProvider time)
    {
        _connection = connection;
        Db = db;
        Time = time;
        Clock = new StudyClock(time);
        Settings = new SettingsService(db);
        Evaluator = new StepEvaluator(db);
        Cursor = new PlanCursor(db, Settings, Clock, Evaluator);
        Vocabulary = new VocabularyService(db, Settings, Clock, new SrsScheduler(), Cursor);
        Activity = new ActivityService(db, Settings, Clock);
        Today = new TodayService(db, Cursor, Evaluator, Vocabulary, Clock);
        Reminders = new ReminderService(Today, Settings);
        Hangul = new HangulService(db, Settings, Clock, new Random(42), Audio, Cursor);
        Grammar = new GrammarService(db, Cursor, Sentences, new GrammarClozeCache(), new Random(42));
        Hanja = new HanjaService(db, Glossary, Cursor, new Random(42));
        WordDetails = new WordDetailsService(db, Glossary, Sentences, Clock, new Random(42));
        Topik = new TopikService(db, Settings, Clock);
        Stats = new StatsService(db, Settings, Vocabulary, Clock);
        Progress = new ProgressTransferService(db, Settings, Clock);
        Mistakes = new MistakeService(db, Settings, Clock, new Random(42));
    }

    /// <summary>Recorded words used by the listening drill; replace before calling Hangul to test recordings.</summary>
    public FakeAudioLibrary Audio { get; } = new();

    public AppDbContext Db { get; }

    public FakeTimeProvider Time { get; }

    public StudyClock Clock { get; }

    public SettingsService Settings { get; }

    public VocabularyService Vocabulary { get; }

    public ActivityService Activity { get; }

    public StepEvaluator Evaluator { get; }

    public PlanCursor Cursor { get; }

    public TodayService Today { get; }

    public ReminderService Reminders { get; }

    public HangulService Hangul { get; }

    public GrammarService Grammar { get; }

    /// <summary>The bundled glosses and sentences, as in the app.</summary>
    public static EmbeddedHanjaGlossary Glossary { get; } = new();

    public static EmbeddedSentenceLibrary Sentences { get; } = new();

    public HanjaService Hanja { get; }

    public WordDetailsService WordDetails { get; }

    public TopikService Topik { get; }

    public StatsService Stats { get; }

    public ProgressTransferService Progress { get; }

    public MistakeService Mistakes { get; }

    public static async Task<TestDatabase> CreateAsync(DateTimeOffset? now = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        await new DatabaseInitializer(db, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        return new TestDatabase(connection, db, new FakeTimeProvider(now ?? DayOneMorning));
    }

    /// <summary>Makes the given step current, as if the learner had moved there just now.</summary>
    public async Task SetPlanStepAsync(string stepKey)
    {
        await Db.PlanSteps.ExecuteDeleteAsync();
        Db.PlanSteps.Add(new PlanStepProgress { StepKey = stepKey, StartedUtc = Clock.UtcNow });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Marks deck words as studied (introduced into the SRS), optionally with lapses.</summary>
    public async Task<List<Word>> StudyAsync(int lapses, params string[] korean)
    {
        var words = await Db.Words.Where(w => korean.Contains(w.Korean)).ToListAsync();
        foreach (var word in words.DistinctBy(w => w.Korean))
        {
            Db.WordProgress.Add(new WordProgress
            {
                WordId = word.Id,
                Phase = Domain.Srs.CardPhase.Review,
                Ease = 2.5,
                IntervalDays = 3,
                Lapses = lapses,
                DueUtc = Clock.UtcNow.AddDays(1),
                IntroducedUtc = Clock.UtcNow,
                LastReviewedUtc = Clock.UtcNow,
            });
        }

        await Db.SaveChangesAsync();
        return words;
    }

    public async Task<UserSettings> GetSettingsAsync() => await Settings.GetEntityAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
