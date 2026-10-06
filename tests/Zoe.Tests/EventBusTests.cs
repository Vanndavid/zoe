using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;
using Zoe.Infrastructure.DependencyInjection;
using Zoe.Infrastructure.Events;
using Zoe.Infrastructure.Persistence;

namespace Zoe.Tests;

public class EventBusTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-bus-test-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddScoped<IEventHandler, EventStoreHandler>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();

        TestDatabase.Delete(_databasePath);
    }

    [Fact]
    public async Task PublishAsync_PersistsEventThroughHandlerPipeline()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var activityEvent = ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "Chrome",
                ["windowTitle"] = "GitHub"
            }));

        await eventBus.PublishAsync(activityEvent);

        var events = await eventStore.GetByTimeRangeAsync(
            activityEvent.Timestamp.AddMinutes(-1),
            activityEvent.Timestamp.AddMinutes(1));

        var stored = Assert.Single(events);
        Assert.Equal("Chrome", stored.Payload.Get("application"));
    }

    [Fact]
    public async Task PublishAsync_PersistsMultipleEventTypesInSequence()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var baseTime = DateTimeOffset.UtcNow;

        await eventBus.PublishAsync(ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string> { ["application"] = "VS Code" }),
            timestamp: baseTime));

        await eventBus.PublishAsync(ActivityEvent.Create(
            EventType.IdleStarted,
            "IdleMonitor",
            timestamp: baseTime.AddMinutes(5)));

        await eventBus.PublishAsync(ActivityEvent.Create(
            EventType.IdleEnded,
            "IdleMonitor",
            timestamp: baseTime.AddMinutes(10)));

        var events = await eventStore.GetByTimeRangeAsync(
            baseTime.AddMinutes(-1),
            baseTime.AddMinutes(15));

        Assert.Equal(3, events.Count);
        Assert.Equal(EventType.WindowChanged, events[0].Type);
        Assert.Equal(EventType.IdleStarted, events[1].Type);
        Assert.Equal(EventType.IdleEnded, events[2].Type);
    }
}

public class EventSchemaTests
{
    [Fact]
    public void EventPayload_RoundTripsDictionaryValues()
    {
        var payload = EventPayload.FromDictionary(new Dictionary<string, string>
        {
            ["application"] = "Visual Studio",
            ["windowTitle"] = "Zoe.sln"
        });

        Assert.Equal("Visual Studio", payload.Get("application"));
        Assert.Equal("Zoe.sln", payload.Get("windowTitle"));

        var updated = payload.With("processId", "1234");
        Assert.Equal("1234", updated.Get("processId"));
        Assert.Equal("Visual Studio", updated.Get("application"));
    }

    [Fact]
    public void ActivityEvent_Create_SetsExpectedDefaults()
    {
        var activityEvent = ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            confidence: 0.95);

        Assert.NotEqual(Guid.Empty, activityEvent.EventId);
        Assert.Equal(EventType.WindowChanged, activityEvent.Type);
        Assert.Equal("WindowMonitor", activityEvent.Source);
        Assert.Equal(0.95, activityEvent.Confidence);
    }
}
