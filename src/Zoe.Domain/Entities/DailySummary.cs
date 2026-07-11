namespace Zoe.Domain.Entities;

public sealed class DailySummary
{
    public DateOnly Date { get; init; }

    public TimeSpan TotalActiveTime { get; init; }

    public TimeSpan TotalIdleTime { get; init; }

    public TimeSpan FocusTime { get; init; }

    public double GoalAlignmentPercent { get; init; }

    public IReadOnlyList<AppUsageSummary> TopApplications { get; init; } = [];

    public IReadOnlyList<string> Highlights { get; init; } = [];

    public string NarrativeSummary { get; init; } = string.Empty;
}

public sealed class AppUsageSummary
{
    public string Application { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    public int EventCount { get; init; }
}

public sealed class WeeklySummary
{
    public DateOnly WeekStart { get; init; }

    public DateOnly WeekEnd { get; init; }

    public TimeSpan TotalFocusTime { get; init; }

    public double AverageGoalAlignmentPercent { get; init; }

    public IReadOnlyList<DailySummary> DailySummaries { get; init; } = [];

    public IReadOnlyList<string> Patterns { get; init; } = [];

    public string NarrativeSummary { get; init; } = string.Empty;
}
