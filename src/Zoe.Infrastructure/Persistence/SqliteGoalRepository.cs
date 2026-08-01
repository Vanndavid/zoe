using Microsoft.EntityFrameworkCore;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Persistence;

public sealed class SqliteGoalRepository : IGoalRepository
{
    private readonly IDbContextFactory<ZoeDbContext> _dbContextFactory;

    public SqliteGoalRepository(IDbContextFactory<ZoeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Goals
            .AsNoTracking()
            .OrderBy(goal => goal.Priority)
            .ThenBy(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(goal => goal.Id == id, cancellationToken);
    }

    public async Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Goals.FirstOrDefaultAsync(g => g.Id == goal.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        dbContext.Entry(existing).CurrentValues.SetValues(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Goals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        dbContext.Goals.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
