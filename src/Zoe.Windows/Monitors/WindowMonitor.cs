using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;

namespace Zoe.Windows.Monitors;

[SupportedOSPlatform("windows")]
public sealed class WindowMonitor : IWindowMonitor, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<WindowMonitor> _logger;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private Guid? _sessionId;
    private string _lastWindowKey = string.Empty;

    public WindowMonitor(
        IEventBus eventBus,
        ILogger<WindowMonitor> logger,
        TimeSpan? pollInterval = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    public string Name => "WindowMonitor";

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
                if (NativeMethods.TryGetForegroundWindowInfo(out var processName, out var windowTitle))
                {
                    var windowKey = $"{processName}|{windowTitle}";

                    if (!string.Equals(windowKey, _lastWindowKey, StringComparison.Ordinal))
                    {
                        _lastWindowKey = windowKey;

                        var activityEvent = ActivityEvent.Create(
                            EventType.WindowChanged,
                            Name,
                            EventPayload.FromDictionary(new Dictionary<string, string>
                            {
                                ["application"] = processName,
                                ["windowTitle"] = windowTitle
                            }),
                            sessionId: _sessionId);

                        await _eventBus.PublishAsync(activityEvent, cancellationToken);
                    }
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
        public static bool TryGetForegroundWindowInfo(out string processName, out string windowTitle)
        {
            processName = string.Empty;
            windowTitle = string.Empty;

            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var titleBuilder = new StringBuilder(512);
            _ = GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            windowTitle = titleBuilder.ToString();

            _ = GetWindowThreadProcessId(hwnd, out var processId);
            var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);

            if (processHandle == IntPtr.Zero)
            {
                processName = "Unknown";
                return true;
            }

            try
            {
                var exePathBuilder = new StringBuilder(1024);
                var size = exePathBuilder.Capacity;

                if (QueryFullProcessImageName(processHandle, 0, exePathBuilder, ref size))
                {
                    processName = Path.GetFileNameWithoutExtension(exePathBuilder.ToString());
                }
                else
                {
                    processName = "Unknown";
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }

            return true;
        }

        private const uint ProcessQueryLimitedInformation = 0x1000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr processHandle,
            int flags,
            StringBuilder exeName,
            ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
