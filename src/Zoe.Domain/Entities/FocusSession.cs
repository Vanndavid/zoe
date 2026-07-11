namespace Zoe.Domain.Entities;

public sealed class FocusSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid? GoalId { get; init; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; init; }

    public string? Notes { get; init; }

    public bool IsActive => EndedAt is null;
}
