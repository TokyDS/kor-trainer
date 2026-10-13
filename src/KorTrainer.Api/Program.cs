using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using KorTrainer.Api.Endpoints;
using KorTrainer.Api.ErrorHandling;
using KorTrainer.Application;
using KorTrainer.Infrastructure;
using KorTrainer.Infrastructure.Persistence;

const int RequestsPerMinute = 600;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Single-user app, but it listens on the LAN: cap request bursts per client address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseWebAssemblyDebugging();
}

// Serves the Blazor WebAssembly client (fingerprinted, pre-compressed framework files).
app.MapStaticAssets();

app.MapKorTrainerApi();
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Entry point marker used by integration tests (WebApplicationFactory).</summary>
public partial class Program;
