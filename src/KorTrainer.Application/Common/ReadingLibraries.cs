using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Application.Common;

public sealed record TextSentence(string Korean, string Russian);

public sealed record TextQuestion(string Question, IReadOnlyList<string> Options, int Answer);

/// <summary>An original graded reading text with comprehension questions.</summary>
public sealed record ReadingText(string Id, PlanStage Stage, string Title, string TitleKorean, IReadOnlyList<TextSentence> Sentences, IReadOnlyList<TextQuestion> Questions);

public interface ITextLibrary
{
    IReadOnlyList<ReadingText> Texts { get; }
}

/// <summary>An original item in a TOPIK I reading format.</summary>
/// <param name="Format">topic, cloze, same-meaning, main-idea or notice.</param>
public sealed record TopikItem(string Id, string Format, PlanStage Stage, string Prompt, string Translation, IReadOnlyList<string> Options, int Answer, string Explanation);

public interface ITopikItemBank
{
    /// <summary>The exam instruction (in Korean) for each format.</summary>
    IReadOnlyDictionary<string, string> Instructions { get; }

    IReadOnlyList<TopikItem> Items { get; }
}

/// <summary>The recognizer of inflected deck words, built once from the bundled deck.</summary>
public interface IWordIndexProvider
{
    WordIndex Index { get; }
}
