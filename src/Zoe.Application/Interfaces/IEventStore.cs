using Zoe.Domain.Entities;
using Zoe.Domain.Enums;

namespace Zoe.Application.Interfaces;

public interface IEventStore
{
    Task AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityEvent>> GetByTimeRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityEvent>> GetByTypeAsync(
        EventType type,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
