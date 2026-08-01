using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zoe.Application.Interfaces;
using Zoe.Application.Services;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;
using Zoe.Infrastructure.DependencyInjection;
using Zoe.Infrastructure.Persistence;

namespace Zoe.Tests;

public class PersonalStatePersistenceTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-personal-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        _serviceProvider = BuildProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task Goals_SurviveAcrossNewServiceProvider()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var goals = scope.ServiceProvider.GetRequiredService<IGoalRepository>();
            await goals.AddAsync(new Goal
            {
                Name = "Ship Zoe MVP",
                Description = "Persist personal state",
                Priority = 1
            });
        }

        await _serviceProvider.DisposeAsync();
        _serviceProvider = BuildProvider();

        using var reloadScope = _serviceProvider.CreateScope();
        var reloaded = await reloadScope.ServiceProvider
            .GetRequiredService<IGoalRepository>()
            .GetAllAsync();

        var goal = Assert.Single(reloaded);
        Assert.Equal("Ship Zoe MVP", goal.Name);
        Assert.Equal("Persist personal state", goal.Description);
        Assert.True(goal.IsActive);
    }

    [Fact]
    public async Task Settings_SurviveAcrossNewServiceProvider()
    {
        var settingsId = Guid.NewGuid();

        using (var scope = _serviceProvider.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            await settings.SaveAsync(new UserSettings
            {
                Id = settingsId,
                LifeProfile = "Engineer seeking promotion",
                InterventionCooldownMinutes = 20,
                AutoStartWithWindows = false,
                WorkDayStart = new TimeOnly(8, 30),
                WorkDayEnd = new TimeOnly(18, 0)
            });
        }

        await _serviceProvider.DisposeAsync();
        _serviceProvider = BuildProvider();

        using var reloadScope = _serviceProvider.CreateScope();
        var loaded = await reloadScope.ServiceProvider
            .GetRequiredService<ISettingsRepository>()
            .GetAsync();

        Assert.NotNull(loaded);
        Assert.Equal(settingsId, loaded.Id);
        Assert.Equal("Engineer seeking promotion", loaded.LifeProfile);
        Assert.Equal(20, loaded.InterventionCooldownMinutes);
        Assert.False(loaded.AutoStartWithWindows);
        Assert.Equal(new TimeOnly(8, 30), loaded.WorkDayStart);
        Assert.Equal(new TimeOnly(18, 0), loaded.WorkDayEnd);
    }

    [Fact]
    public async Task Memories_SurviveAcrossNewServiceProvider()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
            var memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();
            var baseTime = DateTimeOffset.UtcNow;

            for (var i = 0; i < 5; i++)
            {
                await eventStore.AppendAsync(ActivityEvent.Create(
                    EventType.WindowChanged,
                    "WindowMonitor",
                    EventPayload.FromDictionary(new Dictionary<string, string>
                    {
                        ["application"] = "chrome",
                        ["windowTitle"] = "Reddit"
                    }),
                    timestamp: baseTime.AddMinutes(i)));
            }

            await memoryService.ExtractMemoriesFromHistoryAsync();
        }

        await _serviceProvider.DisposeAsync();
        _serviceProvider = BuildProvider();

        using var reloadScope = _serviceProvider.CreateScope();
        var memories = await reloadScope.ServiceProvider
            .GetRequiredService<IMemoryService>()
            .GetRelevantMemoriesAsync();

        Assert.Contains(memories, memory => memory.Category == "DistractionPattern");
        Assert.Contains(memories, memory => memory.Summary.Contains("chrome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureZoeDatabaseCreatedAsync_AddsPersonalStateTablesToExistingDatabase()
    {
        var upgradePath = Path.Combine(Path.GetTempPath(), $"zoe-upgrade-{Guid.NewGuid():N}.db");

        try
        {
            await using (var legacy = new ZoeDbContext(
                new DbContextOptionsBuilder<ZoeDbContext>()
                    .UseSqlite($"Data Source={upgradePath}")
                    .Options))
            {
                await legacy.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE "ActivityEvents" (
                        "EventId" TEXT NOT NULL CONSTRAINT "PK_ActivityEvents" PRIMARY KEY,
                        "Timestamp" INTEGER NOT NULL,
                        "Source" TEXT NOT NULL,
                        "Type" TEXT NOT NULL,
                        "Payload" TEXT NOT NULL,
                        "Confidence" REAL NOT NULL,
                        "SessionId" TEXT NULL,
                        "CorrelationId" TEXT NULL,
                        "Metadata" TEXT NOT NULL
                    );
                    """);
            }

            var services = new ServiceCollection();
            services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={upgradePath}"));
            await using var provider = services.BuildServiceProvider();
            await provider.EnsureZoeDatabaseCreatedAsync();

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ZoeDbContext>();
            db.Goals.Add(new Goal { Name = "After upgrade", Description = "table exists" });
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.Goals.AsNoTracking().CountAsync());
        }
        finally
        {
            if (File.Exists(upgradePath))
            {
                File.Delete(upgradePath);
            }
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddSingleton<IGoalRepository, SqliteGoalRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IMemoryRepository, SqliteMemoryRepository>();
        services.AddScoped<IMemoryService, MemoryService>();
        return services.BuildServiceProvider();
    }
}
