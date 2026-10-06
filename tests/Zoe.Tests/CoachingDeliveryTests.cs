using Microsoft.Extensions.Logging.Abstractions;
using Zoe.Application.Interfaces;
using Zoe.Application.Services;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

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
