using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Windows.Monitors;

[SupportedOSPlatform("windows")]
public sealed class IdleMonitor : IIdleMonitor, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<IdleMonitor> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _idleThreshold;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private Guid? _sessionId;
    private bool _isIdle;

    public IdleMonitor(
        IEventBus eventBus,
        ILogger<IdleMonitor> logger,
        TimeSpan? pollInterval = null,
        TimeSpan? idleThreshold = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        _idleThreshold = idleThreshold ?? TimeSpan.FromMinutes(2);
    }

    public string Name => "IdleMonitor";

    public void SetSessionId(Guid? sessionId) => _sessionId = sessionId;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_monitorTask is not null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation("{Monitor} started", Name);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null || _monitorTask is null)
        {
            return;
        }

        await _cts.CancelAsync();

        try
        {
            await _monitorTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _monitorTask = null;
        _logger.LogInformation("{Monitor} stopped", Name);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var idleTime = NativeMethods.GetIdleTime();
                var currentlyIdle = idleTime >= _idleThreshold;

                if (currentlyIdle && !_isIdle)
                {
                    _isIdle = true;
                    await _eventBus.PublishAsync(
                        ActivityEvent.Create(EventType.IdleStarted, Name, sessionId: _sessionId),
                        cancellationToken);
                }
                else if (!currentlyIdle && _isIdle)
                {
                    _isIdle = false;
                    await _eventBus.PublishAsync(
                        ActivityEvent.Create(EventType.IdleEnded, Name, sessionId: _sessionId),
                        cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in {Monitor} loop", Name);
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    private static class NativeMethods
    {
        public static TimeSpan GetIdleTime()
        {
            var lastInput = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

            if (!GetLastInputInfo(ref lastInput))
            {
                return TimeSpan.Zero;
            }

            var idleMilliseconds = (uint)Environment.TickCount - lastInput.Time;
            return TimeSpan.FromMilliseconds(idleMilliseconds);
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }
    }
}
