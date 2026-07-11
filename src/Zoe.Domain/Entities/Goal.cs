namespace Zoe.Domain.Entities;

public sealed class Goal
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int Priority { get; init; } = 1;

    public bool IsActive { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; init; }
}
