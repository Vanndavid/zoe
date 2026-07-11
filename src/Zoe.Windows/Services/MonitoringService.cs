using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;
using Zoe.Windows.Monitors;

namespace Zoe.Windows.Services;

[SupportedOSPlatform("windows")]
public sealed class MonitoringService : IMonitoringService
{
    private readonly WindowMonitor _windowMonitor;
    private readonly IdleMonitor _idleMonitor;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(
        WindowMonitor windowMonitor,
        IdleMonitor idleMonitor,
        ILogger<MonitoringService> logger)
    {
        _windowMonitor = windowMonitor;
        _idleMonitor = idleMonitor;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public Guid? CurrentSessionId { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        CurrentSessionId = Guid.NewGuid();
        _windowMonitor.SetSessionId(CurrentSessionId);
        _idleMonitor.SetSessionId(CurrentSessionId);

        await _windowMonitor.StartAsync(cancellationToken);
        await _idleMonitor.StartAsync(cancellationToken);

        IsRunning = true;
        _logger.LogInformation("Monitoring started with session {SessionId}", CurrentSessionId);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return;
        }

        await _windowMonitor.StopAsync(cancellationToken);
        await _idleMonitor.StopAsync(cancellationToken);

        IsRunning = false;
        _logger.LogInformation("Monitoring stopped for session {SessionId}", CurrentSessionId);
        CurrentSessionId = null;
    }
}

[SupportedOSPlatform("windows")]
public static class WindowsServiceCollectionExtensions
{
    public static IServiceCollection AddZoeWindowsMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<WindowMonitor>();
        services.AddSingleton<IdleMonitor>();
        services.AddSingleton<IWindowMonitor>(provider => provider.GetRequiredService<WindowMonitor>());
        services.AddSingleton<IIdleMonitor>(provider => provider.GetRequiredService<IdleMonitor>());
        services.AddSingleton<IMonitoringService, MonitoringService>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();
        return services;
    }
}
