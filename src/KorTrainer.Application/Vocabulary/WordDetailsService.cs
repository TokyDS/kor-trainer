using KorTrainer.Application.Common;
using KorTrainer.Application.Hangul;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Srs;
using KorTrainer.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Vocabulary;

/// <summary>Memory aids for a word (roots, examples, own note) and the practice of words that keep slipping away.</summary>
public sealed class WordDetailsService(IAppDbContext db, IHanjaGlossary glossary, ISentenceLibrary sentences, StudyClock clock, Random random)
{
    public const int MaxCount = 50;
    private const int ExampleCount = 3;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.HardMeaning] = "Трудные слова: значение",
        [ExerciseTypes.HardSpelling] = "Трудные слова: написание",
    };

    public async Task<WordDetailsDto> GetAsync(int wordId, CancellationToken cancellationToken)
    {
        var word = await db.Words.AsNoTracking().Include(w => w.Progress).SingleOrDefaultAsync(w => w.Id == wordId, cancellationToken)
            ?? throw new NotFoundException("Такого слова нет в колоде.");
        var note = await db.WordNotes.AsNoTracking().Where(n => n.WordId == wordId).Select(n => n.Text).SingleOrDefaultAsync(cancellationToken);

        var roots = new List<HanjaPartDto>();
        foreach (var pair in Domain.Vocabulary.Hanja.Align(word.Korean, word.PartOfSpeech, word.Hanja))
        {
            string character = pair.Character.ToString();
            int family = await db.Words.CountAsync(w => w.Hanja != null && w.Hanja.Contains(character), cancellationToken);
            var gloss = glossary.Glosses.GetValueOrDefault(pair.Character);
            roots.Add(new HanjaPartDto(pair.Syllable.ToString(), character, gloss?.Hun, gloss?.Russian, family));
        }

        string? hardReason = null;
        if (word.Progress is { } progress)
        {
            var score = HardWords.Score(progress.Lapses, await RecentGradesAsync(word.Id, cancellationToken));
            hardReason = score.IsHard ? score.Reason : null;
        }

        var examples = sentences.ForWord(word.DeckKey).Take(ExampleCount)
            .Select(s => new ExampleDto(s.Id, s.Korean, s.Russian, s.MachineTranslated, s.Author))
            .ToList();
        return new WordDetailsDto(word.Id, word.Korean, word.Russian, word.Hanja, roots, examples, note, hardReason);
    }

    /// <summary>Saves the learner's memory aid; an empty text removes it.</summary>
    public async Task SaveNoteAsync(int wordId, SaveNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string text = (request.Text ?? string.Empty).Trim();
        new Validator()
            .Require(text.Length <= WordNoteLimits.MaxLength, nameof(request.Text), $"Подсказка — не длиннее {WordNoteLimits.MaxLength} знаков.")
            .ThrowIfInvalid();
        if (!await db.Words.AnyAsync(w => w.Id == wordId, cancellationToken))
        {
            throw new NotFoundException("Такого слова нет в колоде.");
        }

        var note = await db.WordNotes.SingleOrDefaultAsync(n => n.WordId == wordId, cancellationToken);
        if (text.Length == 0)
        {
            if (note is not null)
            {
                db.WordNotes.Remove(note);
            }
        }
        else if (note is null)
        {
            db.WordNotes.Add(new WordNote { WordId = wordId, Text = text, UpdatedUtc = clock.UtcNow });
        }
        else
        {
            note.Text = text;
            note.UpdatedUtc = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Words the learner keeps forgetting, hardest first.</summary>
    public async Task<IReadOnlyList<HardWordDto>> GetHardWordsAsync(CancellationToken cancellationToken)
    {
        var againIds = await db.ReviewLogs.Where(r => r.Grade == Grade.Again).Select(r => r.WordId).Distinct().ToListAsync(cancellationToken);
        var candidates = await db.WordProgress
            .Where(p => p.Lapses > 0 || againIds.Contains(p.WordId))
            .Select(p => new { p.WordId, p.Word!.Korean, p.Word.Russian, p.Lapses, p.DueUtc })
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var ids = candidates.Select(c => c.WordId).ToList();
        var grades = (await db.ReviewLogs
                .Where(r => ids.Contains(r.WordId))
                .Select(r => new { r.WordId, r.ReviewedUtc, r.Grade })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.WordId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Grade>)g.OrderBy(r => r.ReviewedUtc).Select(r => r.Grade).ToList());
        var notes = await db.WordNotes.Where(n => ids.Contains(n.WordId)).ToDictionaryAsync(n => n.WordId, n => n.Text, cancellationToken);

        return candidates
            .Select(c => (Word: c, Score: HardWords.Score(c.Lapses, grades.GetValueOrDefault(c.WordId) ?? [])))
            .Where(x => x.Score.IsHard)
            .OrderByDescending(x => x.Score.Value)
            .Select(x => new HardWordDto(x.Word.WordId, x.Word.Korean, x.Word.Russian, x.Score.Reason, x.Word.Lapses, notes.GetValueOrDefault(x.Word.WordId), x.Word.DueUtc))
            .ToList();
    }

    /// <summary>A focused drill over the hard words; it does not change their review schedule.</summary>
    public async Task<ExerciseSetDto> GetHardExercisesAsync(string type, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var hard = await GetHardWordsAsync(cancellationToken);
        if (hard.Count == 0)
        {
            throw ValidationFailedException.For(nameof(type), "Трудных слов пока нет — отлично!");
        }

        var keys = await db.Words.Where(w => hard.Select(h => h.WordId).Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.DeckKey, cancellationToken);
        var pool = await db.WordProgress
            .Select(p => new HardWordItem(p.Word!.DeckKey, p.Word.Korean, p.Word.Russian, null))
            .ToListAsync(cancellationToken);
        var items = hard.Select(h => new HardWordItem(keys[h.WordId], h.Korean, h.Russian, h.Note)).ToList();
        var exercises = new HardWordExerciseGenerator(random).Generate(type, count, items, pool);
        return new ExerciseSetDto(StudyModule.Vocabulary, type, Titles[type], exercises.Select(ExerciseMapper.ToDto).ToList());
    }

    private async Task<IReadOnlyList<Grade>> RecentGradesAsync(int wordId, CancellationToken cancellationToken) =>
        (await db.ReviewLogs.Where(r => r.WordId == wordId)
            .OrderByDescending(r => r.ReviewedUtc)
            .Take(HardWords.RecentAnswers)
            .Select(r => r.Grade)
            .ToListAsync(cancellationToken))
        .AsEnumerable().Reverse().ToList();
}
