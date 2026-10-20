using AwesomeAssertions;
using KorTrainer.Domain.Typing;

namespace KorTrainer.Domain.Tests.Typing;

public sealed class DubeolsikTests
{
    [Theory]
    [InlineData("ㅎㅏㄴㄱㅡㄹ", "한글")]
    [InlineData("ㅇㅏㄴㄴㅕㅇ", "안녕")]
    [InlineData("ㄷㅏㄹㄱ", "닭")]
    [InlineData("ㄷㅏㄹㄱㅣ", "달기")]
    [InlineData("ㄷㅏㄹㄱㅇㅣ", "닭이")]
    [InlineData("ㅇㅗㅏ", "와")]
    [InlineData("ㄱㅗㅏㅇ", "광")]
    [InlineData("ㅇㅜㅓㄴ", "원")]
    [InlineData("ㅇㅡㅣㅅㅏ", "의사")]
    [InlineData("ㅂㅏㅂㅅ", "밦")]
    [InlineData("ㄱ", "ㄱ")]
    [InlineData("ㄱㄱ", "ㄱㄱ")]
    [InlineData("ㅏ", "ㅏ")]
    [InlineData("ㅏㅏ", "ㅏㅏ")]
    [InlineData("ㄲㅗㅊ", "꽃")]
    public void Composer_BuildsSyllablesLikeAKoreanKeyboard(string jamo, string expected)
    {
        var composer = new HangulComposer();

        foreach (char c in jamo)
        {
            composer.Type(c);
        }

        composer.Text.Should().Be(expected);
    }

    [Fact]
    public void Backspace_UndoesOneKeystroke()
    {
        var composer = new HangulComposer();
        foreach (char c in "ㅎㅏㄴㄱㅗㅏ")
        {
            composer.Type(c);
        }

        var steps = new List<string> { composer.Text };
        for (int i = 0; i < 6; i++)
        {
            composer.Backspace();
            steps.Add(composer.Text);
        }

        steps.Should().Equal("한과", "한고", "한ㄱ", "한", "하", "ㅎ", "");
    }

    [Fact]
    public void Reset_KeepsTextTypedOutside()
    {
        var composer = new HangulComposer();
        composer.Reset("안녕");
        composer.Type('ㅎ');
        composer.Type('ㅏ');

        composer.Text.Should().Be("안녕하");
    }

    [Theory]
    [InlineData("KeyG", false, 'ㅎ')]
    [InlineData("KeyK", false, 'ㅏ')]
    [InlineData("KeyR", true, 'ㄲ')]
    [InlineData("KeyO", true, 'ㅒ')]
    [InlineData("KeyK", true, 'ㅏ')]
    public void Layout_MapsPhysicalKeys(string code, bool shift, char jamo) =>
        Dubeolsik.JamoFor(code, shift).Should().Be(jamo);

    [Fact]
    public void Layout_IgnoresOtherKeys() =>
        Dubeolsik.JamoFor("Digit1", false).Should().BeNull();

    [Fact]
    public void Keystrokes_SplitCompoundVowelsAndFinals() =>
        Dubeolsik.Keystrokes("왜 닭").Should().Equal('ㅇ', 'ㅗ', 'ㅐ', ' ', 'ㄷ', 'ㅏ', 'ㄹ', 'ㄱ');

    [Theory]
    [InlineData("안녕", "", 'ㅇ')]
    [InlineData("안녕", "안", 'ㄴ')]
    [InlineData("안녕", "안ㄴ", 'ㅕ')]
    [InlineData("안녕", "안녀", 'ㅇ')]
    [InlineData("달기", "닭", 'ㅣ')]
    [InlineData("안녕", "앙", null)]
    [InlineData("안녕", "안녕", null)]
    public void NextKey_FollowsTheTypedPrefix(string target, string typed, char? expected) =>
        Dubeolsik.NextJamo(target, typed).Should().Be(expected);

    [Fact]
    public void Key_OfAJamo_IncludesShift()
    {
        Dubeolsik.KeyFor('ㅆ').Should().Be(new DubeolsikKey("KeyT", true));
        Dubeolsik.KeyFor('ㅅ').Should().Be(new DubeolsikKey("KeyT", false));
    }

    [Fact]
    public void Lessons_UseOnlyTheirKeys()
    {
        var home = TypingLessons.Generate(1, 30, new Random(1));

        home.Should().HaveCount(30);
        var keys = TypingLessons.Lessons[0].Jamo.ToHashSet();
        home.SelectMany(Dubeolsik.Keystrokes).Where(c => c != ' ').Should().OnlyContain(c => keys.Contains(c));
    }
}
