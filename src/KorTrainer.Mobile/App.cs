using Microsoft.AspNetCore.Components.WebView.Maui;

namespace KorTrainer.Mobile;

/// <summary>A single window hosting the shared Blazor pages.</summary>
public sealed class App : Microsoft.Maui.Controls.Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new ContentPage { Content = CreateWebView() }) { Title = "KorTrainer" };

    private static BlazorWebView CreateWebView()
    {
        var webView = new BlazorWebView { HostPage = "wwwroot/index.html" };
        webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(UI.App) });
        return webView;
    }
}
