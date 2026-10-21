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
public sealed record ParticleResult(string Form, string Wrong, string Particle, string Explanation);

/// <summary>
/// Chooses the allomorph of a particle by whether the noun ends in batchim:
/// <c>(code − 0xAC00) % 28 != 0</c>.
/// </summary>
public static class Particles
{
    public static ParticleResult Attach(string noun, ParticleKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noun);
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

        return new ParticleResult(
            noun + right + suffix,
            noun + wrong + suffix,
            right,
            $"{name.First().ToString().ToUpperInvariant()}{name[1..]}: {afterConsonant} после согласной, {afterVowel} после гласной. В слове {noun} {reason} → {right}.");
    }
}
