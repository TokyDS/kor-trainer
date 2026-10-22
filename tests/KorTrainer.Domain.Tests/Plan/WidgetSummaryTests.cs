using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class WidgetSummaryTests
{
    [Theory]
    [InlineData(1, "слово")]
    [InlineData(2, "слова")]
    [InlineData(4, "слова")]
    [InlineData(5, "слов")]
    [InlineData(11, "слов")]
    [InlineData(12, "слов")]
    [InlineData(14, "слов")]
    [InlineData(21, "слово")]
    [InlineData(22, "слова")]
    [InlineData(100, "слов")]
    [InlineData(0, "слов")]
    public void RussianPlural_PicksTheFormByTheLastDigits(int count, string expected) =>
        RussianPlural.Of(count, "слово", "слова", "слов").Should().Be(expected);

    [Theory]
    [InlineData(12, 3, false, "К повторению: 12 слов · 3 ошибки")]
    [InlineData(1, 0, false, "К повторению: 1 слово")]
    [InlineData(0, 5, true, "К повторению: 5 ошибок")]
    [InlineData(0, 0, true, "На сегодня всё повторено")]
    [InlineData(0, 0, false, "Повторять нечего — можно взять новые слова")]
    public void Detail_NamesWhatIsDue(int words, int mistakes, bool studiedToday, string expected) =>
        WidgetSummary.Detail(words, mistakes, studiedToday).Should().Be(expected);
}
