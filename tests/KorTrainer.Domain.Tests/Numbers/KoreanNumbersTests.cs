using AwesomeAssertions;
using KorTrainer.Domain.Numbers;

namespace KorTrainer.Domain.Tests.Numbers;

public sealed class KoreanNumbersTests
{
    [Theory]
    [InlineData(0, "영")]
    [InlineData(1, "일")]
    [InlineData(10, "십")]
    [InlineData(11, "십일")]
    [InlineData(15, "십오")]
    [InlineData(20, "이십")]
    [InlineData(99, "구십구")]
    [InlineData(100, "백")]
    [InlineData(101, "백일")]
    [InlineData(110, "백십")]
    [InlineData(365, "삼백육십오")]
    [InlineData(1000, "천")]
    [InlineData(1987, "천구백팔십칠")]
    [InlineData(2027, "이천이십칠")]
    [InlineData(10000, "만")]
    [InlineData(15000, "만 오천")]
    [InlineData(25300, "이만 오천삼백")]
    [InlineData(100000, "십만")]
    [InlineData(1000000, "백만")]
    [InlineData(10010, "만 십")]
    [InlineData(100000000, "일억")]
    [InlineData(150000000, "일억 오천만")]
    public void Sino_OmitsLeadingOneAndGroupsByMan(long number, string expected) =>
        KoreanNumbers.Sino(number).Should().Be(expected);

    [Theory]
    [InlineData(1, false, "하나")]
    [InlineData(1, true, "한")]
    [InlineData(2, true, "두")]
    [InlineData(3, true, "세")]
    [InlineData(4, true, "네")]
    [InlineData(5, true, "다섯")]
    [InlineData(10, false, "열")]
    [InlineData(11, true, "열한")]
    [InlineData(20, false, "스물")]
    [InlineData(20, true, "스무")]
    [InlineData(21, true, "스물한")]
    [InlineData(34, false, "서른넷")]
    [InlineData(99, false, "아흔아홉")]
    public void Native_UsesAttributiveFormsBeforeCounters(int number, bool attributive, string expected) =>
        KoreanNumbers.Native(number, attributive).Should().Be(expected);

    [Fact]
    public void Native_RejectsNumbersOutsideOneToNinetyNine()
    {
        FluentActions.Invoking(() => KoreanNumbers.Native(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => KoreanNumbers.Native(100)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(3, 0, "세 시")]
    [InlineData(3, 30, "세 시 삼십 분")]
    [InlineData(12, 15, "열두 시 십오 분")]
    [InlineData(1, 5, "한 시 오 분")]
    public void Time_UsesNativeHoursAndSinoMinutes(int hour, int minute, string expected) =>
        Counters.Time(hour, minute).Should().Be(expected);

    [Theory]
    [InlineData(1000, "천 원")]
    [InlineData(10000, "만 원")]
    [InlineData(15000, "만 오천 원")]
    [InlineData(4500, "사천오백 원")]
    public void Money_ReadsSinoKoreanWon(long amount, string expected) =>
        Counters.Money(amount).Should().Be(expected);

    [Theory]
    [InlineData(6, "유월")]
    [InlineData(10, "시월")]
    [InlineData(3, "삼월")]
    [InlineData(12, "십이월")]
    public void Month_HandlesExceptions(int month, string expected) =>
        Counters.Month(month).Should().Be(expected);

    [Fact]
    public void Phrase_PicksSystemOfTheCounter()
    {
        Counters.Phrase(Counters.All.Single(c => c.Word == "개"), 3).Should().Be("사과 세 개");
        Counters.Phrase(Counters.All.Single(c => c.Word == "층"), 3).Should().Be("삼 층");
        Counters.Phrase(Counters.All.Single(c => c.Word == "명"), 20).Should().Be("학생 스무 명");
    }
}
