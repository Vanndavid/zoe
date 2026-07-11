using System.Collections.ObjectModel;
using Zoe.Application.Interfaces;
using Zoe.Domain.Enums;

namespace Zoe.UI.ViewModels;

public sealed class TimelineViewModel
{
    private readonly ITimelineService _timelineService;

    public TimelineViewModel(ITimelineService timelineService)
    {
        _timelineService = timelineService;
        Items = new ObservableCollection<TimelineItemViewModel>();
    }

    public ObservableCollection<TimelineItemViewModel> Items { get; }

    public async Task LoadTodayAsync()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var from = new DateTimeOffset(today, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await _timelineService.GetTimelineAsync(from, to);

        Items.Clear();
        foreach (var activityEvent in events)
        {
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
