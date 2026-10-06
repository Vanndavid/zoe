using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Services;

/// <summary>
/// Default notifier for console hosts. The WPF app replaces it with Windows toasts.
/// </summary>
public sealed class ConsoleNotificationService : INotificationService
{
    public Task ShowInterventionAsync(Intervention intervention, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Zoe Intervention] {intervention.Action}: {intervention.Message}");
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Zoe] {title}: {message}");
        return Task.CompletedTask;
    }
}
