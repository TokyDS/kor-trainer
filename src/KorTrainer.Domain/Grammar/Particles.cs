using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Grammar;

public enum ParticleKind
{
    /// <summary>이에요/예요 — "is".</summary>
    Copula,

    /// <summary>이/가 아니에요 — "is not".</summary>
    NegativeCopula,

    /// <summary>은/는 — topic.</summary>
    Topic,

    /// <summary>이/가 — subject.</summary>
    Subject,

    /// <summary>을/를 — object.</summary>
    Object,

    /// <summary>와/과 — "and, with".</summary>
    With,

    /// <summary>(으)로 — means, material, direction; 로 also after ㄹ.</summary>
    Means,
}

/// <param name="Form">The noun with the particle attached, e.g. 책이에요.</param>
/// <param name="Wrong">The same noun with the other allomorph — the typical mistake.</param>
/// <param name="WrongWhy">Why <see cref="Wrong"/> is wrong for this very noun.</param>
public sealed record ParticleResult(string Form, string Wrong, string Particle, string Explanation, string WrongWhy);

/// <summary>
/// Chooses the allomorph of a particle by whether the noun ends in batchim:
/// <c>(code − 0xAC00) % 28 != 0</c>.
/// </summary>
public static class Particles
{
    // Pronouns that change before 가: 저 + 가 = 제가, 나 + 가 = 내가, 누구 + 가 = 누가 («кто?» is never negated).
    private static readonly Dictionary<string, string> ContractedBeforeGa = new() { ["저"] = "제", ["나"] = "내", ["누구"] = "누" };

    public static ParticleResult Attach(string noun, ParticleKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noun);
        if (kind is ParticleKind.Subject or ParticleKind.NegativeCopula
            && ContractedBeforeGa.TryGetValue(noun, out string? contracted)
            && !(noun == "누구" && kind == ParticleKind.NegativeCopula))
        {
            return Contracted(noun, contracted, kind);
        }

        char last = Batchim.LastSyllable(noun);
        bool rieul = Syllable.Decompose(last).Final == 'ㄹ';
        bool batchim = Batchim.EndsWithBatchim(noun) && !(kind == ParticleKind.Means && rieul);
        var (afterConsonant, afterVowel, suffix, name) = kind switch
        {
            ParticleKind.Copula => ("이에요", "예요", string.Empty, "связка «это …»"),
            ParticleKind.NegativeCopula => ("이", "가", " 아니에요", "отрицание «это не …»"),
            ParticleKind.Topic => ("은", "는", string.Empty, "частица темы"),
            ParticleKind.Subject => ("이", "가", string.Empty, "частица подлежащего"),
            ParticleKind.Object => ("을", "를", string.Empty, "частица дополнения"),
            ParticleKind.With => ("과", "와", string.Empty, "частица «и, с»"),
            ParticleKind.Means => ("으로", "로", string.Empty, "частица средства и направления"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        string right = batchim ? afterConsonant : afterVowel;
        string wrong = batchim ? afterVowel : afterConsonant;
        string reason = !batchim && rieul
            ? $"последний слог {last} оканчивается на ㄹ, а после ㄹ пишется 로, как после гласной"
            : batchim
            ? $"последний слог {last} оканчивается на согласную {Syllable.Decompose(last).Final} (есть патчим)"
            : $"последний слог {last} оканчивается на гласную (нет патчима)";

        char? final = Syllable.Decompose(last).Final;
        string wrongWhy = kind == ParticleKind.Means && rieul
            ? $"{wrong} не пишут после ㄹ: последний слог {last} оканчивается на ㄹ, а после неё ставится 로, как после гласной."
            : batchim
            ? $"{wrong} ставится после гласной, а последний слог {last} оканчивается на согласную {final}."
            : $"{wrong} ставится после согласной, а последний слог {last} оканчивается гласной (нет патчима).";

        return new ParticleResult(
            noun + right + suffix,
            noun + wrong + suffix,
            right,
            $"{Capitalized(name)}: {afterConsonant} после согласной, {afterVowel} после гласной. В слове {noun} {reason} → {right}.",
            wrongWhy);
    }

    private static ParticleResult Contracted(string noun, string contracted, ParticleKind kind)
    {
        string suffix = kind == ParticleKind.NegativeCopula ? " 아니에요" : string.Empty;
        string name = kind == ParticleKind.NegativeCopula ? "отрицание «это не …»" : "частица подлежащего";
        string form = contracted + "가";
        return new ParticleResult(
            form + suffix,
            noun + "가" + suffix,
            "가",
            $"{Capitalized(name)}: 이 после согласной, 가 после гласной. Но {noun} + 가 сливаются в {form} → {form}{suffix}.",
            $"{noun} перед 가 меняется: {noun} + 가 = {form}. Форму «{noun}가» не используют.");
    }

    private static string Capitalized(string text) => text.First().ToString().ToUpperInvariant() + text[1..];
}
