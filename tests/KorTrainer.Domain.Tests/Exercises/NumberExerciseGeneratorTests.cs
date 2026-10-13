using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Numbers;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class NumberExerciseGeneratorTests
{
    private const int SampleSize = 300;

    public static TheoryData<string> AllTypes => new(NumberExerciseGenerator.Types);

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Generate_ProducesWellFormedQuestions(string type)
    {
        var generator = new NumberExerciseGenerator(new Random(8));

        var exercises = generator.Generate([type], SampleSize);

        exercises.Should().HaveCount(SampleSize);
        foreach (var exercise in exercises)
        {
            exercise.Type.Should().Be(type);
            exercise.Options.Count.Should().BeGreaterThanOrEqualTo(3);
            exercise.Options.Select(o => o.Text).Should().OnlyHaveUniqueItems();
            exercise.Explanation.Should().NotBeNullOrWhiteSpace();
            exercise.SpeakText.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Sino_ToWords_OffersTheExplicitOneMistake()
    {
        var exercise = new NumberExerciseGenerator(new Random(1)).Sino(15, toWords: true);

        exercise.Correct.Text.Should().Be("십오");
        exercise.Options.Select(o => o.Text).Should().Contain("일십오");
        exercise.Explanation.Should().Contain("10 + 5");
    }

    [Fact]
    public void Sino_ToDigits_AnswersWithTheNumber()
    {
        var exercise = new NumberExerciseGenerator(new Random(2)).Sino(1987, toWords: false);

        exercise.Prompt.Should().Be("천구백팔십칠");
        exercise.Correct.Text.Should().Be("1987");
    }

    [Fact]
    public void Native_ToWords_OffersTheSinoNumberAsDistractor()
    {
        var exercise = new NumberExerciseGenerator(new Random(3)).Native(3, toWords: true);

        exercise.Correct.Text.Should().Be("셋");
        exercise.Options.Select(o => o.Text).Should().Contain("삼");
    }

    [Fact]
    public void Counter_ForNativeCounter_UsesAttributiveFormAndOffersMistakes()
    {
        var counter = Counters.All.Single(c => c.Word == "개");

        var exercise = new NumberExerciseGenerator(new Random(4)).Counter(counter, 3);

        exercise.Correct.Text.Should().Be("사과 세 개");
        exercise.Options.Select(o => o.Text).Should().Contain(["사과 삼 개", "사과 셋 개"]);
        exercise.Explanation.Should().Contain("셋 → 세");
    }

    [Fact]
    public void Time_ToWords_OffersWrongSystemsAsDistractors()
    {
        var exercise = new NumberExerciseGenerator(new Random(5)).Time(3, 30, toWords: true);

        exercise.Correct.Text.Should().Be("세 시 삼십 분");
        exercise.Options.Select(o => o.Text).Should().Contain(["삼 시 삼십 분", "세 시 서른 분"]);
    }

    [Theory]
    [InlineData(5, 0, true)]
    [InlineData(7, 0, false)]
    [InlineData(10, 30, false)]
    public void Time_OnTheHourAndHalf_StillHasFourOptions(int hour, int minute, bool toWords)
    {
        var exercise = new NumberExerciseGenerator(new Random(9)).Time(hour, minute, toWords);

        exercise.Options.Should().HaveCount(NumberExerciseGenerator.OptionCount);
    }

    [Fact]
    public void Money_ToWords_OffersIlmanMistake()
    {
        var exercise = new NumberExerciseGenerator(new Random(6)).Money(15000, toWords: true);

        exercise.Prompt.Should().Be("15,000원");
        exercise.Correct.Text.Should().Be("만 오천 원");
        exercise.Options.Select(o => o.Text).Should().Contain("일만 오천 원");
    }

    [Fact]
    public void Generate_RejectsUnknownTypes()
    {
        var act = () => new NumberExerciseGenerator(new Random(7)).Generate(["sino", "nope"], 5);

        act.Should().Throw<ArgumentException>();
    }
}
