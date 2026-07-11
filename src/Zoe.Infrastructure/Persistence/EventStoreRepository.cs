using Microsoft.EntityFrameworkCore;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Infrastructure.Persistence;

namespace Zoe.Infrastructure.Persistence;

public sealed class EventStoreRepository : IEventStore
{
    private readonly ZoeDbContext _dbContext;

    public EventStoreRepository(ZoeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        _dbContext.ActivityEvents.Add(ActivityEventRecord.FromDomain(activityEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetByTimeRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.ActivityEvents
            .AsNoTracking()
            .Where(record => record.Timestamp >= from && record.Timestamp <= to)
            .OrderBy(record => record.Timestamp)
            .ToListAsync(cancellationToken);

        return records.Select(record => record.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetByTypeAsync(
        EventType type,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ActivityEvents
            .AsNoTracking()
            .Where(record => record.Type == type);

        if (from.HasValue)
        {
            query = query.Where(record => record.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(record => record.Timestamp <= to.Value);
        }

        var records = await query
            .OrderBy(record => record.Timestamp)
            .ToListAsync(cancellationToken);

        return records.Select(record => record.ToDomain()).ToList();
    }
}
