using System.Text.RegularExpressions;
using AwesomeAssertions;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed partial class LessonPracticeTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void PhraseLessons_HaveAScene(int day)
    {
        var practice = LessonPractices.Find(day);

        practice.Should().NotBeNull();
        practice!.Setting.Should().NotBeNullOrWhiteSpace();
        practice.Lines.Should().HaveCountGreaterThanOrEqualTo(6)
            .And.OnlyContain(l => l.Korean.Length > 0 && l.Russian.Length > 0 && l.Speaker.Length > 0);
    }

    [Fact]
    public void HangulLessons_HaveNoScene() => LessonPractices.Find(1).Should().BeNull();

    [Fact]
    public void Lines_UseTheParticlesTheLessonsTeach()
    {
        var lines = LessonPractices.All.SelectMany(p => p.Lines).Select(l => l.Korean).ToList();
        int checkedForms = 0;

        foreach (string line in lines)
        {
            foreach (Match m in Copula().Matches(line))
            {
                (m.Groups[1].Value + m.Groups[2].Value).Should().Be(Particles.Attach(m.Groups[1].Value, ParticleKind.Copula).Form, line);
                checkedForms++;
            }

            foreach (Match m in NegativeCopula().Matches(line))
            {
                m.Value.Should().Be(Particles.Attach(m.Groups[1].Value, ParticleKind.NegativeCopula).Form, line);
                checkedForms++;
            }

            foreach (Match m in Topic().Matches(line))
            {
                (m.Groups[1].Value + m.Groups[2].Value).Should().Be(Particles.Attach(m.Groups[1].Value, ParticleKind.Topic).Form, line);
                checkedForms++;
            }
        }

        checkedForms.Should().BeGreaterThan(30);
    }

    [GeneratedRegex(@"([가-힣]+?)(이에요|예요)(?=[.?!])")]
    private static partial Regex Copula();

    [GeneratedRegex(@"([가-힣]+)(이|가) 아니에요")]
    private static partial Regex NegativeCopula();

    [GeneratedRegex(@"(저|책)(은|는)(?= )")]
    private static partial Regex Topic();
}
