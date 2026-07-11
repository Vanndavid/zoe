using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Events;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly List<Type> _handlerTypes = [];

    public InMemoryEventBus(IServiceScopeFactory scopeFactory, ILogger<InMemoryEventBus> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Publishing event {EventType} from {Source} at {Timestamp}",
            activityEvent.Type,
            activityEvent.Source,
            activityEvent.Timestamp);

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(activityEvent, cancellationToken);
        }
    }

    public IDisposable Subscribe<THandler>() where THandler : IEventHandler
    {
        _handlerTypes.Add(typeof(THandler));
        return new Subscription(() => _handlerTypes.Remove(typeof(THandler)));
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose() => _onDispose();
    }
}

public sealed class EventStoreHandler : IEventHandler
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventStoreHandler> _logger;

    public EventStoreHandler(IEventStore eventStore, ILogger<EventStoreHandler> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task HandleAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        await _eventStore.AppendAsync(activityEvent, cancellationToken);
        _logger.LogInformation(
            "Persisted event {EventType} ({EventId})",
            activityEvent.Type,
            activityEvent.EventId);
    }
}
