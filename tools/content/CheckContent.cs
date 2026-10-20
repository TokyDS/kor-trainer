#:property PublishAot=false
#:project ../../src/KorTrainer.Infrastructure/KorTrainer.Infrastructure.csproj

// Checks the authored reading texts and TOPIK items against the deck: which words the app can link
// (tap-to-translate) and which it cannot. Run from the repository root:
//
//   dotnet run tools/content/CheckContent.cs            # summary per text
//   dotnet run tools/content/CheckContent.cs -- --all   # also list every unknown word

using System.Text.Json;
using KorTrainer.Domain.Vocabulary;
using KorTrainer.Infrastructure.Seed;

string seed = Path.Combine(Directory.GetCurrentDirectory(), "src", "KorTrainer.Infrastructure", "Seed");
var deck = DeckLoader.LoadBundled();
var index = new WordIndex(deck.Select(e => new IndexedWord(e.DeckKey, e.Korean, e.PartOfSpeech, e.Irregularity, e.Rank)));
bool all = args.Contains("--all");

using var texts = JsonDocument.Parse(File.ReadAllText(Path.Combine(seed, "texts.json")));
var unknownWords = new Dictionary<string, int>();
foreach (var text in texts.RootElement.GetProperty("texts").EnumerateArray())
{
    var tokens = text.GetProperty("sentences").EnumerateArray()
        .SelectMany(s => index.Analyze(s.GetProperty("ko").GetString()!))
        .ToList();
    var unknown = tokens.Where(t => t.Hangul.Length > 0 && !t.IsKnown).Select(t => t.Hangul).ToList();
    unknown.ForEach(u => unknownWords[u] = unknownWords.GetValueOrDefault(u) + 1);
    Console.WriteLine($"{text.GetProperty("id").GetString(),-28} {WordIndex.Coverage(tokens):P0}  {(all ? string.Join(' ', unknown) : unknown.Count + " unknown")}");
}

Console.WriteLine($"\n{unknownWords.Count} distinct unknown tokens; most frequent:");
foreach (var (word, count) in unknownWords.OrderByDescending(p => p.Value).Take(30))
{
    Console.WriteLine($"  {word} ×{count}");
}
