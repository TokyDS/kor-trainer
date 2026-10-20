using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Common;

internal static class StudyScope
{
    /// <summary>
    /// Latest deck stage whose words may appear in drills (grammar, reading): the current stage, but
    /// never less than stage 1 so early drills have enough words, and never beyond the last word stage.
    /// </summary>
    public static PlanStage MaxWordStage(PlanStage current) => current switch
    {
        < PlanStage.Level1 => PlanStage.Level1,
        PlanStage.MockExam => PlanStage.Level2,
        > PlanStage.TopikPrep => PlanStage.TopikPrep,
        _ => current,
    };
}
