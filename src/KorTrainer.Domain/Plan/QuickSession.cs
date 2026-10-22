using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Plan;

/// <summary>A multiple-choice drill the short session can run inline.</summary>
public sealed record QuickDrill(StudyModule Module, string Type, int Level = 1);

/// <summary>A task of the current step, as the home screen sees it.</summary>
public sealed record QuickTask(string? Route, bool IsDone);

/// <summary>
/// Composes the one-tap «10 минут» session: due word cards, then a drill from the current step,
/// then a short second drill, sized to fit about ten minutes.
/// </summary>
public static class QuickSession
{
    public const int WordCards = 15;
    public const int DrillSize = 12;
    public const int SecondDrillSize = 8;
    public const int MistakeCount = 5;

    private const int MaxHangulLevel = 3;

    /// <param name="weakest">The topic answered worst lately; when given it takes the second drill's place.</param>
    public static IReadOnlyList<QuickDrill> Drills(PlanStage stage, IEnumerable<QuickTask> tasks, QuickDrill? weakest = null)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var drills = tasks.Select(t => (Drill: FromRoute(t.Route), t.IsDone)).Where(t => t.Drill is not null).ToList();
        var first = drills.FirstOrDefault(t => !t.IsDone).Drill
            ?? drills.FirstOrDefault().Drill
            ?? Fallback(stage);

        // During the Hangul lessons there are no words for listening yet: hear a letter, pick it.
        var second = stage <= PlanStage.Hangul
            ? new QuickDrill(StudyModule.Hangul, first.Type == ExerciseTypes.SoundToLetter ? ExerciseTypes.LetterToSound : ExerciseTypes.SoundToLetter, first.Level)
            : first.Module == StudyModule.Listening
                ? new QuickDrill(StudyModule.Grammar, ExerciseTypes.Mixed)
                : new QuickDrill(StudyModule.Listening, ExerciseTypes.ListenMeaning);

        return weakest is not null && weakest != first ? [first, weakest] : [first, second];
    }

    /// <summary>The drill behind a task link such as <c>/grammar/past</c> or <c>/hangul/compose?level=2</c>.</summary>
    public static QuickDrill? FromRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var (path, query) = route.Split('?', 2) is [var p, var q] ? (p, q) : (route, string.Empty);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? type = segments.Length > 1 ? segments[1] : null;
        return segments.FirstOrDefault() switch
        {
            "hangul" when type is not null && ExerciseTypes.HangulChoice.Contains(type) =>
                new QuickDrill(StudyModule.Hangul, type, Math.Clamp(LevelOf(query), 1, MaxHangulLevel)),
            "grammar" when type == ExerciseTypes.Mixed || (type is not null && GrammarCatalog.Find(type) is not null) =>
                new QuickDrill(StudyModule.Grammar, type!),
            "listening" when type is null => new QuickDrill(StudyModule.Listening, ExerciseTypes.ListenMeaning),
            "listening" when type is ExerciseTypes.ListenMeaning or ExerciseTypes.ListenSpelling =>
                new QuickDrill(StudyModule.Listening, type),
            "numbers" when type is not null => new QuickDrill(StudyModule.Numbers, type),
            _ => null,
        };
    }

    private static QuickDrill Fallback(PlanStage stage) => stage <= PlanStage.Hangul
        ? new QuickDrill(StudyModule.Hangul, ExerciseTypes.LetterToSound)
        : new QuickDrill(StudyModule.Grammar, ExerciseTypes.Mixed);

    private static int LevelOf(string query)
    {
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (pair.Split('=', 2) is ["level", var value] && int.TryParse(value, out int level))
            {
                return level;
            }
        }

        return 1;
    }
}
