namespace Zoe.Domain.Entities;

public sealed class Context
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string CurrentActivity { get; init; } = string.Empty;

    public string FocusLevel { get; init; } = "Unknown";

    public double GoalAlignmentPercent { get; init; }

    public string RiskLevel { get; init; } = "Unknown";

    public string EstimatedState { get; init; } = "Unknown";

    public string? ActiveApplication { get; init; }

    public string? ActiveWindowTitle { get; init; }

    public TimeSpan? TimeSinceLastDistraction { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];
}
