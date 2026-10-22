using KorTrainer.Application.Common;
using KorTrainer.Application.Hangul;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;

namespace KorTrainer.Application.Numbers;

public sealed class NumbersService(Random random)
{
    public const int MaxCount = 50;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.SinoNumbers] = "Сино-корейские числа",
        [ExerciseTypes.NativeNumbers] = "Корейские (исконные) числа",
        [ExerciseTypes.Counters] = "Счётные слова",
        [ExerciseTypes.Time] = "Время",
        [ExerciseTypes.Money] = "Деньги",
        [ExerciseTypes.Mixed] = "Числа вперемешку",
    };

    public static string TitleOf(string type) => Titles.GetValueOrDefault(type, type);

    /// <param name="type">One of <see cref="NumberExerciseGenerator.Types"/> or <see cref="ExerciseTypes.Mixed"/>.</param>
    public ExerciseSetDto GetExercises(string type, int count)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения с числами.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var types = type == ExerciseTypes.Mixed ? NumberExerciseGenerator.Types : [type];
        var items = new NumberExerciseGenerator(random).Generate(types, count).Select(ExerciseMapper.ToDto).ToList();
        return new ExerciseSetDto(StudyModule.Numbers, type, Titles[type], items);
    }
}
