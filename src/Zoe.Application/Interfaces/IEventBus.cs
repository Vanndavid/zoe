using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IEventBus
{
    Task PublishAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    IDisposable Subscribe<THandler>() where THandler : IEventHandler;
}

public interface IEventHandler
{
    Task HandleAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);
}

public interface IEventHandler<TEventType> : IEventHandler
{
}
