using System.Collections.Concurrent;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Grammar;

/// <summary>
/// Sentence gaps for every grammar topic, per word scope. The deck and the sentences are bundled and never
/// change at run time, and finding the gaps takes a noticeable fraction of a second on a phone.
/// </summary>
public sealed class GrammarClozeCache
{
    private readonly ConcurrentDictionary<PlanStage, Lazy<IReadOnlyList<ClozeMatch>>> _byScope = new();

    public IReadOnlyList<ClozeMatch> GetOrBuild(PlanStage wordScope, Func<IReadOnlyList<ClozeMatch>> build) =>
        _byScope.GetOrAdd(wordScope, _ => new Lazy<IReadOnlyList<ClozeMatch>>(build)).Value;
}
