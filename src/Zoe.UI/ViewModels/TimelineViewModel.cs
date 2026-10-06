using System.Collections.ObjectModel;
using Zoe.Application.Interfaces;
using Zoe.Domain.Enums;

namespace Zoe.UI.ViewModels;

public sealed class TimelineViewModel
{
    private readonly ITimelineService _timelineService;
    private readonly HashSet<Guid> _shownEventIds = [];
    private DateTime _shownDay;

    public TimelineViewModel(ITimelineService timelineService)
    {
        _timelineService = timelineService;
        Items = new ObservableCollection<TimelineItemViewModel>();
    }

    public ObservableCollection<TimelineItemViewModel> Items { get; }

    /// <summary>
    /// Shows today's events (local day). Safe to call repeatedly: only events not yet
    /// shown are appended, so a live refresh keeps the list's scroll position.
    /// </summary>
    public async Task LoadTodayAsync()
    {
        var today = DateTime.Today;
        var from = new DateTimeOffset(today);
        var to = new DateTimeOffset(today.AddDays(1));

        var events = await _timelineService.GetTimelineAsync(from, to);

        if (today != _shownDay)
        {
            Items.Clear();
            _shownEventIds.Clear();
            _shownDay = today;
        }

        foreach (var activityEvent in events)
        {
            if (!_shownEventIds.Add(activityEvent.EventId))
            {
                continue;
            }

            Items.Add(new TimelineItemViewModel
            {
                Time = activityEvent.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                Type = activityEvent.Type.ToString(),
                Application = activityEvent.Payload.Get("application") ?? "-",
                Details = activityEvent.Payload.Get("windowTitle") ?? "-"
            });
        }
    }
}

public sealed class TimelineItemViewModel
{
    public string Time { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Application { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;
}
