namespace KorTrainer.Domain.Hangul;

/// <summary>Sound changes applied by <see cref="Pronunciation"/>.</summary>
public enum SoundRule
{
    /// <summary>연음: a final consonant moves into the next syllable that starts with silent ㅇ.</summary>
    Linking,

    /// <summary>ㅎ is not pronounced before a vowel (좋아요 → [조아요]).</summary>
    HieutDrop,

    /// <summary>A final consonant is pronounced as one of the 7 representative sounds (꽃 → [꼳]).</summary>
    Neutralization,

    /// <summary>비음화: ㄱ/ㄷ/ㅂ before ㄴ/ㅁ become ㅇ/ㄴ/ㅁ (합니다 → [함니다]).</summary>
    Nasalization,

    /// <summary>유음화: ㄴ next to ㄹ becomes ㄹ (신라 → [실라]).</summary>
    Liquidization,

    /// <summary>ㄹ after ㅁ/ㅇ (or a nasalized stop) is read as ㄴ (종로 → [종노]).</summary>
    RieulNasalization,

    /// <summary>경음화: ㄱ/ㄷ/ㅂ/ㅅ/ㅈ after a stop become tense (학교 → [학꾜]).</summary>
    Tensification,

    /// <summary>격음화: a stop next to ㅎ becomes aspirated (축하 → [추카], 좋다 → [조타]).</summary>
    Aspiration,

    /// <summary>구개음화: ㄷ/ㅌ before 이 become ㅈ/ㅊ (같이 → [가치]).</summary>
    Palatalization,
}
