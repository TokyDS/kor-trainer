using KorTrainer.Application.Activity;
using KorTrainer.Application.Common;
using KorTrainer.Application.Grammar;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Progress;
using KorTrainer.Application.Settings;
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
        Vocabulary = new VocabularyService(db, Settings, Clock, new SrsScheduler());
        Activity = new ActivityService(db, Settings, Clock);
        Today = new TodayService(db, Settings, Vocabulary, Clock);
        Hangul = new HangulService(db, Settings, Clock, new Random(42));
        Grammar = new GrammarService(db, Settings, Clock, new Random(42));
        Progress = new ProgressTransferService(db, Settings, Clock);
    }

    public AppDbContext Db { get; }

    public FakeTimeProvider Time { get; }

    public StudyClock Clock { get; }

    public SettingsService Settings { get; }

    public VocabularyService Vocabulary { get; }

    public ActivityService Activity { get; }

    public TodayService Today { get; }

    public HangulService Hangul { get; }

    public GrammarService Grammar { get; }

    public ProgressTransferService Progress { get; }

    public static async Task<TestDatabase> CreateAsync(DateTimeOffset? now = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        await new DatabaseInitializer(db, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        return new TestDatabase(connection, db, new FakeTimeProvider(now ?? DayOneMorning));
    }

    /// <summary>Moves the plan start so that "now" falls on the given plan day.</summary>
    public async Task SetPlanDayAsync(int day)
    {
        var settings = await Settings.GetEntityAsync(CancellationToken.None);
        settings.StartDate = Clock.Today(settings).AddDays(1 - day);
        await Db.SaveChangesAsync();
    }

    public async Task<UserSettings> GetSettingsAsync() => await Settings.GetEntityAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
