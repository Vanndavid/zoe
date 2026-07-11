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

public class ContextServiceTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-ctx-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddSingleton<IGoalRepository, InMemoryGoalRepository>();
        services.AddScoped<IContextService, ContextService>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();

        var goalRepo = _serviceProvider.GetRequiredService<IGoalRepository>();
        await goalRepo.AddAsync(new Goal { Name = "Finish Login System", Priority = 1 });
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    [Fact]
    public async Task GetCurrentContextAsync_DetectsProductiveActivity()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var contextService = scope.ServiceProvider.GetRequiredService<IContextService>();

        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "devenv",
                ["windowTitle"] = "AuthenticationService.cs"
            })));

        var context = await contextService.GetCurrentContextAsync();

        Assert.Equal("devenv", context.ActiveApplication);
        Assert.Equal("High", context.FocusLevel);
        Assert.True(context.GoalAlignmentPercent >= 80);
        Assert.NotEmpty(context.Evidence);
    }
}

public class RuleEngineTests
{
    [Fact]
    public void Evaluate_IncreasesDistractionScoreForDistractionApp()
    {
        var engine = new RuleEngine();
        var context = new Context
        {
            ActiveApplication = "chrome",
            GoalAlignmentPercent = 30,
            FocusLevel = "Low",
            RiskLevel = "High",
            Evidence = ["Active application: chrome"]
        };

        var result = engine.Evaluate(context, new UserSettings());

        Assert.True(result.DistractionScore >= 40);
        Assert.Contains("DistractionAppDetected", result.TriggeredRules);
    }

    [Fact]
    public void Evaluate_SuppressesDuringDeepWork()
    {
        var engine = new RuleEngine();
        var context = new Context
        {
            FocusLevel = "High",
            RiskLevel = "Low",
            GoalAlignmentPercent = 90,
            Evidence = []
        };

        var result = engine.Evaluate(context, new UserSettings());

        Assert.True(result.ShouldSuppressIntervention);
        Assert.Contains("DeepWorkProtection", result.TriggeredRules);
    }
}

public class MemoryServiceTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-mem-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddSingleton<IMemoryService, MemoryService>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    [Fact]
    public async Task ExtractMemoriesFromHistoryAsync_DetectsDistractionPattern()
    {
        using var scope = _serviceProvider.CreateScope();
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
        var memories = await memoryService.GetRelevantMemoriesAsync();

        Assert.Contains(memories, m => m.Category == "DistractionPattern");
    }
}
