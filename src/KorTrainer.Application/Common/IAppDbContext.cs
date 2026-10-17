using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KorTrainer.Application.Common;

/// <summary>Persistence boundary used by the application services; implemented with EF Core in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<UserSettings> Settings { get; }

    DbSet<Word> Words { get; }

    DbSet<WordProgress> WordProgress { get; }

    DbSet<ReviewLog> ReviewLogs { get; }

    DbSet<ExerciseAttempt> ExerciseAttempts { get; }

    DbSet<ReadingTrial> ReadingTrials { get; }

    DbSet<StudySession> StudySessions { get; }

    DbSet<PlanStepProgress> PlanSteps { get; }

    DbSet<StepTaskCompletion> StepTaskCompletions { get; }

    DbSet<WritingDraft> WritingDrafts { get; }

    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
