using Microsoft.JSInterop;

namespace KorTrainer.UI.Services;

/// <summary>Typed access to <c>wwwroot/js/app.js</c>: speech synthesis, audio playback, keyboard and theme.</summary>
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

    /// <summary>Plays a recording; false if it could not start (offline, missing file).</summary>
    public async ValueTask<bool> PlayRecordingAsync(string url, double rate = 1)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("playRecording", url, rate);
    }

    /// <summary>Plays audio bytes held by .NET (the app has no HTTP server to give a URL).</summary>
    public async ValueTask<bool> PlayRecordingDataAsync(Stream content, string contentType, double rate = 1)
    {
        var module = await GetModuleAsync();
        using var reference = new DotNetStreamReference(content, leaveOpen: true);
        return await module.InvokeAsync<bool>("playRecordingData", reference, contentType, rate);
    }

    /// <summary>Stops a recording that is still playing (before the app speaks through another channel).</summary>
    public async ValueTask StopAudioAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("stopAudio");
    }

    public async ValueTask<IReadOnlyList<VoiceInfo>> GetKoreanVoicesAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<VoiceInfo[]>("koreanVoices");
    }

    public async ValueTask<string?> ChooseVoiceAsync(string name)
    {
        var module = await GetModuleAsync();
        string? chosen = await module.InvokeAsync<string?>("chooseVoice", name);
        if (_speech is not null)
        {
            _speech = _speech with { VoiceName = chosen, HasKoreanVoice = chosen is not null };
        }

        return chosen;
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

    /// <summary>Calls any function exported by app.js.</summary>
    public async ValueTask<T> InvokeAsync<T>(string function, params object?[] args)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<T>(function, args);
    }

    public async ValueTask InvokeVoidAsync(string function, params object?[] args)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(function, args);
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
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/KorTrainer.UI/js/app.js").AsTask();
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
