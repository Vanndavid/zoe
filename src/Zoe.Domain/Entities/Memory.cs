namespace Zoe.Domain.Entities;

public sealed class Memory
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Category { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public double RelevanceScore { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastReferencedAt { get; init; }

    public int ReferenceCount { get; init; }
}
