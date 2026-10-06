using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Zoe.Application.Interfaces;
using Zoe.Application.Services;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Infrastructure.DependencyInjection;
using Zoe.Infrastructure.Services;

namespace Zoe.Tests;

public class InterventionDeliveryServiceTests
{
    [Fact]
    public async Task EvaluateAndDeliverAsync_ShowsAndRecordsDeliveredIntervention()
    {
        var notifier = new FakeNotificationService();
        var eventBus = new CapturingEventBus();
        var service = CreateService(new FakeCoachService(SampleIntervention()), notifier, eventBus);

        var result = await service.EvaluateAndDeliverAsync();

        Assert.NotNull(result);
        Assert.True(result.WasDelivered);
        Assert.Single(notifier.Shown);
        var recorded = Assert.Single(eventBus.Published);
        Assert.Equal(EventType.InterventionTriggered, recorded.Type);
        Assert.Equal("true", recorded.Payload.Get("delivered"));
        Assert.Equal("Warn", recorded.Payload.Get("action"));
    }

    [Fact]
    public async Task EvaluateAndDeliverAsync_RecordsFailedDeliveryWithoutThrowing()
    {
        var notifier = new FakeNotificationService { ThrowOnShow = true };
        var eventBus = new CapturingEventBus();
        var service = CreateService(new FakeCoachService(SampleIntervention()), notifier, eventBus);

        var result = await service.EvaluateAndDeliverAsync();

        Assert.NotNull(result);
        Assert.False(result.WasDelivered);
        var recorded = Assert.Single(eventBus.Published);
        Assert.Equal("false", recorded.Payload.Get("delivered"));
    }

    [Fact]
    public async Task EvaluateAndDeliverAsync_DoesNothingWhenCoachDeclines()
    {
        var notifier = new FakeNotificationService();
        var eventBus = new CapturingEventBus();
        var service = CreateService(new FakeCoachService(null), notifier, eventBus);

        var result = await service.EvaluateAndDeliverAsync();

        Assert.Null(result);
        Assert.Empty(notifier.Shown);
        Assert.Empty(eventBus.Published);
    }

    private static InterventionDeliveryService CreateService(
        ICoachService coach,
        INotificationService notifier,
        IEventBus eventBus) =>
        new(coach, notifier, eventBus, NullLogger<InterventionDeliveryService>.Instance);

    private static Intervention SampleIntervention() => new()
    {
        Action = InterventionAction.Warn,
        Message = "Back to the login system?",
        Evidence = ["Distraction app active: chrome"],
        Confidence = 0.7
    };
}

internal sealed class FakeCoachService : ICoachService
{
    private readonly Intervention? _intervention;

    public FakeCoachService(Intervention? intervention) => _intervention = intervention;

    public Task<Intervention?> EvaluateInterventionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_intervention);

    public Task<string> GenerateDailyReflectionAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<string> GenerateWeeklyReviewAsync(DateOnly weekStart, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);
}

internal sealed class FakeNotificationService : INotificationService
{
    public bool ThrowOnShow { get; init; }

    public List<Intervention> Shown { get; } = [];

    public Task ShowInterventionAsync(Intervention intervention, CancellationToken cancellationToken = default)
    {
        if (ThrowOnShow)
        {
            throw new InvalidOperationException("Notifications unavailable");
        }

        Shown.Add(intervention);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class CapturingEventBus : IEventBus
{
    public List<ActivityEvent> Published { get; } = [];

    public Task PublishAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        Published.Add(activityEvent);
        return Task.CompletedTask;
    }

    public IDisposable Subscribe<THandler>() where THandler : IEventHandler => throw new NotSupportedException();
}

public class CoachingLoopServiceTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task RunOnceAsync_ChecksOnlyWhileMonitoring(bool monitoring, int expectedChecks)
    {
        var delivery = new CountingDeliveryService();
        using var provider = BuildProvider(delivery, monitoring);
        var loop = CreateLoop(provider, TimeSpan.FromMinutes(1));

        await loop.RunOnceAsync();

        Assert.Equal(expectedChecks, delivery.Calls);
    }

    [Fact]
    public async Task RunOnceAsync_SwallowsCheckFailures()
    {
        var delivery = new CountingDeliveryService { ThrowOnCall = true };
        using var provider = BuildProvider(delivery, monitoring: true);
        var loop = CreateLoop(provider, TimeSpan.FromMinutes(1));

        await loop.RunOnceAsync();

        Assert.Equal(1, delivery.Calls);
    }

    [Fact]
    public async Task StartAsync_RunsChecksOnTheInterval()
    {
        var delivery = new CountingDeliveryService();
        using var provider = BuildProvider(delivery, monitoring: true);
        var loop = CreateLoop(provider, TimeSpan.FromMilliseconds(20));

        await loop.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (delivery.Calls < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await loop.StopAsync(CancellationToken.None);

        Assert.True(delivery.Calls >= 2, $"Expected at least 2 checks, got {delivery.Calls}");
    }

    [Fact]
    public void AddZoeCoachingLoop_RegistersResolvableHostedService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Coaching:EvaluationIntervalSeconds"] = "30" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMonitoringService>(new FakeMonitoringService());
        services.AddZoeCoachingLoop(configuration);
        using var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>();

        Assert.IsType<CoachingLoopService>(Assert.Single(hostedServices));
    }

    private static ServiceProvider BuildProvider(CountingDeliveryService delivery, bool monitoring)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInterventionDeliveryService>(delivery);
        services.AddSingleton<IMonitoringService>(new FakeMonitoringService { IsRunning = monitoring });
        return services.BuildServiceProvider();
    }

    private static CoachingLoopService CreateLoop(ServiceProvider provider, TimeSpan interval) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IMonitoringService>(),
            NullLogger<CoachingLoopService>.Instance,
            interval);
}

internal sealed class CountingDeliveryService : IInterventionDeliveryService
{
    private int _calls;

    public bool ThrowOnCall { get; init; }

    public int Calls => Volatile.Read(ref _calls);

    public Task<Intervention?> EvaluateAndDeliverAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        if (ThrowOnCall)
        {
            throw new InvalidOperationException("Coach unavailable");
        }

        return Task.FromResult<Intervention?>(null);
    }
}

internal sealed class FakeMonitoringService : IMonitoringService
{
    public bool IsRunning { get; init; }

    public Guid? CurrentSessionId => null;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
