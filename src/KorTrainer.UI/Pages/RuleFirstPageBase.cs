using KorTrainer.Contracts;
using KorTrainer.UI.Services;
using Microsoft.AspNetCore.Components;

namespace KorTrainer.UI.Pages;

/// <summary>
/// A drill page that shows its rule first. "Понятно" marks the rule read for the plan and starts the drill;
/// a failed mark is retried before every set, so a lesson task cannot get stuck. <c>?drill=true</c> skips the rule.
/// </summary>
public abstract class RuleFirstPageBase : ComponentBase
{
    private string? _shownType;
    private bool _readPending;

    [Inject]
    protected IKorTrainerApi Api { get; set; } = default!;

    [Parameter]
    public string Type { get; set; } = string.Empty;

    /// <summary>Set by plan tasks whose rule was read in the task before: straight to the exercises.</summary>
    [SupplyParameterFromQuery(Name = "drill")]
    public bool Drill { get; set; }

    protected bool Drilling { get; private set; }

    protected string? ReadError { get; private set; }

    /// <summary>The id the plan knows this rule by (<see cref="Domain.Plan.StudyMaterials"/>).</summary>
    protected abstract string Material { get; }

    /// <summary>Looks up the rule of <see cref="Type"/>; without one the drill starts at once.</summary>
    protected abstract bool LoadRule();

    protected override void OnParametersSet()
    {
        if (_shownType == Type)
        {
            // Same topic, now with ?drill=true: go on to the exercises; never back to the rule mid-drill.
            Drilling |= Drill;
            return;
        }

        // Another topic: its rule comes first again.
        _shownType = Type;
        Drilling = Drill || !LoadRule();
        _readPending = false;
        ReadError = null;
    }

    protected async Task StartAsync()
    {
        Drilling = true;
        _readPending = true;
        await MarkReadAsync();
    }

    /// <summary>Call before loading every set of exercises.</summary>
    protected async Task RetryMarkReadAsync()
    {
        if (_readPending)
        {
            await MarkReadAsync();
        }
    }

    private async Task MarkReadAsync()
    {
        try
        {
            await Api.MarkReadAsync(Material);
            _readPending = false;
            ReadError = null;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            ReadError = ex.Message;
        }
    }
}
