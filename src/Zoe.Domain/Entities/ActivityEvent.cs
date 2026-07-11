using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;

namespace Zoe.Domain.Entities;

public sealed class ActivityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string Source { get; init; } = string.Empty;

    public EventType Type { get; init; }

    public EventPayload Payload { get; init; } = EventPayload.Empty;

    public double Confidence { get; init; } = 1.0;

    public Guid? SessionId { get; init; }

    public Guid? CorrelationId { get; init; }

    public EventMetadata Metadata { get; init; } = EventMetadata.Empty;

    public static ActivityEvent Create(
        EventType type,
        string source,
        EventPayload? payload = null,
        double confidence = 1.0,
        Guid? sessionId = null,
        Guid? correlationId = null,
        EventMetadata? metadata = null,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Source = source,
            Type = type,
            Payload = payload ?? EventPayload.Empty,
            Confidence = confidence,
            SessionId = sessionId,
            CorrelationId = correlationId,
            Metadata = metadata ?? EventMetadata.Empty
        };
}
