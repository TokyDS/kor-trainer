#:property PublishAot=false
#:project ../../src/KorTrainer.Infrastructure/KorTrainer.Infrastructure.csproj

// Builds src/KorTrainer.Infrastructure/Seed/sentences-tatoeba.json: example sentences from Tatoeba linked to
// deck words. Run from the repository root:
//
//   dotnet run tools/sentences/BuildSentences.cs                 # rebuild the seed from selected.tsv
//   dotnet run tools/sentences/BuildSentences.cs -- <tatoeba-dir> # re-select from a Tatoeba export first
//
// <tatoeba-dir> holds the per-language exports from https://downloads.tatoeba.org/exports/per_language/:
// kor_sentences_detailed.tsv, kor-eng_links.tsv, kor-rus_links.tsv, eng_sentences.tsv, rus_sentences.tsv.
// Selection keeps short sentences whose every word the deck knows, up to three per word, preferring sentences
// made of earlier (more frequent) words. Russian comes from Tatoeba when it exists, otherwise from
// sentences.ru.tsv (machine translations, flagged in the app).

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using KorTrainer.Domain.Hangul;
using KorTrainer.Domain.Vocabulary;
using KorTrainer.Infrastructure.Seed;

const int PerWord = 3;
const int MaxLength = 40;
const int MaxTokens = 9;

string root = Directory.GetCurrentDirectory();
string toolDir = Path.Combine(root, "tools", "sentences");
string selectedPath = Path.Combine(toolDir, "selected.tsv");
string russianPath = Path.Combine(toolDir, "sentences.ru.tsv");
string excludedPath = Path.Combine(toolDir, "excluded.txt");
string output = Path.Combine(root, "src", "KorTrainer.Infrastructure", "Seed", "sentences-tatoeba.json");

var deck = DeckLoader.LoadBundled();
var index = new WordIndex(deck.Select(e => new IndexedWord(e.DeckKey, e.Korean, e.PartOfSpeech, e.Irregularity, e.Rank)));

if (args.Length > 0)
{
    Select(args[0]);
}

Build();

void Select(string dir)
{
    var english = Links(Path.Combine(dir, "kor-eng_links.tsv"));
    var russian = Links(Path.Combine(dir, "kor-rus_links.tsv"));
    var wantedEnglish = english.Values.ToHashSet();
    var wantedRussian = russian.Values.ToHashSet();
    var englishText = Texts(Path.Combine(dir, "eng_sentences.tsv"), wantedEnglish);
    var russianText = Texts(Path.Combine(dir, "rus_sentences.tsv"), wantedRussian);

    var candidates = new List<Candidate>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(Path.Combine(dir, "kor_sentences_detailed.tsv")))
    {
        var p = line.Split('\t');
        int id = int.Parse(p[0]);
        string ko = p[2].Trim();
        string? en = english.TryGetValue(id, out int e) ? englishText.GetValueOrDefault(e) : null;
        string? ru = russian.TryGetValue(id, out int r) ? russianText.GetValueOrDefault(r) : null;
        if ((en is null && ru is null) || !IsClean(ko) || !seen.Add(AnswerChecker.Normalize(ko)))
        {
            continue;
        }

        var tokens = index.Analyze(ko);
        if (tokens.Count > MaxTokens || WordIndex.Coverage(tokens) < 1)
        {
            continue;
        }

        var words = tokens.Where(t => t.IsKnown).Select(t => t.Words[0]).ToList();
        candidates.Add(new Candidate(id, ko, en ?? string.Empty, ru ?? string.Empty, p[3], words.Select(w => w.Key).Distinct().ToList(), words.Max(w => w.Rank)));
    }

    // Up to three sentences per word, preferring ones built from earlier words, then shorter ones.
    var picked = new HashSet<int>();
    foreach (var group in candidates.SelectMany(c => c.Words.Select(w => (Word: w, Sentence: c))).GroupBy(x => x.Word))
    {
        foreach (var (_, sentence) in group.OrderBy(x => x.Sentence.Level).ThenBy(x => x.Sentence.Korean.Length).Take(PerWord))
        {
            picked.Add(sentence.Id);
        }
    }

    var lines = candidates.Where(c => picked.Contains(c.Id)).OrderBy(c => c.Id)
        .Select(c => string.Join('\t', c.Id, c.Korean, Clean(c.English), Clean(c.Russian), c.Author));
    File.WriteAllLines(selectedPath, lines, new UTF8Encoding(false));
    Console.WriteLine($"{candidates.Count} fully known sentences, {picked.Count} selected → {selectedPath}");
}

