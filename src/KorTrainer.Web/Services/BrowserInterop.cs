using Microsoft.JSInterop;

namespace KorTrainer.Web.Services;

public sealed record SpeechInfo(bool Supported, bool HasKoreanVoice, string? VoiceName, string Platform);

/// <summary>Typed access to <c>wwwroot/js/app.js</c>: speech synthesis, keyboard and theme.</summary>
public sealed class BrowserInterop(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;
    private SpeechInfo? _speech;

    public async ValueTask<SpeechInfo> GetSpeechInfoAsync()
    {
        if (_speech is null)
        {
            var module = await GetModuleAsync();
            _speech = await module.InvokeAsync<SpeechInfo>("speechInfo");
        }

        return _speech;
    }

    public async ValueTask SpeakAsync(string? text, double rate)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await GetSpeechInfoAsync();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("speak", text, rate);
    }

    public async ValueTask<int> RegisterKeysAsync<T>(DotNetObjectReference<T> target)
        where T : class
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<int>("registerKeys", target);
    }

    public async ValueTask UnregisterKeysAsync(int handlerId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("unregisterKeys", handlerId);
    }

    public async ValueTask ApplyThemeAsync(string theme)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("applyTheme", theme);
    }

    public async ValueTask BlurActiveAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("blurActive");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await (await _module).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away; nothing left to clean up.
            }
        }
    }

    // A failed import (e.g. a network blip) is not cached, so the next call retries.
    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/app.js").AsTask();
        try
        {
            return await _module;
        }
        catch
        {
            _module = null;
            throw;
        }
    }
}
