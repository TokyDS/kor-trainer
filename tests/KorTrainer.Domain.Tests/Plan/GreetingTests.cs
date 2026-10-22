using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class GreetingTests
{
    [Theory]
    [InlineData(8, 0, 2, false, GreetingMood.Thinking, "Доброе утро! 2 ошибки ждут повторения — разберём вместе?")]
    [InlineData(14, 1, 0, false, GreetingMood.Hello, "Привет! 1 слово ждёт повторения.")]
    [InlineData(19, 12, 0, true, GreetingMood.Hello, "Добрый вечер! 12 слов ждут повторения.")]
    [InlineData(20, 0, 0, true, GreetingMood.Happy, "Сегодня всё повторено. Можно взять новые слова или просто отдохнуть.")]
    [InlineData(10, 0, 0, false, GreetingMood.Hello, "Доброе утро! Повторять пока нечего — можно начать с «10 минут».")]
    public void For_NamesWhatIsDue(int hour, int words, int mistakes, bool studied, GreetingMood mood, string text) =>
        Greetings.For(hour, words, mistakes, studied).Should().Be(new Greeting(mood, text));

    [Theory]
    [InlineData(23)]
    [InlineData(2)]
    public void For_IsSleepyAtNight_AndDoesNotPush(int hour)
    {
        var greeting = Greetings.For(hour, dueWords: 30, dueMistakes: 3, studiedToday: false);

        greeting.Mood.Should().Be(GreetingMood.Sleepy);
        greeting.Text.Should().NotContain("ошиб").And.NotContain("слов ждут");
    }

    [Fact]
    public void For_NeverTalksAboutStreaksOrBeingBehind()
    {
        var lines = Enumerable.Range(0, 24)
            .SelectMany(h => new[] { Greetings.For(h, 0, 0, false), Greetings.For(h, 5, 1, true) })
            .Select(g => g.Text.ToLowerInvariant());

        lines.Should().NotContain(l => l.Contains("подряд") || l.Contains("отста") || l.Contains("пропуст") || l.Contains("серия"));
    }
}
