using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;

namespace Zoe.Infrastructure.Services;

/// <summary>
/// Periodically asks the coach whether to intervene, while activity monitoring is running.
/// </summary>
public sealed class CoachingLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMonitoringService _monitoringService;
    private readonly ILogger<CoachingLoopService> _logger;
    private readonly TimeSpan _interval;

    public CoachingLoopService(
        IServiceScopeFactory scopeFactory,
        IMonitoringService monitoringService,
        ILogger<CoachingLoopService> logger,
        TimeSpan interval)
    {
        _scopeFactory = scopeFactory;
        _monitoringService = monitoringService;
        _logger = logger;
        _interval = interval;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_monitoringService.IsRunning)
        {
            return;
        }

        try
        {
            // Fresh scope per check, matching how the event bus resolves scoped services.
            using var scope = _scopeFactory.CreateScope();
            var deliveryService = scope.ServiceProvider.GetRequiredService<IInterventionDeliveryService>();
            await deliveryService.EvaluateAndDeliverAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Coaching check failed; retrying on the next tick");
        }
    }

    // Run off the caller's synchronization context so a UI host's dispatcher never runs checks.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => LoopAsync(stoppingToken), CancellationToken.None);

    private async Task LoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Coaching loop started (every {Interval})", _interval);
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
