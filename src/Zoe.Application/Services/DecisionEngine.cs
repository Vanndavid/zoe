using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class DecisionEngine : IDecisionEngine
{
    private readonly IEventStore _eventStore;
    private DateTimeOffset? _lastInterventionAt;

    public DecisionEngine(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<Intervention?> DecideAsync(
        Context context,
        RuleEvaluationResult ruleResult,
        UserSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (ruleResult.ShouldSuppressIntervention)
        {
            return null;
        }

        if (_lastInterventionAt.HasValue &&
            DateTimeOffset.UtcNow - _lastInterventionAt.Value <
            TimeSpan.FromMinutes(settings.InterventionCooldownMinutes))
        {
            return null;
        }

        var recentInterventions = await _eventStore.GetByTypeAsync(
            EventType.InterventionTriggered,
            DateTimeOffset.UtcNow.AddHours(-1),
            cancellationToken: cancellationToken);

        if (recentInterventions.Count >= 3)
        {
            return null;
        }

        var action = DetermineAction(ruleResult.DistractionScore, context);
        if (action == InterventionAction.Ignore || action == InterventionAction.Wait)
        {
            return null;
        }

        var message = BuildMessage(action, context);
        _lastInterventionAt = DateTimeOffset.UtcNow;

        return new Intervention
        {
            Action = action,
            Message = message,
            Evidence = ruleResult.Evidence,
            Confidence = Math.Min(ruleResult.DistractionScore / 100.0, 1.0),
            WasDelivered = false
        };
    }

    private static InterventionAction DetermineAction(double distractionScore, Context context)
    {
        if (distractionScore >= 80)
        {
            return InterventionAction.Escalate;
        }

        if (distractionScore >= 60)
        {
            return InterventionAction.Warn;
        }

        if (distractionScore >= 40)
        {
            return InterventionAction.Encourage;
        }

        if (context.RiskLevel == "Medium")
        {
            return InterventionAction.Wait;
        }

        return InterventionAction.Ignore;
    }

    private static string BuildMessage(InterventionAction action, Context context)
    {
        var app = context.ActiveApplication ?? "your current activity";

        return action switch
        {
            InterventionAction.Escalate =>
                $"You've been on {app} for a while and goal alignment is {context.GoalAlignmentPercent:F0}%. Time to refocus.",
            InterventionAction.Warn =>
                $"Heads up: {app} may not align with your goals right now ({context.GoalAlignmentPercent:F0}% alignment).",
            InterventionAction.Encourage =>
                $"Consider returning to your goals. Current focus level: {context.FocusLevel}.",
            _ => string.Empty
        };
    }
}
