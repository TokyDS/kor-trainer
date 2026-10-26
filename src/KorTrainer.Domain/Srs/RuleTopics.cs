using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Numbers;

namespace KorTrainer.Domain.Srs;

/// <summary>A rule that comes back for spaced checks: a grammar topic or a number topic.</summary>
public sealed record RuleTopic(string Type, string Title, StudyModule Module);

/// <summary>Every rule with a theory page, grammar first. Their type ids do not overlap.</summary>
public static class RuleTopics
{
    public static IReadOnlyList<RuleTopic> All { get; } =
    [
        .. GrammarCatalog.Topics.Select(t => new RuleTopic(t.Type, t.Title, StudyModule.Grammar)),
        .. NumberTheories.All.Select(t => new RuleTopic(t.Type, t.Title, StudyModule.Numbers)),
    ];

    public static IReadOnlySet<StudyModule> Modules { get; } = new HashSet<StudyModule> { StudyModule.Grammar, StudyModule.Numbers };

    public static RuleTopic? Find(string type) => All.FirstOrDefault(t => t.Type == type);
}
