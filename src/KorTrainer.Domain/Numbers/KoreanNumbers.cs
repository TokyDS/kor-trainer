using System.Text;

namespace KorTrainer.Domain.Numbers;

/// <summary>
/// Spells numbers in both Korean systems. Sino-Korean groups digits by 만 (10⁴) and 억 (10⁸) and
/// separates groups with a space (만 오천); native numbers exist up to 99.
/// </summary>
public static class KoreanNumbers
{
    public const int MaxNative = 99;
    public const long MaxSino = 9_999_999_999_999;

    private const string SinoDigits = "영일이삼사오육칠팔구";
    private static readonly string[] SmallUnits = [string.Empty, "십", "백", "천"];
    private static readonly string[] GroupUnits = [string.Empty, "만", "억", "조"];
    private static readonly string[] NativeOnes = [string.Empty, "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱", "여덟", "아홉"];
    private static readonly string[] NativeTens = [string.Empty, "열", "스물", "서른", "마흔", "쉰", "예순", "일흔", "여든", "아흔"];

    public static string Sino(long number)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxSino);
        if (number == 0)
        {
            return "영";
        }

        var groups = new List<string>();
        for (int g = 0; number > 0; g++, number /= 10_000)
        {
            int group = (int)(number % 10_000);
            if (group == 0)
            {
                continue;
            }

            // 만 alone, not 일만; but 일억, 일조 keep the 1.
            string words = group == 1 && g == 1 ? string.Empty : SinoGroup(group);
            groups.Insert(0, words + GroupUnits[g]);
        }

        return string.Join(' ', groups);
    }

    /// <summary>Native Korean numbers 1–99; <paramref name="attributive"/> gives the form before a counter (한, 두, 세, 네, 스무).</summary>
    public static string Native(int number, bool attributive = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNative);

        int tens = number / 10;
        int ones = number % 10;
        if (attributive && ones == 0 && tens == 2)
        {
            return "스무";
        }

        string onesWord = attributive ? Attributive(ones) : NativeOnes[ones];
        return NativeTens[tens] + onesWord;
    }

    private static string Attributive(int ones) => ones switch
    {
        1 => "한",
        2 => "두",
        3 => "세",
        4 => "네",
        _ => NativeOnes[ones],
    };

    private static string SinoGroup(int group)
    {
        var builder = new StringBuilder();
        for (int position = 3; position >= 0; position--)
        {
            int digit = group / (int)Math.Pow(10, position) % 10;
            if (digit == 0)
            {
                continue;
            }

            // 십, 백, 천 without a leading 일.
            if (digit != 1 || position == 0)
            {
                builder.Append(SinoDigits[digit]);
            }

            builder.Append(SmallUnits[position]);
        }

        return builder.ToString();
    }
}
