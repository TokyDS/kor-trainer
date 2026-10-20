using System.Text.Json;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Infrastructure.Seed;

public sealed record DeckEntry(
    string DeckKey,
    string Korean,
    string Russian,
    string Topic,
    PartOfSpeech PartOfSpeech,
    Irregularity Irregularity,
    PlanStage Stage,
    int? LessonDay,
    int Rank,
    string? Hanja = null);

/// <summary>
/// Reads the bundled vocabulary decks: the curated beginner deck first, then the frequency deck
/// generated from the NIKL learner list. Order matters: homonym keys (눈#1, 눈#2) count across files.
/// </summary>
public static class DeckLoader
{
    private static readonly string[] ResourceNames =
    [
        "KorTrainer.Infrastructure.Seed.words.json",
        "KorTrainer.Infrastructure.Seed.words-nikl.json",
    ];

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<DeckEntry> LoadBundled()
    {
        var streams = ResourceNames
            .Select(name => typeof(DeckLoader).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded deck '{name}' was not found."))
            .ToList();
        try
        {
            return Parse(streams);
        }
        finally
        {
            streams.ForEach(s => s.Dispose());
        }
    }

    public static IReadOnlyList<DeckEntry> Parse(Stream json) => Parse([json]);

    public static IReadOnlyList<DeckEntry> Parse(IEnumerable<Stream> decks)
    {
        var occurrences = new Dictionary<string, int>();
        var entries = new List<DeckEntry>();
        foreach (var json in decks)
        {
            var deck = JsonSerializer.Deserialize<DeckFile>(json, Options)
                ?? throw new InvalidDataException("The deck file is empty.");
            AddSections(deck, occurrences, entries);
        }

        return entries;
    }

    private static void AddSections(DeckFile deck, Dictionary<string, int> occurrences, List<DeckEntry> entries)
    {
        foreach (var section in deck.Sections)
        {
            var stage = Enum.Parse<PlanStage>(section.Stage);
            foreach (var word in section.Words)
            {
                if (word.Length is < 3 or > 5)
                {
                    throw new InvalidDataException($"Deck entry [{string.Join(", ", word)}] must have 3 to 5 fields.");
                }

                string korean = word[0].Trim();
                int occurrence = occurrences.GetValueOrDefault(korean) + 1;
                occurrences[korean] = occurrence;

                entries.Add(new DeckEntry(
                    $"{korean}#{occurrence}",
                    korean,
                    word[1].Trim(),
                    section.Topic,
                    ParsePartOfSpeech(word[2]),
                    word.Length >= 4 ? ParseIrregularity(word[3]) : Irregularity.None,
                    stage,
                    section.Day,
                    entries.Count + 1,
                    word.Length == 5 && word[4].Trim().Length > 0 ? word[4].Trim() : null));
            }
        }
    }

    private static PartOfSpeech ParsePartOfSpeech(string value) =>
        Enum.TryParse<PartOfSpeech>(value, ignoreCase: true, out var pos)
            ? pos
            : throw new InvalidDataException($"Unknown part of speech '{value}'.");

    private static Irregularity ParseIrregularity(string value) => value switch
    {
        "" => Irregularity.None,
        "b" => Irregularity.Bieup,
        "d" => Irregularity.Digeut,
        "reu" => Irregularity.Reu,
        "s" => Irregularity.Siot,
        "h" => Irregularity.Hieut,
        _ => throw new InvalidDataException($"Unknown irregularity '{value}'."),
    };

    private sealed record DeckFile(int Version, IReadOnlyList<DeckSection> Sections);

    private sealed record DeckSection(string Stage, int? Day, string Topic, IReadOnlyList<string[]> Words);
}
