using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace KorTrainer.Mobile;

/// <summary>A single window hosting the shared Blazor pages.</summary>
public sealed class App : Microsoft.Maui.Controls.Application
{
    private BlazorWebView? _webView;

    /// <summary>The page to open first, set by <see cref="MainActivity"/> when the widget started the app.</summary>
    public static string? StartRoute { get; set; }

    /// <summary>Opens a page in the running app (a widget tap while the app is already open).</summary>
    public async Task NavigateAsync(string route)
    {
        if (_webView is not null)
        {
            await _webView.TryDispatchAsync(services => services.GetRequiredService<NavigationManager>().NavigateTo(route));
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _webView = CreateWebView();
        return new(new ContentPage { Content = _webView }) { Title = "KorTrainer" };
    }

    private static BlazorWebView CreateWebView()
    {
        var webView = new BlazorWebView { HostPage = "wwwroot/index.html", StartPath = StartRoute ?? "/" };
        StartRoute = null;
        webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(UI.App) });
        return webView;
    }
}
