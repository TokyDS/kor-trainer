using KorTrainer.Application.Common;
using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KorTrainer.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<UserSettings> Settings => Set<UserSettings>();

    public DbSet<Word> Words => Set<Word>();

    public DbSet<WordProgress> WordProgress => Set<WordProgress>();

    public DbSet<ReviewLog> ReviewLogs => Set<ReviewLog>();

    public DbSet<ExerciseAttempt> ExerciseAttempts => Set<ExerciseAttempt>();

    public DbSet<Mistake> Mistakes => Set<Mistake>();

    public DbSet<ReadingTrial> ReadingTrials => Set<ReadingTrial>();

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<PlanStepProgress> PlanSteps => Set<PlanStepProgress>();

    public DbSet<WordNote> WordNotes => Set<WordNote>();

    public DbSet<StepTaskCompletion> StepTaskCompletions => Set<StepTaskCompletion>();

    public DbSet<WritingDraft> WritingDrafts => Set<WritingDraft>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no time zone support: store UTC and restore DateTimeKind.Utc on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
