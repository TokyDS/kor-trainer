namespace KorTrainer.Domain.Common;

public enum StudyModule
{
    Hangul = 1,
    Vocabulary = 2,
    Grammar = 3,
    Numbers = 4,
    Topik = 5,
}

/// <summary>Stable identifiers of exercise kinds, used in routes, attempts and plan targets.</summary>
public static class ExerciseTypes
{
    public const string LetterToSound = "letter-to-sound";
    public const string SoundToLetter = "sound-to-letter";
    public const string Compose = "compose";
    public const string Decompose = "decompose";
    public const string ConsonantSeries = "consonant-series";
    public const string FinalSound = "final-sound";
    public const string SoundChange = "sound-change";
    public const string Reading = "reading";

    public const string Copula = "copula";
    public const string NegativeCopula = "negative-copula";
    public const string TopicParticle = "topic";
    public const string SubjectParticle = "subject";
    public const string ObjectParticle = "object";
    public const string PolitePresent = "polite";
    public const string FormalPresent = "formal";
    public const string Past = "past";
    public const string AndGo = "and";
    public const string SoAseo = "so";
    public const string BecauseNikka = "because";
    public const string Want = "want";
    public const string Can = "can";
    public const string Must = "must";
    public const string Mixed = "mixed";

    public const string SinoNumbers = "sino";
    public const string NativeNumbers = "native";
    public const string Counters = "counter";
    public const string Time = "time";
    public const string Money = "money";

    public static IReadOnlyList<string> HangulChoice { get; } =
        [LetterToSound, SoundToLetter, Compose, Decompose, ConsonantSeries, FinalSound, SoundChange];
}
