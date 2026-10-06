using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;

namespace Zoe.Application.Services;

public sealed class InterventionDeliveryService : IInterventionDeliveryService
{
    private readonly ICoachService _coachService;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<InterventionDeliveryService> _logger;

    public InterventionDeliveryService(
        ICoachService coachService,
        INotificationService notificationService,
        IEventBus eventBus,
        ILogger<InterventionDeliveryService> logger)
    {
        _coachService = coachService;
        _notificationService = notificationService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Intervention?> EvaluateAndDeliverAsync(CancellationToken cancellationToken = default)
    {
        var intervention = await _coachService.EvaluateInterventionAsync(cancellationToken);
        if (intervention is null)
        {
            return null;
        }

        var delivered = false;
        try
        {
            await _notificationService.ShowInterventionAsync(intervention, cancellationToken);
            delivered = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to deliver {Action} intervention", intervention.Action);
        }

        // Recorded even when delivery fails: the decision cooldown reads these events, so a
        // broken notifier cannot cause a fresh evaluation (and LLM call) on every check.
        await _eventBus.PublishAsync(
            ActivityEvent.Create(
                EventType.InterventionTriggered,
                "InterventionDelivery",
                EventPayload.FromDictionary(new Dictionary<string, string>
                {
                    ["action"] = intervention.Action.ToString(),
                    ["message"] = intervention.Message,
                    ["delivered"] = delivered ? "true" : "false"
                })),
            cancellationToken);

        _logger.LogInformation(
            "Intervention {Action} {Outcome}",
            intervention.Action,
            delivered ? "delivered" : "not delivered");

        return new Intervention
        {
            Id = intervention.Id,
            CreatedAt = intervention.CreatedAt,
            Action = intervention.Action,
            Message = intervention.Message,
            Evidence = intervention.Evidence,
            Confidence = intervention.Confidence,
            WasDelivered = delivered
        };
    }
}
