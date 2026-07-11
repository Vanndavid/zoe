using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IEventStore _eventStore;
    private readonly List<Memory> _memories = [];

    private static readonly HashSet<string> DistractionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "Discord", "Steam"
    };

    public MemoryService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task<IReadOnlyList<Memory>> GetRelevantMemoriesAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var ranked = _memories
            .OrderByDescending(m => m.RelevanceScore)
            .ThenByDescending(m => m.LastReferencedAt ?? m.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<Memory>>(ranked);
    }

    public async Task ExtractMemoriesFromHistoryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await _eventStore.GetByTimeRangeAsync(
            now.AddDays(-7),
            now,
            cancellationToken);

        ExtractProductiveHours(events);
        ExtractDistractionPatterns(events);
        ExtractRecurringExcuses(events);
    }

    private void ExtractProductiveHours(IReadOnlyList<ActivityEvent> events)
    {
        var hourCounts = events
            .Where(e => e.Type == EventType.WindowChanged)
            .GroupBy(e => e.Timestamp.Hour)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        if (hourCounts.Count == 0)
        {
            return;
        }

        _memories.RemoveAll(m => m.Category == "ProductiveHours");
        _memories.Add(new Memory
        {
            Category = "ProductiveHours",
            Summary = $"Most active hours: {string.Join(", ", hourCounts.Select(h => $"{h:D2}:00"))}",
            RelevanceScore = 0.8
        });
    }

    private void ExtractDistractionPatterns(IReadOnlyList<ActivityEvent> events)
    {
        var distractions = events
            .Where(e => e.Type == EventType.WindowChanged)
            .Select(e => e.Payload.Get("application"))
            .Where(app => app is not null && DistractionApps.Contains(app))
            .GroupBy(app => app!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (distractions is null)
        {
            return;
        }

        _memories.RemoveAll(m => m.Category == "DistractionPattern");
        _memories.Add(new Memory
        {
            Category = "DistractionPattern",
            Summary = $"Frequent distraction: {distractions.Key} ({distractions.Count()} times this week)",
            RelevanceScore = 0.9
        });
    }

    private void ExtractRecurringExcuses(IReadOnlyList<ActivityEvent> events)
    {
        var titles = events
            .Where(e => e.Type == EventType.WindowChanged)
            .Select(e => e.Payload.Get("windowTitle") ?? string.Empty)
            .Where(t => t.Contains("reddit", StringComparison.OrdinalIgnoreCase) ||
                        t.Contains("youtube", StringComparison.OrdinalIgnoreCase))
            .Count();

        if (titles < 3)
        {
            return;
        }

        _memories.RemoveAll(m => m.Category == "RecurringExcuse");
        _memories.Add(new Memory
        {
            Category = "RecurringExcuse",
            Summary = "Social media and entertainment browsing recurs during work hours",
            RelevanceScore = 0.85
        });
    }
}
