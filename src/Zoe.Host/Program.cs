using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zoe.AI.DependencyInjection;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;
using Zoe.Infrastructure.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

builder.Services.AddZoeLogging();
builder.Services.AddZoeInfrastructure(builder.Configuration);
builder.Services.AddZoeAi();

using var host = builder.Build();
await host.Services.EnsureZoeDatabaseCreatedAsync();

var eventBus = host.Services.GetRequiredService<IEventBus>();
var sessionId = Guid.NewGuid();
// Sample activity spans the last half hour so the context engine sees all of it.
var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);

Console.WriteLine("Zoe Foundation Harness");
Console.WriteLine("======================");
Console.WriteLine();

using (var scope = host.Services.CreateScope())
{
    var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
    var goalRepository = scope.ServiceProvider.GetRequiredService<IGoalRepository>();
    var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
    var contextService = scope.ServiceProvider.GetRequiredService<IContextService>();
    var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();
    var memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();
    var deliveryService = scope.ServiceProvider.GetRequiredService<IInterventionDeliveryService>();
    var dataExport = scope.ServiceProvider.GetRequiredService<IDataExportService>();

    await settingsRepository.SaveAsync(new UserSettings
    {
        LifeProfile =
            "I am a software engineer seeking promotion. I work 8h on software, " +
            "30-60min learning, 30-60min interview prep, 30min PTE practice, " +
            "and enjoy gaming/movies. I want 5-10min exercise daily.",
        // All-day work hours so the harness can demo an intervention at any time of day.
        WorkDayStart = TimeOnly.MinValue,
        WorkDayEnd = TimeOnly.MaxValue
    });

    var existingGoals = await goalRepository.GetAllAsync();
    if (existingGoals.Count == 0)
    {
        await goalRepository.AddAsync(new Goal
        {
            Name = "Finish Login System",
            Description = "Complete authentication service for promotion project",
            Priority = 1
        });
    }

    var sampleEvents = new[]
    {
        ActivityEvent.Create(EventType.WindowChanged, "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "devenv",
                ["windowTitle"] = "AuthenticationService.cs"
            }), sessionId: sessionId, timestamp: baseTime),
        ActivityEvent.Create(EventType.WindowChanged, "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "chrome",
                ["windowTitle"] = "Reddit - procrastination"
            }), sessionId: sessionId, timestamp: baseTime.AddMinutes(12)),
        ActivityEvent.Create(EventType.IdleStarted, "IdleMonitor",
            sessionId: sessionId, timestamp: baseTime.AddMinutes(20)),
        ActivityEvent.Create(EventType.IdleEnded, "IdleMonitor",
            sessionId: sessionId, timestamp: baseTime.AddMinutes(25)),
        ActivityEvent.Create(EventType.WindowChanged, "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "devenv",
                ["windowTitle"] = "AuthenticationService.cs"
            }), sessionId: sessionId, timestamp: baseTime.AddMinutes(26)),
        ActivityEvent.Create(EventType.WindowChanged, "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "chrome",
                ["windowTitle"] = "YouTube - just one more video"
            }), sessionId: sessionId, timestamp: baseTime.AddMinutes(28))
    };

    foreach (var activityEvent in sampleEvents)
    {
        await eventBus.PublishAsync(activityEvent);
    }

    var timeline = await eventStore.GetByTimeRangeAsync(
        baseTime.AddMinutes(-1), baseTime.AddMinutes(30));

    Console.WriteLine($"Timeline ({timeline.Count} events):");
    foreach (var activityEvent in timeline)
    {
        var app = activityEvent.Payload.Get("application") ?? "-";
        var title = activityEvent.Payload.Get("windowTitle") ?? "-";
        Console.WriteLine(
            $"  {activityEvent.Timestamp:HH:mm:ss}  {activityEvent.Type,-15}  {app,-18}  {title}");
    }

    Console.WriteLine();
    var context = await contextService.GetCurrentContextAsync();
    Console.WriteLine("Current Context:");
    Console.WriteLine($"  Activity: {context.CurrentActivity}");
    Console.WriteLine($"  Focus: {context.FocusLevel}");
    Console.WriteLine($"  Goal Alignment: {context.GoalAlignmentPercent:F0}%");
    Console.WriteLine($"  Risk: {context.RiskLevel}");
    Console.WriteLine($"  State: {context.EstimatedState}");

    Console.WriteLine();
    await memoryService.ExtractMemoriesFromHistoryAsync();
    var memories = await memoryService.GetRelevantMemoriesAsync(5);
    Console.WriteLine($"Memories ({memories.Count}):");
    foreach (var memory in memories)
    {
        Console.WriteLine($"  [{memory.Category}] {memory.Summary}");
    }

    Console.WriteLine();
    var intervention = await deliveryService.EvaluateAndDeliverAsync();
    Console.WriteLine(intervention is null
        ? "No intervention (on track, suppressed, or cooling down)."
        : $"Intervention {intervention.Action} delivered: {intervention.WasDelivered}");

    Console.WriteLine();
    var dailySummary = await statisticsService.GetDailySummaryAsync(DateOnly.FromDateTime(baseTime.DateTime));
    Console.WriteLine($"Daily Summary: {dailySummary.NarrativeSummary}");

    Console.WriteLine();
    var exportPath = Path.Combine(Path.GetTempPath(), "zoe-export.json");
    await dataExport.ExportEventsAsync(exportPath);
    Console.WriteLine($"Data exported to: {exportPath}");
}

// Reload personal state from a fresh scope to prove SQLite durability.
using (var reloadScope = host.Services.CreateScope())
{
    var settings = await reloadScope.ServiceProvider.GetRequiredService<ISettingsRepository>().GetAsync();
    var goals = await reloadScope.ServiceProvider.GetRequiredService<IGoalRepository>().GetAllAsync();
    var memories = await reloadScope.ServiceProvider.GetRequiredService<IMemoryService>().GetRelevantMemoriesAsync(5);

    Console.WriteLine();
    Console.WriteLine("Persisted personal state (reloaded):");
    Console.WriteLine($"  Settings profile length: {settings?.LifeProfile.Length ?? 0}");
    Console.WriteLine($"  Goals: {goals.Count} ({string.Join(", ", goals.Select(g => g.Name))})");
    Console.WriteLine($"  Memories: {memories.Count}");
}

Console.WriteLine();
Console.WriteLine("All phases verified: events -> context -> rules -> AI -> delivery -> summaries -> export -> persistence.");
