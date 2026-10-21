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

public sealed class GrammarService(IAppDbContext db, PlanCursor cursor, ISentenceLibrary sentences, GrammarClozeCache clozeCache, Random random)
{
    public const int MaxCount = 50;

    /// <summary>Share of topic-drill questions asked as a gap in a sentence.</summary>
    private const double SentenceShare = 0.5;

    /// <summary>Longer sentences make a gap drill slow to read on a phone.</summary>
    private const int MaxSentenceLength = 40;

    /// <summary>Common misspellings in Tatoeba sentences that must not be shown as a model.</summary>
    private static readonly string[] Misspellings = ["되요", "됬", "갰어"];

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
        bool sentencesOnly = type == ExerciseTypes.GrammarSentences;
        new Validator()
            .Require(topic is not null || type is ExerciseTypes.Mixed or ExerciseTypes.GrammarSentences, nameof(type), "Неизвестная тема грамматики.")
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

        var known = (await db.Words.Where(w => w.Stage <= maxWordStage).Select(w => w.DeckKey).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var allGaps = clozeCache.GetOrBuild(maxWordStage, () => GrammarClozeFinder.Find(KnownSentences(known), GrammarCatalog.Topics, nouns, verbs));
        var types = topics.Select(t => t.Type).ToHashSet(StringComparer.Ordinal);
        var cloze = new ClozeSettings(
            allGaps.Where(m => types.Contains(m.Topic.Type)).ToList(),
            sentencesOnly ? 1 : SentenceShare,
            RevealEnding: !sentencesOnly);

        List<ChoiceExerciseDto> items;
        try
        {
            items = new GrammarExerciseGenerator(random).Generate(topics, count, nouns, verbs, cloze).Select(ExerciseMapper.ToDto).ToList();
        }
        catch (InvalidOperationException)
        {
            throw ValidationFailedException.For(nameof(type), sentencesOnly
                ? "Пока нет подходящих предложений для открытых тем — они появятся с новыми словами."
                : "В колоде нет подходящих слов для этой темы.");
        }

        string title = sentencesOnly ? "Грамматика в предложениях" : topic is null ? "Грамматика вперемешку" : $"Грамматика: {topic.Title}";
        return new ExerciseSetDto(StudyModule.Grammar, type, title, items);
    }

    /// <summary>Short sentences made only of words the learner's stage has opened.</summary>
    private IEnumerable<ClozeSentence> KnownSentences(HashSet<string> known) => sentences.Sentences
        .Where(s => s.Korean.Length <= MaxSentenceLength && s.WordKeys.All(known.Contains))
        .Where(s => !Misspellings.Any(m => s.Korean.Contains(m, StringComparison.Ordinal)))
        .Select(s => new ClozeSentence(s.Id, s.Korean, s.Russian, s.MachineTranslated));

    /// <summary>Topics the plan has introduced so far — at least the first ones, so the mixed drill is never empty.</summary>
    private async Task<(PlanStage Stage, IReadOnlyList<GrammarTopic> Topics)> UnlockedTopicsAsync(CancellationToken cancellationToken)
    {
        var current = await cursor.GetAsync(cancellationToken);
        var unlocked = current.Roadmap.GrammarUnlocked(current.Step);
        return (current.Step.Stage, unlocked.Count > 0 ? unlocked : GrammarCatalog.Topics.Where(t => t.Stage == PlanStage.FirstPhrases).ToList());
    }
}
