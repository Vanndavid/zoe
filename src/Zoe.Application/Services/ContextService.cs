using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class ContextService : IContextService
{
    private readonly IEventStore _eventStore;
    private readonly IGoalRepository _goalRepository;

    private static readonly HashSet<string> ProductiveApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "devenv", "Code", "cursor", "WindowsTerminal", "powershell", "cmd", "notepad++"
    };

    private static readonly HashSet<string> DistractionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "Discord", "Steam", "Spotify"
    };

    public ContextService(IEventStore eventStore, IGoalRepository goalRepository)
    {
        _eventStore = eventStore;
        _goalRepository = goalRepository;
    }

    public async Task<Context> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recentEvents = await _eventStore.GetByTimeRangeAsync(
            now.AddHours(-2),
            now,
            cancellationToken);

        var goals = await _goalRepository.GetAllAsync(cancellationToken);
        var activeGoals = goals.Where(goal => goal.IsActive).ToList();

        var latestWindow = recentEvents
            .LastOrDefault(e => e.Type == EventType.WindowChanged);

        var activeApp = latestWindow?.Payload.Get("application");
        var windowTitle = latestWindow?.Payload.Get("windowTitle");

        var isIdle = recentEvents.LastOrDefault()?.Type == EventType.IdleStarted;
        var focusLevel = DetermineFocusLevel(activeApp, isIdle, recentEvents);
        var goalAlignment = CalculateGoalAlignment(activeApp, windowTitle, activeGoals);
        var risk = goalAlignment < 50 ? "High" : goalAlignment < 75 ? "Medium" : "Low";
        var state = isIdle ? "Idle" : focusLevel == "High" ? "Deep Work" : "Active";

        var lastDistraction = FindLastDistractionTime(recentEvents, now);
        var evidence = BuildEvidence(activeApp, windowTitle, activeGoals, lastDistraction);

        return new Context
        {
            CurrentActivity = isIdle ? "Idle" : activeApp ?? "Unknown",
            FocusLevel = focusLevel,
            GoalAlignmentPercent = goalAlignment,
            RiskLevel = risk,
            EstimatedState = state,
            ActiveApplication = activeApp,
            ActiveWindowTitle = windowTitle,
            TimeSinceLastDistraction = lastDistraction,
            Evidence = evidence
        };
    }

    private static string DetermineFocusLevel(
        string? activeApp,
        bool isIdle,
        IReadOnlyList<ActivityEvent> recentEvents)
    {
        if (isIdle)
        {
            return "Low";
        }

        if (activeApp is not null && ProductiveApps.Contains(activeApp))
        {
            var windowChanges = recentEvents.Count(e =>
                e.Type == EventType.WindowChanged &&
                e.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-30));

            return windowChanges <= 3 ? "High" : "Medium";
        }

        if (activeApp is not null && DistractionApps.Contains(activeApp))
        {
            return "Low";
        }

        return "Medium";
    }

    private static double CalculateGoalAlignment(
        string? activeApp,
        string? windowTitle,
        IReadOnlyList<Goal> activeGoals)
    {
        if (activeApp is null)
        {
            return 50;
        }

        if (ProductiveApps.Contains(activeApp))
        {
            return 90;
        }

        if (DistractionApps.Contains(activeApp))
        {
            var title = windowTitle ?? string.Empty;
            if (title.Contains("github", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("stackoverflow", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("docs", StringComparison.OrdinalIgnoreCase))
            {
                return 75;
            }

            return 30;
        }

        return activeGoals.Count > 0 ? 60 : 50;
    }

    private static TimeSpan? FindLastDistractionTime(
        IReadOnlyList<ActivityEvent> events,
        DateTimeOffset now)
    {
        var lastDistraction = events
            .Where(e => e.Type == EventType.WindowChanged)
            .LastOrDefault(e =>
            {
                var app = e.Payload.Get("application");
                return app is not null && DistractionApps.Contains(app);
            });

        return lastDistraction is null ? null : now - lastDistraction.Timestamp;
    }

    private static IReadOnlyList<string> BuildEvidence(
        string? activeApp,
        string? windowTitle,
        IReadOnlyList<Goal> activeGoals,
        TimeSpan? lastDistraction)
    {
        var evidence = new List<string>();

        if (activeApp is not null)
        {
            evidence.Add($"Active application: {activeApp}");
        }

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            evidence.Add($"Window title: {windowTitle}");
        }

        if (activeGoals.Count > 0)
        {
            evidence.Add($"Active goals: {string.Join(", ", activeGoals.Select(g => g.Name))}");
        }

        if (lastDistraction.HasValue)
        {
            evidence.Add($"Last distraction: {lastDistraction.Value.TotalMinutes:F0} minutes ago");
        }

        return evidence;
    }
}
