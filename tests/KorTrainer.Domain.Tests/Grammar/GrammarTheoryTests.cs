using AwesomeAssertions;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Tests.Grammar;

public sealed class GrammarTheoryTests
{
    public static TheoryData<string> TopicTypes => new(GrammarCatalog.Topics.Select(t => t.Type));

    [Theory]
    [MemberData(nameof(TopicTypes))]
    public void EveryTopic_HasTheoryWithStepsAndExamples(string type)
    {
        var theory = GrammarTheories.Find(type);

        theory.Should().NotBeNull();
        theory!.Meaning.Should().NotBeNullOrWhiteSpace();
        theory.Steps.Should().NotBeEmpty();
        theory.Examples.Should().HaveCountGreaterThanOrEqualTo(3);
        theory.Pitfall.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(TopicTypes))]
    public void Examples_AreTheFormsTheDrillsTeach(string type)
    {
        var topic = GrammarCatalog.Find(type)!;

        foreach (var example in GrammarTheories.Find(type)!.Examples)
        {
            string form = topic.Particle is { } particle
                ? Particles.Attach(example.Word, particle).Form
                : Conjugator.Conjugate(example.Word, topic.Ending!.Value, example.Irregularity).Form;

            example.Form.Should().Be(form, $"{example.Word} in {type}");
            example.Sentence.Should().Contain(example.Form);
            example.Translation.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Theories_BelongToCatalogTopics()
    {
        GrammarTheories.All.Select(t => t.Type).Should().OnlyHaveUniqueItems()
            .And.BeSubsetOf(GrammarCatalog.Topics.Select(t => t.Type));
    }
}
