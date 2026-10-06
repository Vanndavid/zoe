using Microsoft.Toolkit.Uwp.Notifications;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.UI.Services;

public sealed class ToastNotificationService : INotificationService
{
    public Task ShowInterventionAsync(Intervention intervention, CancellationToken cancellationToken = default)
    {
        Show(GetTitle(intervention.Action), intervention.Message);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Show(title, message);
        return Task.CompletedTask;
    }

    private static void Show(string title, string message) =>
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();

    private static string GetTitle(InterventionAction action) => action switch
    {
        InterventionAction.Escalate => "Time to refocus",
        InterventionAction.Warn => "Heads up",
        _ => "Nudge"
    };
}
