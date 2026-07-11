using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class RuleEngine : IRuleEngine
{
    private static readonly HashSet<string> DistractionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "Discord", "Steam"
    };

    public RuleEvaluationResult Evaluate(Context context, UserSettings settings)
    {
        var distractionScore = 0.0;
        var triggeredRules = new List<string>();
        var evidence = new List<string>(context.Evidence);
        var suppress = false;

        if (context.ActiveApplication is not null &&
            DistractionApps.Contains(context.ActiveApplication))
        {
            distractionScore += 40;
            triggeredRules.Add("DistractionAppDetected");
            evidence.Add($"Distraction app active: {context.ActiveApplication}");
        }

        if (context.GoalAlignmentPercent < 50)
        {
            distractionScore += 30;
            triggeredRules.Add("LowGoalAlignment");
            evidence.Add($"Goal alignment below 50%: {context.GoalAlignmentPercent:F0}%");
        }

        if (context.TimeSinceLastDistraction is { TotalMinutes: < 15 })
        {
            distractionScore += 20;
            triggeredRules.Add("RecentDistraction");
            evidence.Add("Distraction within last 15 minutes");
        }

        if (context.FocusLevel == "High" && context.RiskLevel == "Low")
        {
            suppress = true;
            triggeredRules.Add("DeepWorkProtection");
            evidence.Add("User in deep work — suppress non-critical interventions");
        }

        if (!settings.MonitoringEnabled)
        {
            suppress = true;
            triggeredRules.Add("MonitoringDisabled");
        }

        return new RuleEvaluationResult
        {
            DistractionScore = distractionScore,
            ShouldSuppressIntervention = suppress,
            TriggeredRules = triggeredRules,
            Evidence = evidence
        };
    }
}
