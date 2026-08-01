using Microsoft.EntityFrameworkCore;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Persistence;

public sealed class SqliteMemoryRepository : IMemoryRepository
{
    private readonly IDbContextFactory<ZoeDbContext> _dbContextFactory;

    public SqliteMemoryRepository(IDbContextFactory<ZoeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<Memory>> GetRelevantAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Memories
            .AsNoTracking()
            .OrderByDescending(memory => memory.RelevanceScore)
            .ThenByDescending(memory => memory.LastReferencedAt ?? memory.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceByCategoryAsync(
        string category,
        Memory memory,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Memories
            .Where(row => row.Category == category)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            dbContext.Memories.RemoveRange(existing);
        }

        dbContext.Memories.Add(memory);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
