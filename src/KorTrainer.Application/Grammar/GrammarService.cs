using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Grammar;

public sealed class GrammarService(IAppDbContext db, PlanCursor cursor, Random random)
{
    public const int MaxCount = 50;

    public async Task<IReadOnlyList<GrammarTopicDto>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        var unlocked = await UnlockedTopicsAsync(cancellationToken);
        return GrammarCatalog.Topics
            .Select(t => new GrammarTopicDto(t.Type, t.Title, t.Description, t.Stage, unlocked.Topics.Contains(t)))
            .ToList();
    }

    /// <param name="type">A topic type, or <see cref="ExerciseTypes.Mixed"/> for every topic the plan has introduced.</param>
    public async Task<ExerciseSetDto> GetExercisesAsync(string type, int count, CancellationToken cancellationToken)
    {
        var topic = GrammarCatalog.Find(type);
        new Validator()
            .Require(topic is not null || type == ExerciseTypes.Mixed, nameof(type), "Неизвестная тема грамматики.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var unlocked = await UnlockedTopicsAsync(cancellationToken);
        var topics = topic is not null ? [topic] : unlocked.Topics;
        var maxWordStage = StudyScope.MaxWordStage(unlocked.Stage);
        var words = await db.Words
            .Where(w => w.Stage <= maxWordStage)
            .Where(w => w.PartOfSpeech == PartOfSpeech.Noun || w.PartOfSpeech == PartOfSpeech.Verb || w.PartOfSpeech == PartOfSpeech.Adjective)
            .Select(w => new { w.Korean, w.Russian, w.PartOfSpeech, w.Irregularity })
            .ToListAsync(cancellationToken);

        var nouns = words.Where(w => w.PartOfSpeech == PartOfSpeech.Noun).Select(w => new GrammarNoun(w.Korean, w.Russian)).ToList();
        var verbs = words.Where(w => w.PartOfSpeech != PartOfSpeech.Noun)
            .Select(w => new GrammarVerb(w.Korean, w.Russian, w.PartOfSpeech, w.Irregularity))
            .ToList();

        List<ChoiceExerciseDto> items;
        try
        {
            items = new GrammarExerciseGenerator(random).Generate(topics, count, nouns, verbs).Select(ExerciseMapper.ToDto).ToList();
        }
        catch (InvalidOperationException)
        {
            throw ValidationFailedException.For(nameof(type), "В колоде нет подходящих слов для этой темы.");
        }
        string title = topic is null ? "Грамматика вперемешку" : $"Грамматика: {topic.Title}";
        return new ExerciseSetDto(StudyModule.Grammar, type, title, items);
    }

    /// <summary>Topics the plan has introduced so far — at least the first ones, so the mixed drill is never empty.</summary>
    private async Task<(PlanStage Stage, IReadOnlyList<GrammarTopic> Topics)> UnlockedTopicsAsync(CancellationToken cancellationToken)
    {
        var current = await cursor.GetAsync(cancellationToken);
        var unlocked = current.Roadmap.GrammarUnlocked(current.Step);
        return (current.Step.Stage, unlocked.Count > 0 ? unlocked : GrammarCatalog.Topics.Where(t => t.Stage == PlanStage.FirstPhrases).ToList());
    }
}
