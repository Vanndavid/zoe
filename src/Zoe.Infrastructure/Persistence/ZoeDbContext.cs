using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;

namespace Zoe.Infrastructure.Persistence;

public sealed class ZoeDbContext : DbContext
{
    public ZoeDbContext(DbContextOptions<ZoeDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActivityEventRecord> ActivityEvents => Set<ActivityEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var payloadConverter = new ValueConverter<EventPayload, string>(
            payload => JsonSerializer.Serialize(payload.Data, (JsonSerializerOptions?)null),
            json => EventPayload.FromDictionary(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                ?? new Dictionary<string, string>()));

        var metadataConverter = new ValueConverter<EventMetadata, string>(
            metadata => JsonSerializer.Serialize(metadata.Tags, (JsonSerializerOptions?)null),
            json => EventMetadata.FromDictionary(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)
                ?? new Dictionary<string, string>()));

        modelBuilder.Entity<ActivityEventRecord>(entity =>
        {
            entity.ToTable("ActivityEvents");
            entity.HasKey(record => record.EventId);
            entity.Property(record => record.Timestamp)
                .HasConversion(
                    timestamp => timestamp.UtcTicks,
                    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
            entity.HasIndex(record => record.Timestamp);
            entity.HasIndex(record => record.Type);
            entity.HasIndex(record => record.SessionId);
            entity.Property(record => record.Source).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Type).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(record => record.Payload).HasConversion(payloadConverter).IsRequired();
            entity.Property(record => record.Metadata).HasConversion(metadataConverter).IsRequired();
        });
    }
}

public sealed class ActivityEventRecord
{
    public Guid EventId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Source { get; set; } = string.Empty;

    public EventType Type { get; set; }

    public EventPayload Payload { get; set; } = EventPayload.Empty;

    public double Confidence { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? CorrelationId { get; set; }

    public EventMetadata Metadata { get; set; } = EventMetadata.Empty;

    public static ActivityEventRecord FromDomain(ActivityEvent activityEvent) =>
        new()
        {
            EventId = activityEvent.EventId,
            Timestamp = activityEvent.Timestamp,
            Source = activityEvent.Source,
            Type = activityEvent.Type,
            Payload = activityEvent.Payload,
            Confidence = activityEvent.Confidence,
            SessionId = activityEvent.SessionId,
            CorrelationId = activityEvent.CorrelationId,
            Metadata = activityEvent.Metadata
        };

    public ActivityEvent ToDomain() =>
        new()
        {
            EventId = EventId,
            Timestamp = Timestamp,
            Source = Source,
            Type = Type,
            Payload = Payload,
            Confidence = Confidence,
            SessionId = SessionId,
            CorrelationId = CorrelationId,
            Metadata = Metadata
        };
}
