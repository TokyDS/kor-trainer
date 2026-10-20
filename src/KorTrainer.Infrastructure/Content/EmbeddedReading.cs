using KorTrainer.Application.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Vocabulary;
using KorTrainer.Infrastructure.Seed;

namespace KorTrainer.Infrastructure.Content;

/// <summary>Seed/texts.json: original graded reading texts.</summary>
public sealed class EmbeddedTextLibrary : ITextLibrary
{
    private readonly Lazy<IReadOnlyList<ReadingText>> _texts = new(() =>
        EmbeddedResource.ReadJson<TextFile>("texts.json").Texts
            .Select(t => new ReadingText(
                t.Id,
                Enum.Parse<PlanStage>(t.Stage),
                t.Title,
                t.TitleKo,
                t.Sentences.Select(s => new TextSentence(s.Ko, s.Ru)).ToList(),
                t.Questions.Select(q => new TextQuestion(q.Question, q.Options, q.Answer)).ToList()))
            .ToList());

    public IReadOnlyList<ReadingText> Texts => _texts.Value;

    private sealed record TextFile(int Version, IReadOnlyList<TextRow> Texts);

    private sealed record TextRow(string Id, string Stage, string Title, string TitleKo, IReadOnlyList<SentenceRow> Sentences, IReadOnlyList<QuestionRow> Questions);

    private sealed record SentenceRow(string Ko, string Ru);

    private sealed record QuestionRow(string Question, IReadOnlyList<string> Options, int Answer);
}

/// <summary>Seed/topik-reading.json: original items in TOPIK I reading formats.</summary>
public sealed class EmbeddedTopikItemBank : ITopikItemBank
{
    private readonly Lazy<TopikFile> _file = new(() => EmbeddedResource.ReadJson<TopikFile>("topik-reading.json"));

    public IReadOnlyDictionary<string, string> Instructions => _file.Value.Instructions;

    public IReadOnlyList<TopikItem> Items => _items ??= _file.Value.Items
        .Select(i => new TopikItem(i.Id, i.Format, Enum.Parse<PlanStage>(i.Stage), i.Prompt, i.Translation, i.Options, i.Answer, i.Explanation))
        .ToList();

    private IReadOnlyList<TopikItem>? _items;

    private sealed record TopikFile(int Version, Dictionary<string, string> Instructions, IReadOnlyList<ItemRow> Items);

    private sealed record ItemRow(string Id, string Format, string Stage, string Prompt, string Translation, IReadOnlyList<string> Options, int Answer, string Explanation);
}

/// <summary>A WordIndex over the bundled deck (the same words the database is seeded with).</summary>
public sealed class BundledWordIndex : IWordIndexProvider
{
    private readonly Lazy<WordIndex> _index = new(() => new WordIndex(
        DeckLoader.LoadBundled().Select(e => new IndexedWord(e.DeckKey, e.Korean, e.PartOfSpeech, e.Irregularity, e.Rank))));

    public WordIndex Index => _index.Value;
}
