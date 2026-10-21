#:property PublishAot=false
#:project ../../src/KorTrainer.Domain/KorTrainer.Domain.csproj

// Lists what the Hangul module speaks outside the deck, for tools/tts/generate_melo.py --only hangul:
// letter sample syllables, minimal-pair words and pronunciation-rule examples. (Every single syllable is
// added by the Python script itself.) Run from the repository root:
//
//   dotnet run tools/tts/ExportHangulTexts.cs

using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Hangul;

var texts = JamoCatalog.All.Select(j => j.SampleSyllable)
    .Concat(MinimalPairs.Sets.SelectMany(set => set).Select(w => w.Korean))
    .Concat(HangulExerciseGenerator.SoundChangeExamples)
    .Distinct()
    .ToList();

string output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tts", "hangul-texts.txt");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllLines(output, texts);
Console.WriteLine($"{texts.Count} texts → {output}");
