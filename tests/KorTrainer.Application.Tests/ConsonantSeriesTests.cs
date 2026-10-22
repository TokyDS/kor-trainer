using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Application.Tests;

public sealed class ConsonantSeriesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Focus_KeepsTheDrillInOneRow()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await t.Hangul.GetExercisesAsync(ExerciseTypes.ConsonantSeries, 3, 20, Ct, focus: "ㄷ");

        set.Items.Should().HaveCount(20).And.OnlyContain(i => i.Type == ExerciseTypes.ConsonantSeries);
        set.Items.SelectMany(i => i.Options)
            .Should().OnlyContain(o => "ㄷㅌㄸ".Contains(Syllable.Decompose(o.Text[0]).Initial));
    }

    [Theory]
    [InlineData(ExerciseTypes.ConsonantSeries, "ㄴ")]
    [InlineData(ExerciseTypes.ConsonantSeries, "다")]
    [InlineData(ExerciseTypes.LetterToSound, "ㄷ")]
    public async Task Focus_IsOnlyForSeriesConsonants(string type, string focus)
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Hangul.GetExercisesAsync(type, 3, 10, Ct, focus);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }
}
