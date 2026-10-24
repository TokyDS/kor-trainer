#:property PublishAot=false
#:project ../../src/KorTrainer.Domain/KorTrainer.Domain.csproj

// Lists what the grammar drills speak, for tools/tts/generate_melo.py --only grammar: every verb and
// adjective of the first stages (about the first 800 words) in every ending the drills ask it in, and every
// noun of those stages in each particle question's phrase (책을 좋아해요). Wrong options are not voiced on
// purpose. Run from the repository root:
//
//   dotnet run tools/tts/ExportGrammarTexts.cs

using System.Text.Json;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Grammar;

string[] stages = ["FirstPhrases", "Level1"];
string seed = Path.Combine(Directory.GetCurrentDirectory(), "src", "KorTrainer.Infrastructure", "Seed");

var verbs = new List<GrammarVerb>();
var nouns = new List<string>();
foreach (string file in new[] { "words.json", "words-nikl.json" })
{
    using var deck = JsonDocument.Parse(File.ReadAllText(Path.Combine(seed, file)));
    foreach (var section in deck.RootElement.GetProperty("sections").EnumerateArray())
    {
        if (!stages.Contains(section.GetProperty("stage").GetString()))
        {
            continue;
        }

        foreach (var word in section.GetProperty("words").EnumerateArray())
        {
            string pos = word[2].GetString()!;
            if (pos == "noun")
            {
                nouns.Add(word[0].GetString()!.Trim());
            }

            if (pos is not ("verb" or "adjective"))
            {
                continue;
            }

            string irregular = word.GetArrayLength() > 3 ? word[3].GetString() ?? string.Empty : string.Empty;
            verbs.Add(new GrammarVerb(
                word[0].GetString()!.Trim(),
                word[1].GetString()!,
                pos == "verb" ? PartOfSpeech.Verb : PartOfSpeech.Adjective,
                irregular switch
                {
                    "b" => Irregularity.Bieup,
                    "d" => Irregularity.Digeut,
                    "reu" => Irregularity.Reu,
                    "s" => Irregularity.Siot,
                    "h" => Irregularity.Hieut,
                    _ => Irregularity.None,
                }));
        }
    }
}

var usable = verbs.Where(v => v.Korean.Length >= 2 && v.Korean[^1] == '다' && !v.Korean.Contains(' ')).DistinctBy(v => v.Korean).ToList();
var texts = GrammarCatalog.Topics
    .Where(t => t.Ending is not null)
    .SelectMany(t => usable.Where(v => GrammarExerciseGenerator.Suits(t, v)).Select(v => Conjugator.Conjugate(v.Korean, t.Ending!.Value, v.Irregularity).Form))
    .Concat(GrammarCatalog.Topics
        .Where(t => t.Particle is not null)
        .SelectMany(t => nouns.Where(n => n.Length > 0 && n.All(KorTrainer.Domain.Hangul.Syllable.IsSyllable))
            .Select(n => GrammarExerciseGenerator.ParticleSpeakText(n, t.Particle!.Value))))
    // The example sentences of the rules, read before each drill.
    .Concat(GrammarTheories.All.SelectMany(t => t.Examples).Select(e => e.Sentence))
    // The lines of the lesson scenes.
    .Concat(KorTrainer.Domain.Plan.LessonPractices.All.SelectMany(p => p.Lines).Select(l => l.Korean))
    // The tables and examples of the number rules.
    .Concat(KorTrainer.Domain.Numbers.NumberTheories.All.SelectMany(t => t.Table.Concat(t.Examples)).Select(s => s.Korean))
    .Distinct()
    .ToList();

string output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tts", "grammar-texts.txt");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllLines(output, texts);
Console.WriteLine($"{usable.Count} verbs, {nouns.Distinct().Count()} nouns → {texts.Count} texts → {output}");
