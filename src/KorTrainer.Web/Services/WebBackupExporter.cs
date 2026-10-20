using KorTrainer.UI.Services;
using Microsoft.AspNetCore.Components;

namespace KorTrainer.Web.Services;

/// <summary>Lets the browser download the backup from the API (it is sent as an attachment).</summary>
public sealed class WebBackupExporter(NavigationManager navigation) : IBackupExporter
{
    public Task ExportAsync()
    {
        navigation.NavigateTo("api/progress/export", forceLoad: true);
        return Task.CompletedTask;
    }
}
