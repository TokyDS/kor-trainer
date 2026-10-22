using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Application.Common;

/// <summary>Native names and Russian glosses of hanja characters (bundled reference data).</summary>
public interface IHanjaGlossary
{
    IReadOnlyDictionary<char, HanjaGloss> Glosses { get; }
}

/// <param name="WordKeys">Deck keys of the words the sentence contains, in order.</param>
/// <param name="MachineTranslated">The Russian is a machine translation (the Korean and its author are from Tatoeba).</param>
public sealed record ExampleSentence(int Id, string Korean, string Russian, string Author, bool MachineTranslated, IReadOnlyList<string> WordKeys);

/// <param name="Topic">The grammar topic the sentence was written for (an <c>ExerciseTypes</c> id).</param>
/// <param name="Stage">The earliest stage whose words the sentence uses.</param>
public sealed record GrammarSentence(int Id, string Topic, KorTrainer.Domain.Plan.PlanStage Stage, string Korean, string Russian);

/// <summary>Original sentences written for grammar topics that Tatoeba barely covers (bundled).</summary>
public interface IGrammarSentenceLibrary
{
    IReadOnlyList<GrammarSentence> Sentences { get; }
}

/// <summary>Example sentences from Tatoeba linked to deck words (bundled, CC BY 2.0 FR).</summary>
public interface ISentenceLibrary
{
    IReadOnlyList<ExampleSentence> Sentences { get; }

    /// <summary>Sentences containing the word, best first (human translations, shorter).</summary>
    IReadOnlyList<ExampleSentence> ForWord(string deckKey);
}
