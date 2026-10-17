using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Grammar;

/// <summary>A grammar topic that can be drilled; <see cref="Stage"/> is when the plan introduces it.</summary>
public sealed record GrammarTopic(
    string Type,
    string Title,
    string Description,
    PlanStage Stage,
    ParticleKind? Particle = null,
    Ending? Ending = null);

public static class GrammarCatalog
{
    public static IReadOnlyList<GrammarTopic> Topics { get; } =
    [
        new(ExerciseTypes.Copula, "이에요/예요", "«Это …»: 이에요 после согласной, 예요 после гласной.", PlanStage.FirstPhrases, ParticleKind.Copula),
        new(ExerciseTypes.NegativeCopula, "이/가 아니에요", "«Это не …»: 이 после согласной, 가 после гласной.", PlanStage.FirstPhrases, ParticleKind.NegativeCopula),
        new(ExerciseTypes.TopicParticle, "은/는", "Частица темы: 은 после согласной, 는 после гласной.", PlanStage.FirstPhrases, ParticleKind.Topic),
        new(ExerciseTypes.SubjectParticle, "이/가", "Частица подлежащего: 이 после согласной, 가 после гласной.", PlanStage.Level1, ParticleKind.Subject),
        new(ExerciseTypes.ObjectParticle, "을/를", "Частица прямого дополнения: 을 после согласной, 를 после гласной.", PlanStage.Level1, ParticleKind.Object),
        new(ExerciseTypes.PolitePresent, "-아/어요", "Вежливое настоящее время: гармония гласных, слияние, неправильные глаголы.", PlanStage.Level1, Ending: Grammar.Ending.PolitePresent),
        new(ExerciseTypes.Past, "-았/었어요", "Прошедшее время от формы на -아/어.", PlanStage.Level1, Ending: Grammar.Ending.PolitePast),
        new(ExerciseTypes.FormalPresent, "-습니다/ㅂ니다", "Официальный стиль: -습니다 после согласной, -ㅂ니다 после гласной.", PlanStage.Level1, Ending: Grammar.Ending.FormalPresent),
        new(ExerciseTypes.AndGo, "-고", "Соединительное «и»: 먹고, 가고.", PlanStage.Level2, Ending: Grammar.Ending.And),
        new(ExerciseTypes.SoAseo, "-아서/어서", "«…и поэтому», последовательность действий.", PlanStage.Level2, Ending: Grammar.Ending.SoThat),
        new(ExerciseTypes.BecauseNikka, "-(으)니까", "«Потому что»: 으 после согласной (кроме ㄹ).", PlanStage.Level2, Ending: Grammar.Ending.Because),
        new(ExerciseTypes.Want, "-고 싶다", "«Хочу …»: 가고 싶어요.", PlanStage.Level2, Ending: Grammar.Ending.Want),
        new(ExerciseTypes.Can, "-(으)ㄹ 수 있다", "«Могу …»: 갈 수 있어요, 먹을 수 있어요.", PlanStage.Level2, Ending: Grammar.Ending.Can),
        new(ExerciseTypes.Must, "-아/어야 하다", "«Нужно …»: 가야 해요, 먹어야 해요.", PlanStage.Level2, Ending: Grammar.Ending.Must),
    ];

    public static GrammarTopic? Find(string type) => Topics.FirstOrDefault(t => t.Type == type);
}
