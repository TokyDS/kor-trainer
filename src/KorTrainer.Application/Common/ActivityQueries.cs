using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Common;

internal static class ActivityQueries
{
    /// <summary>Local dates with any study activity: reviews, answers, reading, writing or a ticked task.</summary>
    public static async Task<HashSet<DateOnly>> ActiveDaysAsync(this IAppDbContext db, CancellationToken cancellationToken)
    {
        var days = new HashSet<DateOnly>();
        days.UnionWith(await db.ReviewLogs.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.ExerciseAttempts.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.ReadingTrials.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.WritingDrafts.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.TaskCompletions.Select(r => r.Date).Distinct().ToListAsync(cancellationToken));
        return days;
    }
}
