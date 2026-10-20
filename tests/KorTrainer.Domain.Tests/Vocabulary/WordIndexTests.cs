using AwesomeAssertions;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Domain.Tests.Vocabulary;

public sealed class WordIndexTests
{
    private static readonly WordIndex Index = new(
    [
        Word("학교#1", "학교", PartOfSpeech.Noun, 1),
        Word("가방#1", "가방", PartOfSpeech.Noun, 2),
        Word("가다#1", "가다", PartOfSpeech.Verb, 3),
        Word("먹다#1", "먹다", PartOfSpeech.Verb, 4),
        Word("하다#1", "하다", PartOfSpeech.Verb, 5),
        Word("공부하다#1", "공부하다", PartOfSpeech.Verb, 6),
        Word("듣다#1", "듣다", PartOfSpeech.Verb, 7, Irregularity.Digeut),
        Word("덥다#1", "덥다", PartOfSpeech.Adjective, 8, Irregularity.Bieup),
        Word("모르다#1", "모르다", PartOfSpeech.Verb, 9, Irregularity.Reu),
        Word("만들다#1", "만들다", PartOfSpeech.Verb, 10),
        Word("보다#1", "보다", PartOfSpeech.Verb, 11),
        Word("눈#1", "눈", PartOfSpeech.Noun, 12),
        Word("눈#2", "눈", PartOfSpeech.Noun, 40),
        Word("시#1", "시", PartOfSpeech.Counter, 13),
        Word("안녕히 가세요#1", "안녕히 가세요", PartOfSpeech.Phrase, 14),
        Word("좋다#1", "좋다", PartOfSpeech.Adjective, 15),
    ]);

    [Theory]
    [InlineData("학교에서", "학교#1")]
    [InlineData("가방을", "가방#1")]
    [InlineData("가요", "가다#1")]
    [InlineData("갔어요", "가다#1")]
    [InlineData("갑니다", "가다#1")]
    [InlineData("먹었습니다", "먹다#1")]
    [InlineData("먹는", "먹다#1")]
    [InlineData("먹은", "먹다#1")]
    [InlineData("먹을", "먹다#1")]
    [InlineData("먹으니까", "먹다#1")]
    [InlineData("해요", "하다#1")]
    [InlineData("했어요", "하다#1")]
    [InlineData("공부했어요", "공부하다#1")]
    [InlineData("들어요", "듣다#1")]
    [InlineData("들었어요", "듣다#1")]
    [InlineData("더워요", "덥다#1")]
    [InlineData("더운", "덥다#1")]
    [InlineData("몰라요", "모르다#1")]
    [InlineData("만드는", "만들다#1")]
    [InlineData("봐요", "보다#1")]
    [InlineData("좋아해요", "좋다#1")]
    public void Lookup_FindsTheDictionaryWordOfAnInflectedForm(string token, string key) =>
        Index.Lookup(token).Select(w => w.Key).Should().Contain(key);

    [Fact]
    public void Lookup_PrefersTheLongestMatch() =>
        Index.Lookup("가방에").Select(w => w.Key).Should().Equal("가방#1");

    [Fact]
    public void Lookup_ReturnsHomonymsByFrequency() =>
        Index.Lookup("눈이").Select(w => w.Key).Should().Equal("눈#1", "눈#2");

    [Fact]
    public void Lookup_UnknownToken_IsEmpty() =>
        Index.Lookup("컴퓨터").Should().BeEmpty();

    [Theory]
    [InlineData("가수")]
    [InlineData("보리")]
    [InlineData("시장")]
    public void Lookup_DoesNotLinkUnknownWordsToAOneSyllableStem(string token) =>
        Index.Lookup(token).Should().BeEmpty();

    [Theory]
    [InlineData("가서", "가다#1")]
    [InlineData("가고", "가다#1")]
    [InlineData("보면", "보다#1")]
    [InlineData("시에", "시#1")]
    [InlineData("눈도", "눈#1")]
    public void Lookup_OneSyllableStem_WithAParticleOrEnding_IsStillFound(string token, string key) =>
        Index.Lookup(token).Select(w => w.Key).Should().Contain(key);

    [Fact]
    public void Analyze_SplitsPunctuationDigitsAndSpaces()
    {
        var tokens = Index.Analyze("3시에 학교에 가요!");

        tokens.Select(t => t.Text).Should().Equal("3시에", "학교에", "가요!");
        tokens.Select(t => t.Words.FirstOrDefault()?.Key).Should().Equal("시#1", "학교#1", "가다#1");
        tokens[0].Hangul.Should().Be("시에");
    }

    [Fact]
    public void Analyze_MatchesMultiWordPhrases()
    {
        var tokens = Index.Analyze("안녕히 가세요.");

        tokens.Should().ContainSingle().Which.Words.Select(w => w.Key).Should().Equal("안녕히 가세요#1");
    }

    [Fact]
    public void Coverage_CountsRecognizedTokens()
    {
        var tokens = Index.Analyze("학교에 컴퓨터가 있어요");

        WordIndex.Coverage(tokens).Should().BeApproximately(1 / 3.0, 0.001);
    }

    [Fact]
    public void Forms_OfAVerb_IncludeTheMainStems() =>
        WordIndex.Forms("가다", PartOfSpeech.Verb, Irregularity.None)
            .Should().Contain(["가", "갔", "갑니다", "간", "갈", "가는"]);

    private static IndexedWord Word(string key, string korean, PartOfSpeech pos, int rank, Irregularity irregularity = Irregularity.None) =>
        new(key, korean, pos, irregularity, rank);
}
