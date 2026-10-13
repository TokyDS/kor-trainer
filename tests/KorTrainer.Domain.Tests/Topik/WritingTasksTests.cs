using AwesomeAssertions;
using KorTrainer.Domain.Topik;

namespace KorTrainer.Domain.Tests.Topik;

public sealed class WritingTasksTests
{
    [Fact]
    public void All_CoversTasks51To54WithinTheFiftyMinuteSection()
    {
        WritingTasks.All.Select(t => t.Number).Should().Equal(51, 52, 53, 54);
        WritingTasks.All.Sum(t => t.RecommendedMinutes).Should().Be(WritingTasks.SectionMinutes);
        WritingTasks.Find(53)!.MinChars.Should().Be(200);
        WritingTasks.Find(54)!.MaxChars.Should().Be(700);
    }

    [Fact]
    public void EveryTask_HasTipsAndTemplatePhrases()
    {
        WritingTasks.All.Should().OnlyContain(t => t.Tips.Count > 0 && t.Template.Count > 0);
    }

    [Theory]
    [InlineData("최근 관심이 높다.", 10)]
    [InlineData("첫째,\n둘째", 5)]
    [InlineData("", 0)]
    public void CountCharacters_CountsSpacesButNotLineBreaks(string text, int expected) =>
        WritingTasks.CountCharacters(text).Should().Be(expected);
}
