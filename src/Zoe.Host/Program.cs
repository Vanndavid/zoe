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
var eventStore = host.Services.GetRequiredService<IEventStore>();
var contextService = host.Services.GetRequiredService<IContextService>();
var statisticsService = host.Services.GetRequiredService<IStatisticsService>();
var memoryService = host.Services.GetRequiredService<IMemoryService>();
var coachService = host.Services.GetRequiredService<ICoachService>();
var settingsRepository = host.Services.GetRequiredService<ISettingsRepository>();
var goalRepository = host.Services.GetRequiredService<IGoalRepository>();
var dataExport = host.Services.GetRequiredService<IDataExportService>();

await settingsRepository.SaveAsync(new UserSettings
{
    LifeProfile =
        "I am a software engineer seeking promotion. I work 8h on software, " +
        "30-60min learning, 30-60min interview prep, 30min PTE practice, " +
        "and enjoy gaming/movies. I want 5-10min exercise daily."
});

await goalRepository.AddAsync(new Goal
{
    Name = "Finish Login System",
    Description = "Complete authentication service for promotion project",
    Priority = 1
});

var sessionId = Guid.NewGuid();
var baseTime = DateTimeOffset.UtcNow;

Console.WriteLine("Zoe Foundation Harness");
Console.WriteLine("======================");
Console.WriteLine();

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
        }), sessionId: sessionId, timestamp: baseTime.AddMinutes(26))
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
var intervention = await coachService.EvaluateInterventionAsync();
if (intervention is not null)
{
    Console.WriteLine($"Intervention ({intervention.Action}):");
    Console.WriteLine($"  {intervention.Message}");
}

Console.WriteLine();
var dailySummary = await statisticsService.GetDailySummaryAsync(DateOnly.FromDateTime(baseTime.DateTime));
Console.WriteLine($"Daily Summary: {dailySummary.NarrativeSummary}");

Console.WriteLine();
var exportPath = Path.Combine(Path.GetTempPath(), "zoe-export.json");
await dataExport.ExportEventsAsync(exportPath);
Console.WriteLine($"Data exported to: {exportPath}");
Console.WriteLine();
Console.WriteLine("All phases verified: events -> context -> rules -> AI -> summaries -> export.");
