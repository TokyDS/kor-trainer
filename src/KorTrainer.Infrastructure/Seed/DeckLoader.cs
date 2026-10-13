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
    int Rank);

/// <summary>Reads the bundled vocabulary deck (embedded <c>Seed/words.json</c>).</summary>
public static class DeckLoader
{
    private const string ResourceName = "KorTrainer.Infrastructure.Seed.words.json";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<DeckEntry> LoadBundled()
    {
        using var stream = typeof(DeckLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded deck '{ResourceName}' was not found.");
        return Parse(stream);
    }

    public static IReadOnlyList<DeckEntry> Parse(Stream json)
    {
        var deck = JsonSerializer.Deserialize<DeckFile>(json, Options)
            ?? throw new InvalidDataException("The deck file is empty.");

        var occurrences = new Dictionary<string, int>();
        var entries = new List<DeckEntry>();
        foreach (var section in deck.Sections)
        {
            var stage = Enum.Parse<PlanStage>(section.Stage);
            foreach (var word in section.Words)
            {
                if (word.Length is < 3 or > 4)
                {
                    throw new InvalidDataException($"Deck entry [{string.Join(", ", word)}] must have 3 or 4 fields.");
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
                    word.Length == 4 ? ParseIrregularity(word[3]) : Irregularity.None,
                    stage,
                    section.Day,
                    entries.Count + 1));
            }
        }

        return entries;
    }

    private static PartOfSpeech ParsePartOfSpeech(string value) =>
        Enum.TryParse<PartOfSpeech>(value, ignoreCase: true, out var pos)
            ? pos
            : throw new InvalidDataException($"Unknown part of speech '{value}'.");

    private static Irregularity ParseIrregularity(string value) => value switch
    {
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
