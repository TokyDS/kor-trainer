namespace KorTrainer.Domain.Typing;

/// <param name="Code">KeyboardEvent.code of the physical key (independent of the OS layout), e.g. "KeyG".</param>
public sealed record DubeolsikKey(string Code, bool Shift);

/// <summary>The standard Korean two-set layout (두벌식, KS X 5002): consonants on the left hand, vowels on the right.</summary>
public static class Dubeolsik
{
    /// <summary>Rows of the layout as (code, plain jamo, shifted jamo or null).</summary>
    public static IReadOnlyList<IReadOnlyList<(string Code, char Jamo, char? Shifted)>> Rows { get; } =
    [
        [("KeyQ", 'ㅂ', 'ㅃ'), ("KeyW", 'ㅈ', 'ㅉ'), ("KeyE", 'ㄷ', 'ㄸ'), ("KeyR", 'ㄱ', 'ㄲ'), ("KeyT", 'ㅅ', 'ㅆ'),
         ("KeyY", 'ㅛ', null), ("KeyU", 'ㅕ', null), ("KeyI", 'ㅑ', null), ("KeyO", 'ㅐ', 'ㅒ'), ("KeyP", 'ㅔ', 'ㅖ')],
        [("KeyA", 'ㅁ', null), ("KeyS", 'ㄴ', null), ("KeyD", 'ㅇ', null), ("KeyF", 'ㄹ', null), ("KeyG", 'ㅎ', null),
         ("KeyH", 'ㅗ', null), ("KeyJ", 'ㅓ', null), ("KeyK", 'ㅏ', null), ("KeyL", 'ㅣ', null)],
        [("KeyZ", 'ㅋ', null), ("KeyX", 'ㅌ', null), ("KeyC", 'ㅊ', null), ("KeyV", 'ㅍ', null), ("KeyB", 'ㅠ', null),
         ("KeyN", 'ㅜ', null), ("KeyM", 'ㅡ', null)],
    ];

    private static readonly Dictionary<string, (char Jamo, char? Shifted)> ByCode =
        Rows.SelectMany(r => r).ToDictionary(k => k.Code, k => (k.Jamo, k.Shifted), StringComparer.Ordinal);

    private static readonly Dictionary<char, DubeolsikKey> ByJamo = Rows.SelectMany(r => r)
        .SelectMany(k => k.Shifted is { } s
            ? new[] { (k.Jamo, new DubeolsikKey(k.Code, false)), (s, new DubeolsikKey(k.Code, true)) }
            : [(k.Jamo, new DubeolsikKey(k.Code, false))])
        .ToDictionary(x => x.Item1, x => x.Item2);

    /// <summary>The jamo a physical key types, or null for keys outside the layout.</summary>
    public static char? JamoFor(string code, bool shift) =>
        ByCode.TryGetValue(code ?? string.Empty, out var key) ? (shift && key.Shifted is { } s ? s : key.Jamo) : null;

    public static DubeolsikKey? KeyFor(char jamo) => ByJamo.GetValueOrDefault(jamo);

    public static IReadOnlyList<char> Keystrokes(string text) => HangulComposer.Keystrokes(text);

    /// <summary>
    /// The next jamo to type when <paramref name="typed"/> is what the keyboard shows so far, or null when the
    /// text is complete or no longer on the way to <paramref name="target"/> (a typo to fix).
    /// </summary>
    public static char? NextJamo(string target, string typed)
    {
        ArgumentNullException.ThrowIfNull(target);
        var keys = Keystrokes(target);
        var composer = new HangulComposer();
        for (int k = 0; k <= keys.Count; k++)
        {
            if (composer.Text == typed)
            {
                return k < keys.Count ? keys[k] : null;
            }

            if (k < keys.Count)
            {
                composer.Type(keys[k]);
            }
        }

        return null;
    }
}
