using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Vocabulary;

/// <summary>Translations the learner doubts, collected for a later fix of the deck; part of the backup.</summary>
public sealed class TranslationFlagService(IAppDbContext db, StudyClock clock)
{
    /// <summary>Flags the word's translation, or replaces the note of an existing flag.</summary>
    public async Task<TranslationFlagDto> FlagAsync(int wordId, FlagTranslationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        new Validator()
            .Require(note is null || note.Length <= TranslationFlagLimits.MaxNoteLength, nameof(request.Note), $"Заметка — не длиннее {TranslationFlagLimits.MaxNoteLength} знаков.")
            .ThrowIfInvalid();
        var word = await db.Words.AsNoTracking().SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken)
            ?? throw new NotFoundException("Такого слова нет в колоде.");

        var flag = await db.TranslationFlags.SingleOrDefaultAsync(f => f.WordId == wordId, cancellationToken);
        if (flag is null)
        {
            flag = new TranslationFlag { WordId = wordId, Russian = word.Russian, CreatedUtc = clock.UtcNow };
            db.TranslationFlags.Add(flag);
        }
        else if (flag.Russian != word.Russian)
        {
            // Flagged again after a fix: it is the new translation that is in doubt now.
            flag.Russian = word.Russian;
            flag.CreatedUtc = clock.UtcNow;
        }

        flag.Note = note;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(flag, word);
    }

    public Task UnflagAsync(int wordId, CancellationToken cancellationToken) =>
        db.TranslationFlags.Where(f => f.WordId == wordId).ExecuteDeleteAsync(cancellationToken);

    /// <summary>Every flag, newest first.</summary>
    public async Task<IReadOnlyList<TranslationFlagDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var flags = await db.TranslationFlags.AsNoTracking()
            .Include(f => f.Word)
            .OrderByDescending(f => f.CreatedUtc)
            .ToListAsync(cancellationToken);
        return flags.Select(f => ToDto(f, f.Word!)).ToList();
    }

    public static TranslationFlagDto ToDto(TranslationFlag flag, Word word) =>
        new(word.Id, word.Korean, flag.Russian, word.Russian, flag.Note, flag.CreatedUtc);
}
