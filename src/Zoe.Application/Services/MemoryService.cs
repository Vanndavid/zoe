using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Services;

public sealed class MemoryService : IMemoryService
{
    private readonly IEventStore _eventStore;
    private readonly IMemoryRepository _memoryRepository;

    private static readonly HashSet<string> DistractionApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "Discord", "Steam"
    };

    public MemoryService(IEventStore eventStore, IMemoryRepository memoryRepository)
    {
        _eventStore = eventStore;
        _memoryRepository = memoryRepository;
    }

    public Task<IReadOnlyList<Memory>> GetRelevantMemoriesAsync(
        int limit = 10,
        CancellationToken cancellationToken = default) =>
        _memoryRepository.GetRelevantAsync(limit, cancellationToken);

    public async Task ExtractMemoriesFromHistoryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await _eventStore.GetByTimeRangeAsync(
            now.AddDays(-7),
            now,
            cancellationToken);

        await ExtractProductiveHoursAsync(events, cancellationToken);
        await ExtractDistractionPatternsAsync(events, cancellationToken);
        await ExtractRecurringExcusesAsync(events, cancellationToken);
    }

    private async Task ExtractProductiveHoursAsync(
        IReadOnlyList<ActivityEvent> events,
        CancellationToken cancellationToken)
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

        await _memoryRepository.ReplaceByCategoryAsync(
            "ProductiveHours",
            new Memory
            {
                Category = "ProductiveHours",
                Summary = $"Most active hours: {string.Join(", ", hourCounts.Select(h => $"{h:D2}:00"))}",
                RelevanceScore = 0.8
            },
            cancellationToken);
    }

    private async Task ExtractDistractionPatternsAsync(
        IReadOnlyList<ActivityEvent> events,
        CancellationToken cancellationToken)
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

        await _memoryRepository.ReplaceByCategoryAsync(
            "DistractionPattern",
            new Memory
            {
                Category = "DistractionPattern",
                Summary = $"Frequent distraction: {distractions.Key} ({distractions.Count()} times this week)",
                RelevanceScore = 0.9
            },
            cancellationToken);
    }

    private async Task ExtractRecurringExcusesAsync(
        IReadOnlyList<ActivityEvent> events,
        CancellationToken cancellationToken)
    {
        var titles = events
            .Where(e => e.Type == EventType.WindowChanged)
            .Select(e => e.Payload.Get("windowTitle") ?? string.Empty)
            .Count(t => t.Contains("reddit", StringComparison.OrdinalIgnoreCase) ||
                        t.Contains("youtube", StringComparison.OrdinalIgnoreCase));

        if (titles < 3)
        {
            return;
        }

        await _memoryRepository.ReplaceByCategoryAsync(
            "RecurringExcuse",
            new Memory
            {
                Category = "RecurringExcuse",
                Summary = "Social media and entertainment browsing recurs during work hours",
                RelevanceScore = 0.85
            },
            cancellationToken);
    }
}
