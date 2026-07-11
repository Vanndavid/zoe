using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;
using Zoe.Infrastructure.DependencyInjection;
using Zoe.Infrastructure.Persistence;

namespace Zoe.Tests;

public class EventStoreTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-test-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task AppendAsync_PersistsEventWithPayload()
    {
        var eventStore = _serviceProvider.GetRequiredService<IEventStore>();
        var sessionId = Guid.NewGuid();
        var activityEvent = ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "Visual Studio",
                ["windowTitle"] = "AuthenticationService.cs"
            }),
            sessionId: sessionId);

        await eventStore.AppendAsync(activityEvent);

        var events = await eventStore.GetByTimeRangeAsync(
            activityEvent.Timestamp.AddMinutes(-1),
            activityEvent.Timestamp.AddMinutes(1));

        var stored = Assert.Single(events);
        Assert.Equal(activityEvent.EventId, stored.EventId);
        Assert.Equal(EventType.WindowChanged, stored.Type);
        Assert.Equal("Visual Studio", stored.Payload.Get("application"));
        Assert.Equal(sessionId, stored.SessionId);
    }

    [Fact]
    public async Task GetByTimeRangeAsync_ReturnsEventsInChronologicalOrder()
    {
        var eventStore = _serviceProvider.GetRequiredService<IEventStore>();
        var baseTime = DateTimeOffset.UtcNow;

        var first = ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            timestamp: baseTime.AddMinutes(-2));

        var second = ActivityEvent.Create(
            EventType.IdleStarted,
            "IdleMonitor",
            timestamp: baseTime.AddMinutes(-1));

        await eventStore.AppendAsync(first);
        await eventStore.AppendAsync(second);

        var events = await eventStore.GetByTimeRangeAsync(
            baseTime.AddMinutes(-5),
            baseTime.AddMinutes(5));

        Assert.Equal(2, events.Count);
        Assert.Equal(EventType.WindowChanged, events[0].Type);
        Assert.Equal(EventType.IdleStarted, events[1].Type);
    }

    [Fact]
    public async Task GetByTypeAsync_FiltersByEventType()
    {
        var eventStore = _serviceProvider.GetRequiredService<IEventStore>();
        var baseTime = DateTimeOffset.UtcNow;

        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            timestamp: baseTime));

        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.IdleStarted,
            "IdleMonitor",
            timestamp: baseTime.AddMinutes(1)));

        var idleEvents = await eventStore.GetByTypeAsync(
            EventType.IdleStarted,
            baseTime.AddMinutes(-1),
            baseTime.AddMinutes(5));

        var stored = Assert.Single(idleEvents);
        Assert.Equal(EventType.IdleStarted, stored.Type);
    }
}
