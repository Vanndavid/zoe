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
        services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddSingleton<IGoalRepository, SqliteGoalRepository>();
        services.AddScoped<IContextService, ContextService>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();

        var goalRepo = _serviceProvider.GetRequiredService<IGoalRepository>();
        await goalRepo.AddAsync(new Goal { Name = "Finish Login System", Priority = 1 });
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        TestDatabase.Delete(_databasePath);
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

    [Fact]
    public async Task GetCurrentContextAsync_StaysIdleAfterInterventionEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var contextService = scope.ServiceProvider.GetRequiredService<IContextService>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "chrome",
                ["windowTitle"] = "Reddit"
            }),
            timestamp: baseTime));
        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.IdleStarted, "IdleMonitor", timestamp: baseTime.AddMinutes(2)));
        await eventStore.AppendAsync(ActivityEvent.Create(
            EventType.InterventionTriggered, "InterventionDelivery", timestamp: baseTime.AddMinutes(5)));

        var context = await contextService.GetCurrentContextAsync();

        Assert.Equal("Idle", context.EstimatedState);
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

    [Fact]
    public void Evaluate_SuppressesWhenUserIsIdle()
    {
        var engine = new RuleEngine();
        var context = new Context
        {
            GeneratedAt = AtLocalTime(10, 0),
            ActiveApplication = "chrome",
            GoalAlignmentPercent = 30,
            FocusLevel = "Low",
            RiskLevel = "High",
            EstimatedState = "Idle",
            Evidence = []
        };

        var result = engine.Evaluate(context, new UserSettings());

        Assert.True(result.ShouldSuppressIntervention);
        Assert.Contains("UserIdle", result.TriggeredRules);
    }

    [Fact]
    public void Evaluate_SuppressesOutsideWorkHoursOnly()
    {
        var engine = new RuleEngine();
        var settings = new UserSettings { WorkDayStart = new TimeOnly(9, 0), WorkDayEnd = new TimeOnly(17, 0) };

        var evening = engine.Evaluate(DistractedAt(AtLocalTime(22, 0)), settings);
        var midMorning = engine.Evaluate(DistractedAt(AtLocalTime(10, 30)), settings);

        Assert.True(evening.ShouldSuppressIntervention);
        Assert.Contains("OutsideWorkHours", evening.TriggeredRules);
        Assert.False(midMorning.ShouldSuppressIntervention);
        Assert.DoesNotContain("OutsideWorkHours", midMorning.TriggeredRules);
    }

    private static DateTimeOffset AtLocalTime(int hour, int minute) =>
        new(DateTime.Today.AddHours(hour).AddMinutes(minute));

    private static Context DistractedAt(DateTimeOffset generatedAt) => new()
    {
        GeneratedAt = generatedAt,
        ActiveApplication = "chrome",
        GoalAlignmentPercent = 30,
        FocusLevel = "Low",
        RiskLevel = "High",
        EstimatedState = "Active",
        Evidence = []
    };
}

public class DecisionEngineTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-dec-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddScoped<IDecisionEngine, DecisionEngine>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        TestDatabase.Delete(_databasePath);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(20, true)]
    public async Task DecideAsync_AppliesCooldownFromStoredInterventions(int minutesSinceLast, bool expectIntervention)
    {
        using (var seedScope = _serviceProvider.CreateScope())
        {
            await seedScope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(ActivityEvent.Create(
                EventType.InterventionTriggered,
                "InterventionDelivery",
                timestamp: DateTimeOffset.UtcNow.AddMinutes(-minutesSinceLast)));
        }

        // A fresh scope gets a fresh DecisionEngine, as each background check does.
        using var scope = _serviceProvider.CreateScope();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<IDecisionEngine>();

        var decision = await decisionEngine.DecideAsync(
            new Context { ActiveApplication = "chrome", GoalAlignmentPercent = 30, FocusLevel = "Low" },
            new RuleEvaluationResult { DistractionScore = 90 },
            new UserSettings { InterventionCooldownMinutes = 15 });

        Assert.Equal(expectIntervention, decision is not null);
    }

    [Fact]
    public async Task DecideAsync_CapsInterventionsPerHour()
    {
        using (var seedScope = _serviceProvider.CreateScope())
        {
            var eventStore = seedScope.ServiceProvider.GetRequiredService<IEventStore>();
            foreach (var minutesAgo in new[] { 50, 35, 20 })
            {
                await eventStore.AppendAsync(ActivityEvent.Create(
                    EventType.InterventionTriggered,
                    "InterventionDelivery",
                    timestamp: DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)));
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<IDecisionEngine>();

        var decision = await decisionEngine.DecideAsync(
            new Context { ActiveApplication = "chrome", GoalAlignmentPercent = 30, FocusLevel = "Low" },
            new RuleEvaluationResult { DistractionScore = 90 },
            new UserSettings { InterventionCooldownMinutes = 15 });

        Assert.Null(decision);
    }
}

public class MemoryServiceTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-mem-{Guid.NewGuid():N}.db");
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ZoeDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddSingleton<IMemoryRepository, SqliteMemoryRepository>();
        services.AddScoped<IMemoryService, MemoryService>();

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        TestDatabase.Delete(_databasePath);
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
