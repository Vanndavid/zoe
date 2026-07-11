using Zoe.Domain.Enums;

namespace Zoe.Domain.Entities;

public sealed class Intervention
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public InterventionAction Action { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public double Confidence { get; init; }

    public bool WasDelivered { get; init; }
}
