using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Application.Services;

public sealed class TimelineService : ITimelineService
{
    private readonly IEventStore _eventStore;

    public TimelineService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task<IReadOnlyList<ActivityEvent>> GetTimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        _eventStore.GetByTimeRangeAsync(from, to, cancellationToken);
}
