using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zoe.AI.DependencyInjection;
using Zoe.Application.Interfaces;
using Zoe.Domain.Entities;
using Zoe.Domain.Enums;
using Zoe.Domain.ValueObjects;
using Zoe.Infrastructure.DependencyInjection;

namespace Zoe.Tests;

/// <summary>
/// Runs the real pipeline (context → rules → decision → offline coach → delivery) against SQLite,
/// with only the notifier faked.
/// </summary>
public class CoachingPipelineTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"zoe-pipeline-{Guid.NewGuid():N}.db");
    private readonly FakeNotificationService _notifier = new();
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ZoeDatabase"] = $"Data Source={_databasePath}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddZoeInfrastructure(configuration);
        services.AddZoeAi();
        services.AddSingleton<INotificationService>(_notifier);

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.EnsureZoeDatabaseCreatedAsync();

        // All-day work hours so the result does not depend on when the test runs.
        await _serviceProvider.GetRequiredService<ISettingsRepository>().SaveAsync(new UserSettings
        {
            WorkDayStart = TimeOnly.MinValue,
            WorkDayEnd = TimeOnly.MaxValue,
            InterventionCooldownMinutes = 15
        });
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    [Fact]
    public async Task DistractionIsDeliveredOnceThenCoolsDown()
    {
        var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(ActivityEvent.Create(
            EventType.WindowChanged,
            "WindowMonitor",
            EventPayload.FromDictionary(new Dictionary<string, string>
            {
                ["application"] = "chrome",
                ["windowTitle"] = "Reddit - procrastination"
            }),
            timestamp: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var first = await EvaluateInFreshScopeAsync();
        var second = await EvaluateInFreshScopeAsync();

        Assert.NotNull(first);
        Assert.True(first.WasDelivered);
        Assert.False(string.IsNullOrWhiteSpace(first.Message));
        Assert.Null(second);
        Assert.Single(_notifier.Shown);

        using var scope = _serviceProvider.CreateScope();
        var recorded = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetByTypeAsync(EventType.InterventionTriggered);
        Assert.Equal("true", Assert.Single(recorded).Payload.Get("delivered"));
    }

    private async Task<Intervention?> EvaluateInFreshScopeAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInterventionDeliveryService>()
            .EvaluateAndDeliverAsync();
    }
}
