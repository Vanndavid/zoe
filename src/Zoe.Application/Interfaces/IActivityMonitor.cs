using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IActivityMonitor
{
    string Name { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IWindowMonitor : IActivityMonitor
{
}

public interface IIdleMonitor : IActivityMonitor
{
}

public interface IMonitoringService
{
    bool IsRunning { get; }

    Guid? CurrentSessionId { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
