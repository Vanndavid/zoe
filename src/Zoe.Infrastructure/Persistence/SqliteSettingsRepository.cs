using Microsoft.EntityFrameworkCore;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;

namespace Zoe.Infrastructure.Persistence;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly IDbContextFactory<ZoeDbContext> _dbContextFactory;

    public SqliteSettingsRepository(IDbContextFactory<ZoeDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<UserSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.UserSettings
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.UserSettings
            .FirstOrDefaultAsync(row => row.Id == settings.Id, cancellationToken);

        if (existing is null)
        {
            // Keep a single settings document: replace any older row when Id changes.
            var previous = await dbContext.UserSettings.ToListAsync(cancellationToken);
            if (previous.Count > 0)
            {
                dbContext.UserSettings.RemoveRange(previous);
            }

            dbContext.UserSettings.Add(settings);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(settings);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
