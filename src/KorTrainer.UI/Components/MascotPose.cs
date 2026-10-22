namespace KorTrainer.UI.Components;

/// <summary>Drawings under <c>wwwroot/img/mascot</c> (the author's Recraft sheets, split per pose).</summary>
public static class MascotPose
{
    /// <summary>The magpie flying with a sealed letter: reminders and news.</summary>
    public const string MagpieLetter = "magpie-letter";

    /// <summary>The magpie singing on a pine branch: «nothing left to do» screens.</summary>
    public const string MagpieBranch = "magpie-branch";

    /// <summary>The magpie hopping with wings up: a finished session.</summary>
    public const string MagpieHop = "magpie-hop";

    public const string TigerHello = "tiger-hello";
    public const string TigerLaugh = "tiger-laugh";
    public const string TigerThink = "tiger-think";
    public const string TigerOops = "tiger-oops";
    public const string TigerSleep = "tiger-sleep";
    public const string TigerCelebrate = "tiger-celebrate";

    /// <summary>The tiger's face for a home-page greeting.</summary>
    public static string ForMood(KorTrainer.Domain.Plan.GreetingMood mood) => mood switch
    {
        KorTrainer.Domain.Plan.GreetingMood.Thinking => TigerThink,
        KorTrainer.Domain.Plan.GreetingMood.Happy => TigerLaugh,
        KorTrainer.Domain.Plan.GreetingMood.Sleepy => TigerSleep,
        _ => TigerHello,
    };
}
