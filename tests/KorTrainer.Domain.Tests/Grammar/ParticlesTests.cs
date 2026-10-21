using AwesomeAssertions;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Tests.Grammar;

public sealed class ParticlesTests
{
    [Theory]
    [InlineData("책", ParticleKind.Copula, "책이에요", "책예요")]
    [InlineData("의자", ParticleKind.Copula, "의자예요", "의자이에요")]
    [InlineData("학생", ParticleKind.NegativeCopula, "학생이 아니에요", "학생가 아니에요")]
    [InlineData("커피", ParticleKind.NegativeCopula, "커피가 아니에요", "커피이 아니에요")]
    [InlineData("선생님", ParticleKind.Topic, "선생님은", "선생님는")]
    [InlineData("저", ParticleKind.Topic, "저는", "저은")]
    [InlineData("친구", ParticleKind.Subject, "친구가", "친구이")]
    [InlineData("물", ParticleKind.Object, "물을", "물를")]
    [InlineData("사과", ParticleKind.Object, "사과를", "사과을")]
    [InlineData("빵", ParticleKind.With, "빵과", "빵와")]
    [InlineData("우유", ParticleKind.With, "우유와", "우유과")]
    [InlineData("버스", ParticleKind.Means, "버스로", "버스으로")]
    [InlineData("손", ParticleKind.Means, "손으로", "손로")]
    [InlineData("연필", ParticleKind.Means, "연필로", "연필으로")]
    public void Attach_ChoosesAllomorphByBatchim(string noun, ParticleKind kind, string expected, string wrong)
    {
        var result = Particles.Attach(noun, kind);

        result.Form.Should().Be(expected);
        result.Wrong.Should().Be(wrong);
    }

    [Fact]
    public void Attach_ExplainsWhichFinalConsonantDecided()
    {
        Particles.Attach("책", ParticleKind.Copula).Explanation.Should().Contain("ㄱ").And.Contain("이에요");
        Particles.Attach("의자", ParticleKind.Copula).Explanation.Should().Contain("нет патчима");
        Particles.Attach("연필", ParticleKind.Means).Explanation.Should().Contain("после ㄹ");
    }
}
