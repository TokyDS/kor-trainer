using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Domain.Tests.Vocabulary;

public sealed class HanjaTests
{
    private static readonly IReadOnlyDictionary<char, HanjaGloss> Glosses = new Dictionary<char, HanjaGloss>
    {
        ['學'] = new("배울", "учёба"),
        ['校'] = new("학교", "школа"),
        ['生'] = new("날", "жизнь"),
        ['大'] = new("클", "большой"),
        ['始'] = new("비로소", "начало"),
        ['作'] = new("지을", "делать"),
        ['番'] = new("차례", "очередь"),
        ['國'] = new("나라", "страна"),
        ['人'] = new("사람", "человек"),
    };

    [Theory]
    [InlineData("학교", PartOfSpeech.Noun, "學校", "학學 교校")]
    [InlineData("시작하다", PartOfSpeech.Verb, "始作-", "시始 작作")]
    [InlineData("이번", PartOfSpeech.Noun, "-番", "번番")]
    [InlineData("정말", PartOfSpeech.Adverb, "正-", "정正")]
    public void Align_PairsSyllablesWithCharacters(string korean, PartOfSpeech pos, string hanja, string expected) =>
        string.Join(' ', Hanja.Align(korean, pos, hanja).Select(p => $"{p.Syllable}{p.Character}")).Should().Be(expected);

    [Theory]
    [InlineData("학교", "學")]
    [InlineData("베이징", "北京")]
    [InlineData("학교", "")]
    public void Align_RejectsMismatchedOrMissingHanja(string korean, string hanja) =>
        Hanja.Align(korean, PartOfSpeech.Noun, hanja).Should().BeEmpty();

    [Fact]
    public void Normalize_MapsCompatibilityIdeographs() =>
        Hanja.Normalize("\uF981子").Should().Be("女子");

    [Fact]
    public void Roots_GroupWordsBySharedCharacter()
    {
        var words = Words();

        var roots = Hanja.Roots(words, Glosses);

        var learning = roots.Single(r => r.Character == '學');
        learning.Reading.Should().Be('학');
        learning.Words.Select(w => w.Korean).Should().Equal("학교", "학생", "대학");
        learning.Gloss!.Russian.Should().Be("учёба");
        roots.Select(r => r.Words.Count).Should().BeInDescendingOrder();
    }

    [Fact]
    public void RootDrill_AsksForTheCommonRootOfSeveralWords()
    {
        var items = new HanjaExerciseGenerator(new Random(7)).Generate(ExerciseTypes.HanjaRoot, 5, Words(), Glosses);

        items.Should().NotBeEmpty();
        foreach (var item in items)
        {
            item.Options.Should().HaveCount(4).And.OnlyHaveUniqueItems(o => o.Text);
            item.Correct.Hint.Should().NotBeNullOrEmpty();
            item.Prompt.Split(", ").Should().HaveCountGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public void MeaningDrill_ShowsTheRootsAsTheHint()
    {
        var items = new HanjaExerciseGenerator(new Random(3)).Generate(ExerciseTypes.HanjaMeaning, 10, Words(), Glosses);

        var item = items.First(i => i.Prompt == "대학");
        item.PromptNote.Should().Contain("大").And.Contain("большой").And.Contain("學");
        item.Correct.Text.Should().Be("университет");
        item.Options.Should().HaveCount(4);
    }

    private static List<HanjaWord> Words() =>
    [
        new("학교#1", "학교", "школа", PartOfSpeech.Noun, "學校", 1),
        new("학생#1", "학생", "студент", PartOfSpeech.Noun, "學生", 2),
        new("대학#1", "대학", "университет", PartOfSpeech.Noun, "大學", 3),
        new("외국인#1", "외국인", "иностранец", PartOfSpeech.Noun, "外國人", 4),
        new("한국#1", "한국", "Корея", PartOfSpeech.Noun, "韓國", 5),
        new("중국#1", "중국", "Китай", PartOfSpeech.Noun, "中國", 6),
        new("인생#1", "인생", "жизнь человека", PartOfSpeech.Noun, "人生", 7),
        new("대인#1", "대인", "взрослый", PartOfSpeech.Noun, "大人", 8),
        new("시작하다#1", "시작하다", "начинать", PartOfSpeech.Verb, "始作-", 9),
        new("생일#1", "생일", "день рождения", PartOfSpeech.Noun, "生日", 10),
    ];
}
