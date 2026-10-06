using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class DecisionEngine : IDecisionEngine
{
    private const int MaxInterventionsPerHour = 3;

    private readonly IEventStore _eventStore;

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

        // Cooldown and hourly cap come from stored intervention events, so they hold
        // across scopes and app restarts rather than living in this instance.
        var now = DateTimeOffset.UtcNow;
        var cooldown = TimeSpan.FromMinutes(settings.InterventionCooldownMinutes);
        var lookback = cooldown > TimeSpan.FromHours(1) ? cooldown : TimeSpan.FromHours(1);

        var recentInterventions = await _eventStore.GetByTypeAsync(
            EventType.InterventionTriggered,
            now - lookback,
            cancellationToken: cancellationToken);

        if (recentInterventions.Any(e => now - e.Timestamp < cooldown))
        {
            return null;
        }

        if (recentInterventions.Count(e => e.Timestamp >= now.AddHours(-1)) >= MaxInterventionsPerHour)
        {
            return null;
        }

        var action = DetermineAction(ruleResult.DistractionScore, context);
        if (action == InterventionAction.Ignore || action == InterventionAction.Wait)
        {
            return null;
        }

        var message = BuildMessage(action, context);

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
