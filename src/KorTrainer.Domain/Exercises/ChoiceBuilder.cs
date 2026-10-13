namespace KorTrainer.Domain.Exercises;

internal static class ChoiceBuilder
{
    /// <summary>
    /// Shuffles the correct option among distinct distractors (by text) and returns the options
    /// together with the index of the correct one.
    /// </summary>
    public static (IReadOnlyList<ChoiceOption> Options, int CorrectIndex) Shuffle(
        ChoiceOption correct,
        IEnumerable<ChoiceOption> distractors,
        int optionCount,
        Random random)
    {
        var picked = new List<ChoiceOption> { correct };
        foreach (var option in distractors)
        {
            if (picked.Count == optionCount)
            {
                break;
            }

            if (picked.TrueForAll(p => p.Text != option.Text))
            {
                picked.Add(option);
            }
        }

        var shuffled = picked.OrderBy(_ => random.Next()).ToList();
        return (shuffled, shuffled.IndexOf(correct));
    }

    public static T Pick<T>(this IReadOnlyList<T> items, Random random) => items[random.Next(items.Count)];

    public static IEnumerable<T> Shuffled<T>(this IEnumerable<T> items, Random random) =>
        items.OrderBy(_ => random.Next()).ToList();
}
