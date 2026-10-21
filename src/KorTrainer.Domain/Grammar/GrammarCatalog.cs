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
        new(ExerciseTypes.Not, "안 + глагол", "Короткое отрицание «не»: 안 가요, 안 먹어요; 공부 안 해요.", PlanStage.Level1, Ending: Grammar.Ending.Not),
        new(ExerciseTypes.Past, "-았/었어요", "Прошедшее время от формы на -아/어.", PlanStage.Level1, Ending: Grammar.Ending.PolitePast),
        new(ExerciseTypes.WithParticle, "와/과", "«И, вместе с»: 과 после согласной, 와 после гласной.", PlanStage.Level1, ParticleKind.With),
        new(ExerciseTypes.FormalPresent, "-습니다/ㅂ니다", "Официальный стиль: -습니다 после согласной, -ㅂ니다 после гласной.", PlanStage.Level1, Ending: Grammar.Ending.FormalPresent),
        new(ExerciseTypes.NotJi, "-지 않다", "Длинное отрицание: 가지 않아요, 먹지 않아요 — основа не меняется.", PlanStage.Level1, Ending: Grammar.Ending.NotJi),
        new(ExerciseTypes.Future, "-(으)ㄹ 거예요", "Будущее время и предположение: 갈 거예요, 먹을 거예요.", PlanStage.Level1, Ending: Grammar.Ending.Future),
        new(ExerciseTypes.MeansParticle, "(으)로", "Средство, материал, направление: 버스로, 손으로; после ㄹ тоже 로 (연필로).", PlanStage.Level1, ParticleKind.Means),
        new(ExerciseTypes.Please, "-(으)세요", "Уважительная форма и вежливая просьба: 가세요, 읽으세요; 먹다 → 드세요.", PlanStage.Level1, Ending: Grammar.Ending.Please),
        new(ExerciseTypes.Progressive, "-고 있다", "Действие в процессе: 먹고 있어요 «сейчас ем».", PlanStage.Level1, Ending: Grammar.Ending.Progressive),
        new(ExerciseTypes.But, "-지만", "«…, но …»: 비싸지만 맛있어요.", PlanStage.Level1, Ending: Grammar.Ending.But),
        new(ExerciseTypes.AndGo, "-고", "Соединительное «и»: 먹고, 가고.", PlanStage.Level2, Ending: Grammar.Ending.And),
        new(ExerciseTypes.Want, "-고 싶다", "«Хочу …»: 가고 싶어요.", PlanStage.Level2, Ending: Grammar.Ending.Want),
        new(ExerciseTypes.SoAseo, "-아서/어서", "«…и поэтому», последовательность действий.", PlanStage.Level2, Ending: Grammar.Ending.SoThat),
        new(ExerciseTypes.BecauseNikka, "-(으)니까", "«Потому что»: 으 после согласной (кроме ㄹ).", PlanStage.Level2, Ending: Grammar.Ending.Because),
        new(ExerciseTypes.Can, "-(으)ㄹ 수 있다", "«Могу …»: 갈 수 있어요, 먹을 수 있어요.", PlanStage.Level2, Ending: Grammar.Ending.Can),
        new(ExerciseTypes.Cannot, "못 + глагол", "«Не могу»: 못 가요, 운동 못 해요.", PlanStage.Level2, Ending: Grammar.Ending.Cannot),
        new(ExerciseTypes.Must, "-아/어야 하다", "«Нужно …»: 가야 해요, 먹어야 해요.", PlanStage.Level2, Ending: Grammar.Ending.Must),
        new(ExerciseTypes.ForMe, "-아/어 주세요", "Просьба «сделайте для меня»: 기다려 주세요.", PlanStage.Level2, Ending: Grammar.Ending.ForMe),
        new(ExerciseTypes.If, "-(으)면", "«Если, когда»: 가면, 먹으면; основа на ㄹ сохраняет ㄹ — 살면.", PlanStage.Level2, Ending: Grammar.Ending.If),
        new(ExerciseTypes.Purpose, "-(으)러 가다", "«Иду, чтобы …»: 먹으러 가요, 놀러 가요.", PlanStage.Level2, Ending: Grammar.Ending.Purpose),
        new(ExerciseTypes.Intention, "-(으)ㄹ게요", "«Я сделаю» — обещание собеседнику: 갈게요, 먹을게요.", PlanStage.Level2, Ending: Grammar.Ending.Intention),
        new(ExerciseTypes.LetUs, "-(으)ㄹ까요?", "«Давайте …?», «Мне …?»: 갈까요? 먹을까요?", PlanStage.Level2, Ending: Grammar.Ending.LetUs),
        new(ExerciseTypes.BecauseGi, "-기 때문에", "«Из-за того, что» — письменный стиль: 바쁘기 때문에.", PlanStage.Level2, Ending: Grammar.Ending.BecauseGi),
    ];

    public static GrammarTopic? Find(string type) => Topics.FirstOrDefault(t => t.Type == type);
}
