using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

    DbSet<DailyTaskCompletion> TaskCompletions { get; }

    DbSet<WritingDraft> WritingDrafts { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
