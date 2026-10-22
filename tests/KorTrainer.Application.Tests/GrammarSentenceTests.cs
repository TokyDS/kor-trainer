using AwesomeAssertions;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

/// <summary>The authored sentences must show their topic exactly as the Conjugator spells it, with deck words.</summary>
public sealed class GrammarSentenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void AuthoredSentences_HaveUniqueIdsAndKnownTopics()
    {
        var sentences = TestDatabase.GrammarSentences.Sentences;

        sentences.Should().HaveCountGreaterThanOrEqualTo(100);
        sentences.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        sentences.Select(s => s.Korean).Should().OnlyHaveUniqueItems();
        sentences.Should().OnlyContain(s => GrammarCatalog.Find(s.Topic) != null && s.Russian.Length > 0);
    }

    [Fact]
    public async Task EveryAuthoredSentence_YieldsAGapForItsTopic_AtItsStage()
    {
        await using var t = await TestDatabase.CreateAsync();
        var failures = new List<string>();

        foreach (var group in TestDatabase.GrammarSentences.Sentences.GroupBy(s => s.Stage))
        {
            var scope = StudyScopeOf(group.Key);
            var words = await t.Db.Words
                .Where(w => w.Stage <= scope)
                .Where(w => w.PartOfSpeech == PartOfSpeech.Noun || w.PartOfSpeech == PartOfSpeech.Verb || w.PartOfSpeech == PartOfSpeech.Adjective)
                .Select(w => new { w.Korean, w.Russian, w.PartOfSpeech, w.Irregularity })
                .ToListAsync(Ct);
            var nouns = words.Where(w => w.PartOfSpeech == PartOfSpeech.Noun).Select(w => new GrammarNoun(w.Korean, w.Russian)).ToList();
            var verbs = words.Where(w => w.PartOfSpeech != PartOfSpeech.Noun).Select(w => new GrammarVerb(w.Korean, w.Russian, w.PartOfSpeech, w.Irregularity)).ToList();

            foreach (var sentence in group)
            {
                var matches = GrammarClozeFinder.Find([new ClozeSentence(sentence.Id, sentence.Korean, sentence.Russian, false)], GrammarCatalog.Topics, nouns, verbs);
                if (!matches.Any(m => m.Topic.Type == sentence.Topic))
                {
                    failures.Add($"{sentence.Id} [{sentence.Topic}] {sentence.Korean} → {string.Join(", ", matches.Select(m => $"{m.Topic.Type}:{m.Form}"))}");
                }
            }
        }

        failures.Should().BeEmpty(string.Join(" | ", failures));
    }

    // The service draws words from at least stage 1 (StudyScope.MaxWordStage); test at the same scope.
    private static PlanStage StudyScopeOf(PlanStage stage) => stage < PlanStage.Level1 ? PlanStage.Level1 : stage;
}
