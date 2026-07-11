namespace Zoe.Domain.Entities;

public sealed class UserSettings
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public bool MonitoringEnabled { get; init; } = true;

    public bool AutoStartWithWindows { get; init; } = true;

    public TimeOnly WorkDayStart { get; init; } = new(9, 0);

    public TimeOnly WorkDayEnd { get; init; } = new(17, 0);

    public string LifeProfile { get; init; } = string.Empty;

    public int InterventionCooldownMinutes { get; init; } = 15;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
