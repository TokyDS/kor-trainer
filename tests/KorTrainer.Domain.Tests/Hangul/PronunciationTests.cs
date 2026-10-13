using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class PronunciationTests
{
    [Theory]
    [InlineData('ㄱ', 'ㄱ')]
    [InlineData('ㄲ', 'ㄱ')]
    [InlineData('ㅋ', 'ㄱ')]
    [InlineData('ㅅ', 'ㄷ')]
    [InlineData('ㅆ', 'ㄷ')]
    [InlineData('ㅈ', 'ㄷ')]
    [InlineData('ㅊ', 'ㄷ')]
    [InlineData('ㅌ', 'ㄷ')]
    [InlineData('ㅎ', 'ㄷ')]
    [InlineData('ㅍ', 'ㅂ')]
    [InlineData('ㄺ', 'ㄱ')]
    [InlineData('ㄻ', 'ㅁ')]
    [InlineData('ㄼ', 'ㄹ')]
    [InlineData('ㅄ', 'ㅂ')]
    [InlineData('ㄶ', 'ㄴ')]
    public void RepresentativeSound_MapsFinalsToSevenSounds(char final, char expected) =>
        Pronunciation.RepresentativeSound(final).Should().Be(expected);

    [Fact]
    public void RepresentativeSound_CoversAll27FinalsWithOnly7Sounds()
    {
        var sounds = HangulAlphabet.Finals.Select(Pronunciation.RepresentativeSound).Distinct();

        sounds.Should().BeEquivalentTo(Pronunciation.RepresentativeSounds.ToCharArray());
    }

    [Theory]
    [InlineData("한국어", "한구거")]
    [InlineData("음악", "으막")]
    [InlineData("읽어요", "일거요")]
    [InlineData("있어요", "이써요")]
    [InlineData("없어요", "업써요")]
    [InlineData("앉아요", "안자요")]
    [InlineData("많아요", "마나요")]
    [InlineData("좋아요", "조아요")]
    [InlineData("강아지", "강아지")]
    public void Pronounce_AppliesLinking(string spelling, string expected) =>
        Pronunciation.Pronounce(spelling).Text.Should().Be(expected);

    [Theory]
    [InlineData("꽃", "꼳")]
    [InlineData("밖", "박")]
    [InlineData("부엌", "부억")]
    [InlineData("앞", "압")]
    [InlineData("닭", "닥")]
    public void Pronounce_NeutralizesWordFinalConsonants(string spelling, string expected) =>
        Pronunciation.Pronounce(spelling).Text.Should().Be(expected);

    [Theory]
    [InlineData("감사합니다", "감사함니다")]
    [InlineData("국물", "궁물")]
    [InlineData("먹는", "멍는")]
    [InlineData("학교", "학꾜")]
    [InlineData("책상", "책쌍")]
    [InlineData("읽다", "익따")]
    [InlineData("신라", "실라")]
    [InlineData("설날", "설랄")]
    [InlineData("종로", "종노")]
    [InlineData("국립", "궁닙")]
    [InlineData("축하", "추카")]
    [InlineData("입학", "이팍")]
    [InlineData("못해요", "모태요")]
    [InlineData("좋다", "조타")]
    [InlineData("좋습니다", "조씀니다")]
    [InlineData("같이", "가치")]
    [InlineData("맛있어요", "마시써요")]
    public void Pronounce_AppliesAssimilationRules(string spelling, string expected) =>
        Pronunciation.Pronounce(spelling).Text.Should().Be(expected);

    [Fact]
    public void Pronounce_DoesNotLinkAcrossSpacesAndKeepsNonHangul()
    {
        Pronunciation.Pronounce("책 이에요!").Text.Should().Be("책 이에요!");
    }

    [Fact]
    public void Pronounce_ReportsAppliedRules()
    {
        var result = Pronunciation.Pronounce("한국어");

        result.IsChanged.Should().BeTrue();
        result.Changes.Should().ContainSingle().Which.Rule.Should().Be(SoundRule.Linking);
    }

    [Fact]
    public void Pronounce_ReportsNoChangesForPlainWords()
    {
        Pronunciation.Pronounce("나무").IsChanged.Should().BeFalse();
    }
}
