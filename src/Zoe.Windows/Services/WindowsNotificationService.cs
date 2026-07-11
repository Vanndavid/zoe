using System.Runtime.Versioning;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Windows.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsNotificationService : INotificationService
{
    public Task ShowInterventionAsync(Intervention intervention, CancellationToken cancellationToken = default)
    {
        // Windows toast notifications will be wired in a future iteration.
        Console.WriteLine($"[Zoe Intervention] {intervention.Action}: {intervention.Message}");
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Zoe] {title}: {message}");
        return Task.CompletedTask;
    }
}
