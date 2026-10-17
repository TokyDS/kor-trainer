using KorTrainer.UI;
using KorTrainer.UI.Services;
using KorTrainer.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IKorTrainerApi, HttpKorTrainerApi>();
builder.Services.AddScoped<BrowserInterop>();
builder.Services.AddScoped<ISpeechOutput, BrowserSpeechOutput>();
builder.Services.AddScoped<IBackupExporter, WebBackupExporter>();
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();
