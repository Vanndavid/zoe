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

        if (context.EstimatedState == "Idle")
        {
            suppress = true;
            triggeredRules.Add("UserIdle");
            evidence.Add("User is idle — nobody to coach");
        }

        if (!IsWithinWorkHours(context.GeneratedAt, settings))
        {
            suppress = true;
            triggeredRules.Add("OutsideWorkHours");
            evidence.Add($"Outside work hours ({settings.WorkDayStart:HH:mm}-{settings.WorkDayEnd:HH:mm})");
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

    private static bool IsWithinWorkHours(DateTimeOffset at, UserSettings settings)
    {
        // Work hours are wall-clock times in the user's local time zone.
        var localTime = TimeOnly.FromDateTime(at.ToLocalTime().DateTime);
        return localTime.IsBetween(settings.WorkDayStart, settings.WorkDayEnd);
    }
}
