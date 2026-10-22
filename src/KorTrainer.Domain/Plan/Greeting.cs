using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>Which face the companion shows with its line.</summary>
public enum GreetingMood
{
    Hello,
    Thinking,
    Happy,
    Sleepy,
}

public sealed record Greeting(GreetingMood Mood, string Text);

/// <summary>
/// The companion's one line on the home page. Gentle by rule: it may point at what is due, but never
/// counts days, mentions streaks or says the learner is behind.
/// </summary>
public static class Greetings
{
    private const int NightStarts = 23;
    private const int MorningStarts = 5;
    private const int DayStarts = 12;
    private const int EveningStarts = 18;

    public static Greeting For(int localHour, int dueWords, int dueMistakes, bool studiedToday)
    {
        if (localHour >= NightStarts || localHour < MorningStarts)
        {
            return new(GreetingMood.Sleepy, studiedToday
                ? "Поздно — пора отдыхать. Сон помогает словам улечься."
                : "Поздно уже. Если хочется — пять минут, а слова подождут до завтра.");
        }

        string hello = localHour < DayStarts ? "Доброе утро!" : localHour < EveningStarts ? "Привет!" : "Добрый вечер!";
        if (dueMistakes > 0)
        {
            return new(GreetingMood.Thinking,
                $"{hello} {RussianPlural.Count(dueMistakes, "ошибка ждёт", "ошибки ждут", "ошибок ждут")} повторения — разберём вместе?");
        }

        if (dueWords > 0)
        {
            return new(GreetingMood.Hello,
                $"{hello} {RussianPlural.Count(dueWords, "слово ждёт", "слова ждут", "слов ждут")} повторения.");
        }

        return studiedToday
            ? new(GreetingMood.Happy, "Сегодня всё повторено. Можно взять новые слова или просто отдохнуть.")
            : new(GreetingMood.Hello, $"{hello} Повторять пока нечего — можно начать с «10 минут».");
    }
}