void Build()
{
    var machine = File.Exists(russianPath)
        ? File.ReadLines(russianPath).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToDictionary(p => int.Parse(p[0]), p => p[1].Trim())
        : [];
    var exclusions = File.ReadLines(excludedPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
    var excludedIds = exclusions.Where(l => int.TryParse(l, out _)).Select(int.Parse).ToHashSet();
    var excludedKorean = exclusions.Where(l => l.StartsWith("ko:", StringComparison.Ordinal)).Select(l => l[3..]).ToList();
    var excludedRussian = exclusions.Where(l => l.StartsWith("ru:", StringComparison.Ordinal)).Select(l => l[3..].ToLowerInvariant()).ToList();
    var sentences = new List<object>();
    int missing = 0;
    int excluded = 0;
    foreach (var p in File.ReadLines(selectedPath).Select(l => l.Split('\t')))
    {
        int id = int.Parse(p[0]);
        bool human = p[3].Length > 0;
        string? ru = human ? p[3] : machine.GetValueOrDefault(id);
        if (ru is null)
        {
            missing++;
            continue;
        }

        string lower = ru.ToLowerInvariant();
        if (excludedIds.Contains(id) || excludedKorean.Any(p[1].Contains) || excludedRussian.Any(lower.Contains))
        {
            excluded++;
            continue;
        }

        var keys = index.Analyze(p[1]).Where(t => t.IsKnown).Select(t => t.Words[0].Key).Distinct().ToList();
        sentences.Add(new { id, ko = p[1], ru, author = p[4], machine = !human, words = keys });
    }

    var json = JsonSerializer.Serialize(
        new
        {
            version = 1,
            note = "Generated by tools/sentences/BuildSentences.cs from Tatoeba (https://tatoeba.org, CC BY 2.0 FR). Russian marked machine=true was machine-translated. Do not edit by hand.",
            sentences,
        },
        new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
    File.WriteAllText(output, json + Environment.NewLine);
    Console.WriteLine($"{sentences.Count} sentences → {output} ({excluded} excluded, {missing} still need a Russian translation in {russianPath})");
}

static Dictionary<int, int> Links(string path) => File.ReadLines(path)
    .Select(l => l.Split('\t'))
    .GroupBy(p => int.Parse(p[0]))
    .ToDictionary(g => g.Key, g => int.Parse(g.First()[1]));

static Dictionary<int, string> Texts(string path, HashSet<int> wanted)
{
    var texts = new Dictionary<int, string>();
    foreach (var line in File.ReadLines(path))
    {
        int tab = line.IndexOf('\t');
        if (tab > 0 && int.TryParse(line.AsSpan(0, tab), out int id) && wanted.Contains(id))
        {
            texts[id] = line[(line.IndexOf('\t', tab + 1) + 1)..];
        }
    }

    return texts;
}

// Short, Hangul-only sentences (digits and simple punctuation allowed).
static bool IsClean(string ko) =>
    ko.Length is >= 3 and <= MaxLength
    && ko.Any(Syllable.IsSyllable)
    && ko.All(c => Syllable.IsSyllable(c) || char.IsDigit(c) || " .,?!~\"'".Contains(c));

static string Clean(string text) => text.Replace('\t', ' ').Trim();

internal sealed record Candidate(int Id, string Korean, string English, string Russian, string Author, List<string> Words, int Level);
