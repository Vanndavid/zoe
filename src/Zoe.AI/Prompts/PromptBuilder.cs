using System.Text;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.AI.Prompts;

public static class PromptBuilder
{
    public static string BuildCoachingSystemPrompt(UserSettings settings) =>
        $"""
        You are Zoe, an AI personal mentor. You observe digital behavior and coach with evidence.
        Be direct, honest, and specific — never generic motivational quotes.
        Every statement must reference observable evidence from the user's activity data.
        Respect the user's life profile and goals.

        User life profile:
        {settings.LifeProfile}
        """;

    public static string BuildInterventionPrompt(
        Context context,
        RuleEvaluationResult ruleResult,
        IReadOnlyList<Memory> memories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Current context:");
        builder.AppendLine($"- Activity: {context.CurrentActivity}");
        builder.AppendLine($"- Focus: {context.FocusLevel}");
        builder.AppendLine($"- Goal alignment: {context.GoalAlignmentPercent:F0}%");
        builder.AppendLine($"- Risk: {context.RiskLevel}");
        builder.AppendLine($"- State: {context.EstimatedState}");
        builder.AppendLine();
        builder.AppendLine("Evidence:");
        foreach (var item in context.Evidence)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
        builder.AppendLine($"Distraction score: {ruleResult.DistractionScore:F0}");
        builder.AppendLine("Triggered rules: " + string.Join(", ", ruleResult.TriggeredRules));

        if (memories.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Relevant memories:");
            foreach (var memory in memories)
            {
                builder.AppendLine($"- [{memory.Category}] {memory.Summary}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            "Write a brief, evidence-based coaching message (2-3 sentences). " +
            "Reference specific apps, times, or patterns. Do not be preachy.");

        return builder.ToString();
    }

    public static string BuildDailyReflectionPrompt(DailySummary summary, UserSettings settings) =>
        $"""
        Generate a daily reflection for the user based on this data:

        Date: {summary.Date}
        Focus time: {summary.FocusTime.TotalHours:F1} hours
        Idle time: {summary.TotalIdleTime.TotalMinutes:F0} minutes
        Goal alignment: {summary.GoalAlignmentPercent:F0}%
        Top apps: {string.Join(", ", summary.TopApplications.Select(a => $"{a.Application} ({a.Duration.TotalMinutes:F0}m)"))}
        Highlights: {string.Join("; ", summary.Highlights)}

        User life profile: {settings.LifeProfile}

        Write an honest, specific reflection (3-5 sentences). Mention what went well and what to improve tomorrow.
        """;

    public static string BuildWeeklyReviewPrompt(WeeklySummary summary, UserSettings settings) =>
        $"""
        Generate a weekly review for the user:

        Week: {summary.WeekStart} to {summary.WeekEnd}
        Total focus: {summary.TotalFocusTime.TotalHours:F1} hours
        Average goal alignment: {summary.AverageGoalAlignmentPercent:F0}%
        Patterns: {string.Join("; ", summary.Patterns)}

        User life profile: {settings.LifeProfile}

        Write a strategic weekly review (4-6 sentences). Identify trends and one concrete improvement for next week.
        """;
}
