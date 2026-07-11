using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zoe.Application.Interfaces;
using Zoe.Infrastructure.Persistence;

namespace Zoe.Infrastructure.Services;

public sealed class DataExportService : IDataExportService
{
    private readonly ZoeDbContext _dbContext;

    public DataExportService(ZoeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExportEventsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var events = await _dbContext.ActivityEvents
            .AsNoTracking()
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task DeleteAllDataAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
