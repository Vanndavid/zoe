using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class StatisticsService : IStatisticsService
{
    private readonly IEventStore _eventStore;

    private static readonly HashSet<string> ProductiveApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "devenv", "Code", "cursor", "WindowsTerminal", "powershell", "cmd"
    };

    public StatisticsService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<DailySummary> GetDailySummaryAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);
        var events = await _eventStore.GetByTimeRangeAsync(from, to, cancellationToken);

        var appDurations = CalculateAppDurations(events);
        var idleTime = CalculateIdleTime(events);
        var activeTime = TimeSpan.FromHours(8) - idleTime;
        if (activeTime < TimeSpan.Zero)
        {
            activeTime = TimeSpan.Zero;
        }

        var focusTime = appDurations
            .Where(pair => ProductiveApps.Contains(pair.Key))
            .Aggregate(TimeSpan.Zero, (total, pair) => total + pair.Value);

        var topApps = appDurations
            .OrderByDescending(pair => pair.Value)
            .Take(5)
            .Select(pair => new AppUsageSummary
            {
                Application = pair.Key,
                Duration = pair.Value,
                EventCount = events.Count(e =>
                    e.Type == EventType.WindowChanged &&
                    e.Payload.Get("application") == pair.Key)
            })
            .ToList();

        var goalAlignment = focusTime.TotalMinutes / Math.Max(activeTime.TotalMinutes, 1) * 100;

        return new DailySummary
        {
            Date = date,
            TotalActiveTime = activeTime,
            TotalIdleTime = idleTime,
            FocusTime = focusTime,
            GoalAlignmentPercent = Math.Round(goalAlignment, 1),
            TopApplications = topApps,
            Highlights = BuildHighlights(topApps, idleTime, focusTime),
            NarrativeSummary = BuildDailyNarrative(date, focusTime, idleTime, topApps, goalAlignment)
        };
    }

    public async Task<WeeklySummary> GetWeeklySummaryAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var dailySummaries = new List<DailySummary>();

        for (var i = 0; i < 7; i++)
        {
            dailySummaries.Add(await GetDailySummaryAsync(weekStart.AddDays(i), cancellationToken));
        }

        var totalFocus = dailySummaries.Aggregate(TimeSpan.Zero, (t, d) => t + d.FocusTime);
        var avgAlignment = dailySummaries.Average(d => d.GoalAlignmentPercent);

        return new WeeklySummary
        {
            WeekStart = weekStart,
            WeekEnd = weekStart.AddDays(6),
            TotalFocusTime = totalFocus,
            AverageGoalAlignmentPercent = Math.Round(avgAlignment, 1),
            DailySummaries = dailySummaries,
            Patterns = ExtractPatterns(dailySummaries),
            NarrativeSummary = BuildWeeklyNarrative(weekStart, totalFocus, avgAlignment, dailySummaries)
        };
    }

    private static Dictionary<string, TimeSpan> CalculateAppDurations(IReadOnlyList<ActivityEvent> events)
    {
        var durations = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var windowEvents = events
            .Where(e => e.Type == EventType.WindowChanged)
            .OrderBy(e => e.Timestamp)
            .ToList();

        for (var i = 0; i < windowEvents.Count; i++)
        {
            var current = windowEvents[i];
            var app = current.Payload.Get("application") ?? "Unknown";
            var end = i + 1 < windowEvents.Count
                ? windowEvents[i + 1].Timestamp
                : current.Timestamp.AddMinutes(5);

            var duration = end - current.Timestamp;
            durations[app] = durations.GetValueOrDefault(app) + duration;
        }

        return durations;
    }

    private static TimeSpan CalculateIdleTime(IReadOnlyList<ActivityEvent> events)
    {
        var idleTime = TimeSpan.Zero;
        DateTimeOffset? idleStart = null;

        foreach (var activityEvent in events.OrderBy(e => e.Timestamp))
        {
            if (activityEvent.Type == EventType.IdleStarted)
            {
                idleStart = activityEvent.Timestamp;
            }
            else if (activityEvent.Type == EventType.IdleEnded && idleStart.HasValue)
            {
                idleTime += activityEvent.Timestamp - idleStart.Value;
                idleStart = null;
            }
        }

        return idleTime;
    }

    private static IReadOnlyList<string> BuildHighlights(
        IReadOnlyList<AppUsageSummary> topApps,
        TimeSpan idleTime,
        TimeSpan focusTime)
    {
        var highlights = new List<string>();

        if (topApps.Count > 0)
        {
            highlights.Add($"Most used app: {topApps[0].Application} ({topApps[0].Duration.TotalMinutes:F0} min)");
        }

        highlights.Add($"Focus time: {focusTime.TotalHours:F1} hours");
        highlights.Add($"Idle time: {idleTime.TotalMinutes:F0} minutes");

        return highlights;
    }

    private static string BuildDailyNarrative(
        DateOnly date,
        TimeSpan focusTime,
        TimeSpan idleTime,
        IReadOnlyList<AppUsageSummary> topApps,
        double goalAlignment)
    {
        var topApp = topApps.FirstOrDefault()?.Application ?? "unknown apps";
        return $"On {date:yyyy-MM-dd}, you spent {focusTime.TotalHours:F1}h in focus, " +
               $"{idleTime.TotalMinutes:F0}m idle. Top app: {topApp}. Goal alignment: {goalAlignment:F0}%.";
    }

    private static IReadOnlyList<string> ExtractPatterns(IReadOnlyList<DailySummary> dailySummaries)
    {
        var patterns = new List<string>();
        var bestDay = dailySummaries.MaxBy(d => d.FocusTime);

        if (bestDay is not null)
        {
            patterns.Add($"Most productive day: {bestDay.Date:dddd} ({bestDay.FocusTime.TotalHours:F1}h focus)");
        }

        var avgIdle = dailySummaries.Average(d => d.TotalIdleTime.TotalMinutes);
        patterns.Add($"Average idle time: {avgIdle:F0} minutes/day");

        return patterns;
    }

    private static string BuildWeeklyNarrative(
        DateOnly weekStart,
        TimeSpan totalFocus,
        double avgAlignment,
        IReadOnlyList<DailySummary> dailySummaries)
    {
        var bestDay = dailySummaries.MaxBy(d => d.FocusTime);
        return $"Week of {weekStart:yyyy-MM-dd}: {totalFocus.TotalHours:F1}h total focus, " +
               $"{avgAlignment:F0}% avg goal alignment. Best day: {bestDay?.Date:dddd}.";
    }
}
