namespace KorTrainer.UI.Components;

/// <summary>How a finished set went: items answered right at the first try without a hint, out of all items.</summary>
public sealed record DrillScore(int Correct, int Total);
