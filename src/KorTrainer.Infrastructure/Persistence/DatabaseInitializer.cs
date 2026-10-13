using KorTrainer.Domain.Entities;
using KorTrainer.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KorTrainer.Infrastructure.Persistence;

/// <summary>Applies migrations and synchronizes the bundled deck on startup.</summary>
public sealed class DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SyncDeckAsync(DeckLoader.LoadBundled(), cancellationToken);
    }

    /// <summary>
    /// Inserts new deck entries and updates changed ones by deck key. Words removed from the
    /// deck are kept, so the learner's progress is never lost.
    /// </summary>
    public async Task<int> SyncDeckAsync(IReadOnlyList<DeckEntry> deck, CancellationToken cancellationToken)
    {
        var existing = await db.Words.ToDictionaryAsync(w => w.DeckKey, cancellationToken);
        foreach (var entry in deck)
        {
            if (!existing.TryGetValue(entry.DeckKey, out var word))
            {
                word = new Word { DeckKey = entry.DeckKey, Korean = entry.Korean, Russian = entry.Russian, Topic = entry.Topic };
                db.Words.Add(word);
            }

            word.Korean = entry.Korean;
            word.Russian = entry.Russian;
            word.Topic = entry.Topic;
            word.PartOfSpeech = entry.PartOfSpeech;
            word.Irregularity = entry.Irregularity;
            word.Stage = entry.Stage;
            word.LessonDay = entry.LessonDay;
            word.Rank = entry.Rank;
        }

        int changes = await db.SaveChangesAsync(cancellationToken);
        if (changes > 0)
        {
            logger.LogInformation("Deck synchronized: {Changes} rows inserted or updated ({Total} words in deck)", changes, deck.Count);
        }

        return changes;
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        if (!await db.Settings.AnyAsync(cancellationToken))
        {
            db.Settings.Add(new UserSettings());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
