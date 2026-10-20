using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class ListeningExerciseGeneratorTests
{
    private static readonly ListeningWord[] Pool =
    [
        new("바다", "море"), new("학교", "школа"), new("사람", "человек"), new("시간", "время"),
        new("친구", "друг, подруга"), new("음식", "еда, блюдо"), new("가방", "сумка"), new("의자", "стул"),
    ];

    [Fact]
    public void SoundAlikes_ChangeOneAudibleSound()
    {
        var variants = ListeningExerciseGenerator.SoundAlikes("바다");

        variants.Should().Contain(["파다", "빠다", "버다"]);
        variants.Should().NotContain("바다");
        variants.Should().OnlyContain(v => Pronunciation.Pronounce(v).Text != Pronunciation.Pronounce("바다").Text);
    }

    [Fact]
    public void SoundAlikes_NeverSwapVowelsThatSoundTheSame()
    {
        ListeningExerciseGenerator.SoundAlikes("개").Should().NotContain("게");
    }

    [Theory]
    [InlineData(ExerciseTypes.ListenMeaning)]
    [InlineData(ExerciseTypes.ListenSpelling)]
    public void Generate_ProducesFourDistinctOptionsAndPlaysTheWord(string type)
    {
        var generator = new ListeningExerciseGenerator(new Random(3));

        var exercises = generator.Generate(type, 100, Pool, Pool);

        foreach (var exercise in exercises)
        {
            exercise.AutoSpeak.Should().BeTrue();
            exercise.Options.Should().HaveCount(ListeningExerciseGenerator.OptionCount);
            exercise.Options.Select(o => o.Text).Should().OnlyHaveUniqueItems();
            var word = Pool.Single(w => w.Korean == exercise.SpeakText);
            exercise.Correct.Text.Should().Be(type == ExerciseTypes.ListenMeaning
                ? GrammarExerciseGenerator.FirstSense(word.Russian)
                : word.Korean);
        }
    }

    [Fact]
    public void Generate_PlaysOnlyTheGivenTargets()
    {
        var generator = new ListeningExerciseGenerator(new Random(5));

        var exercises = generator.Generate(ExerciseTypes.ListenMeaning, 30, [Pool[0], Pool[1]], Pool);

        exercises.Select(e => e.SpeakText).Should().OnlyContain(w => w == "바다" || w == "학교");
    }

    [Fact]
    public void Generate_RejectsUnknownTypesAndTooSmallPools()
    {
        var generator = new ListeningExerciseGenerator(new Random(7));

        FluentActions.Invoking(() => generator.Generate("nope", 5, Pool, Pool)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => generator.Generate(ExerciseTypes.ListenMeaning, 5, Pool, Pool[..2])).Should().Throw<InvalidOperationException>();
    }
}
