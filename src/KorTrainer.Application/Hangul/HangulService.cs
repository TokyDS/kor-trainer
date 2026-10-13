using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Hangul;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Hangul;

public sealed class HangulService(IAppDbContext db, SettingsService settingsService, StudyClock clock, Random random)
{
    public const int MaxCount = 50;
    public const int MaxAnalyzeLength = 40;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.LetterToSound] = "Буква → звук",
        [ExerciseTypes.SoundToLetter] = "Звук → буква",
        [ExerciseTypes.Compose] = "Сборка слога",
        [ExerciseTypes.Decompose] = "Разбор слога",
        [ExerciseTypes.ConsonantSeries] = "Три ряда согласных",
        [ExerciseTypes.FinalSound] = "Звучание патчима",
        [ExerciseTypes.SoundChange] = "Чтение по правилам (연음)",
    };

    public static string TitleOf(string type) => Titles.GetValueOrDefault(type, type);

    public async Task<ExerciseSetDto> GetExercisesAsync(string type, int level, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(ExerciseTypes.HangulChoice.Contains(type), nameof(type), "Неизвестный тип упражнения.")
            .Require(Enum.IsDefined((HangulLevel)level), nameof(level), "Уровень: 1, 2 или 3.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var maxStage = await MaxWordStageAsync(cancellationToken);
        var words = await db.Words.Where(w => w.Stage <= maxStage).Select(w => w.Korean).ToListAsync(cancellationToken);
        var generator = new HangulExerciseGenerator(random);
        var items = generator.Generate(type, (HangulLevel)level, count, words).Select(ExerciseMapper.ToDto).ToList();
        return new ExerciseSetDto(StudyModule.Hangul, type, TitleOf(type), items);
    }

    public async Task<IReadOnlyList<ReadingItemDto>> GetReadingAsync(int level, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Enum.IsDefined((HangulLevel)level), nameof(level), "Уровень: 1, 2 или 3.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var maxStage = await MaxWordStageAsync(cancellationToken);
        var words = await db.Words
            .Where(w => w.Stage <= maxStage)
            .Select(w => new { w.Korean, w.Russian })
            .ToListAsync(cancellationToken);
        var generator = new HangulExerciseGenerator(random);
        return generator
            .Reading((HangulLevel)level, count, words.Select(w => (w.Korean, (string?)w.Russian)).ToList())
            .Select(i => new ReadingItemDto(i.Text, i.SyllableCount, i.Pronounced, i.Translation, Romanizer.Romanize(i.Text)))
            .ToList();
    }

    public async Task<ReadingStatsDto> GetReadingStatsAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var trials = await db.ReadingTrials
            .Select(t => new { t.SyllableCount, t.ElapsedMs, t.ReadCorrectly, t.LocalDate })
            .ToListAsync(cancellationToken);

        if (trials.Count == 0)
        {
            return new ReadingStatsDto(0, 0, 0, null);
        }

        var correct = trials.Where(t => t.ReadCorrectly).ToList();
        var todayCorrect = correct.Where(t => t.LocalDate == today).ToList();
        return new ReadingStatsDto(
            trials.Count,
            correct.Count == 0 ? 0 : Math.Round((double)correct.Sum(t => t.ElapsedMs) / correct.Sum(t => t.SyllableCount)),
            Math.Round((double)correct.Count / trials.Count, 3),
            todayCorrect.Count == 0 ? null : Math.Round((double)todayCorrect.Sum(t => t.ElapsedMs) / todayCorrect.Sum(t => t.SyllableCount)));
    }

    private async Task<PlanStage> MaxWordStageAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var stage = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).StageOn(clock.Today(settings));
        return StudyScope.MaxWordStage(stage);
    }

    public static HangulAnalysisDto Analyze(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw ValidationFailedException.For(nameof(text), "Введите текст на хангыле.");
        }

        new Validator()
            .Require(text.Length <= MaxAnalyzeLength, nameof(text), $"Текст: не длиннее {MaxAnalyzeLength} символов.")
            .Require(text.Any(Syllable.IsSyllable), nameof(text), "В тексте нет слогов хангыля.")
            .ThrowIfInvalid();

        var pronunciation = Pronunciation.Pronounce(text);
        var syllables = text
            .Where(Syllable.IsSyllable)
            .Select(Syllable.Decompose)
            .Select(s => new SyllablePartsDto(s.ToString(), s.Initial.ToString(), s.Medial.ToString(), s.Final?.ToString()))
            .ToList();
        var changes = pronunciation.Changes.Select(c => new SoundChangeDto(c.Rule.ToString(), c.Description)).ToList();
        return new HangulAnalysisDto(text, pronunciation.Text, Romanizer.Romanize(text), syllables, changes);
    }

    public static HangulGuideDto GetGuide(int level)
    {
        new Validator().Require(Enum.IsDefined((HangulLevel)level), nameof(level), "Уровень: 1, 2 или 3.").ThrowIfInvalid();

        var lesson = new StudyPlan(StudyPlan.DefaultStartDate).Lessons.Single(l => l.Day == level);
        var letters = (HangulLevel)level switch
        {
            HangulLevel.Basics => JamoCatalog.ForLevel(HangulLevel.Basics),
            HangulLevel.Full => JamoCatalog.All.Where(j => j.Level == HangulLevel.Full).ToList(),
            _ => JamoCatalog.All.Where(j => !j.IsVowel).ToList(),
        };

        return new HangulGuideDto(
            level,
            lesson.Title,
            lesson.KeyPoints,
            letters.Select(j => new JamoDto(j.Jamo.ToString(), j.Kind.ToString(), j.Sound, j.Hint, j.Name, j.SampleSyllable, j.Romanization)).ToList());
    }
}

internal static class ExerciseMapper
{
    public static ChoiceExerciseDto ToDto(ChoiceExercise e) => new(
        e.Type,
        e.ItemKey,
        e.Prompt,
        e.PromptNote,
        e.PromptRomanization,
        e.Options.Select(o => new ChoiceOptionDto(o.Text, o.Hint, o.Romanization)).ToList(),
        e.CorrectIndex,
        e.Explanation,
        e.SpeakText,
        e.AutoSpeak);
}
