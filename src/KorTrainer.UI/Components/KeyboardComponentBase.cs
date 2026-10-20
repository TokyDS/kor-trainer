using KorTrainer.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace KorTrainer.UI.Components;

/// <summary>
/// Base for exercise screens controlled from the keyboard. Receives normalized keys
/// ("1"–"9", "Enter", "Space", "Escape", letters) from <c>app.js</c> while the component is alive.
/// Derived components that override <see cref="OnAfterRenderAsync"/> must call the base.
/// </summary>
public abstract class KeyboardComponentBase : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<KeyboardComponentBase>? _reference;
    private int? _handlerId;
    private bool _disposed;
    private bool _handlingKey;

    [Inject]
    protected BrowserInterop Browser { get; set; } = default!;

    [JSInvokable]
    public async Task OnKey(string key)
    {
        // Ignore keys after disposal and while a previous key is still being processed.
        if (_disposed || _handlingKey)
        {
            return;
        }

        _handlingKey = true;
        try
        {
            await InvokeAsync(async () =>
            {
                await HandleKeyAsync(key);
                StateHasChanged();
            });
        }
        finally
        {
            _handlingKey = false;
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        _disposed = true;
        await UnregisterAsync();
        _reference?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected abstract Task HandleKeyAsync(string key);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _reference = DotNetObjectReference.Create(this);
        try
        {
            _handlerId = await Browser.RegisterKeysAsync(_reference);
        }
        catch (JSException)
        {
            // Without the script the page still works by mouse/touch.
            return;
        }

        // The component may have been disposed while the JS module was loading.
        if (_disposed)
        {
            await UnregisterAsync();
        }
    }

    protected static int? DigitOf(string key) =>
        key.Length == 1 && char.IsAsciiDigit(key[0]) ? key[0] - '0' : null;

    private async Task UnregisterAsync()
    {
        if (_handlerId is not { } id)
        {
            return;
        }

        _handlerId = null;
        try
        {
            await Browser.UnregisterKeysAsync(id);
        }
        catch (JSDisconnectedException)
        {
            // Browser already gone; listeners died with it.
        }
    }
}
