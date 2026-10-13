using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Entities;

public enum PartOfSpeech
{
    Noun = 1,
    Pronoun = 2,
    Verb = 3,
    Adjective = 4,
    Adverb = 5,
    Numeral = 6,
    Counter = 7,
    Determiner = 8,
    Phrase = 9,
}

/// <summary>Conjugation classes that cannot be inferred from spelling (입다 is regular, 덥다 is not).</summary>
public enum Irregularity
{
    None = 0,
    Bieup = 1,
    Digeut = 2,
    Reu = 3,
    Siot = 4,
    Hieut = 5,
}

/// <summary>A vocabulary entry from the bundled deck.</summary>
public sealed class Word
{
    public int Id { get; set; }

    /// <summary>Stable key from the deck file (e.g. "눈#2"), survives translation fixes and exports.</summary>
    public required string DeckKey { get; set; }

    public required string Korean { get; set; }

    public required string Russian { get; set; }

    public required string Topic { get; set; }

    public PartOfSpeech PartOfSpeech { get; set; }

    public Irregularity Irregularity { get; set; }

    public PlanStage Stage { get; set; }

    /// <summary>Plan day (4–9) that introduces the word during "first phrases"; null afterwards.</summary>
    public int? LessonDay { get; set; }

    /// <summary>Order of introduction within a stage (lower first).</summary>
    public int Rank { get; set; }

    public WordProgress? Progress { get; set; }
}

/// <summary>SRS state of a word that has been introduced to the learner.</summary>
public sealed class WordProgress
{
    public int WordId { get; set; }

    public Word? Word { get; set; }

    public CardPhase Phase { get; set; }

    public double Ease { get; set; }

    public double IntervalDays { get; set; }

    public int Repetitions { get; set; }

    public int Lapses { get; set; }

    public int Step { get; set; }

    public DateTime DueUtc { get; set; }

    public DateTime IntroducedUtc { get; set; }

    public DateTime LastReviewedUtc { get; set; }

    public CardState ToCardState() => new(Phase, Ease, IntervalDays, Repetitions, Lapses, Step, DueUtc);

    public void Apply(CardState state, DateTime reviewedUtc)
    {
        Phase = state.Phase;
        Ease = state.Ease;
        IntervalDays = state.IntervalDays;
        Repetitions = state.Repetitions;
        Lapses = state.Lapses;
        Step = state.Step;
        DueUtc = state.DueUtc;
        LastReviewedUtc = reviewedUtc;
    }
}
