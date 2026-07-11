using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Zoe.Application.Interfaces;
using Zoe.Application.Services;
using Zoe.Infrastructure.Events;
using Zoe.Infrastructure.Persistence;
using Zoe.Infrastructure.Services;

namespace Zoe.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZoeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ZoeDatabase")
            ?? "Data Source=zoe.db";

        services.AddDbContext<ZoeDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddScoped<IEventHandler, EventStoreHandler>();

        services.AddSingleton<IGoalRepository, InMemoryGoalRepository>();
        services.AddSingleton<ISettingsRepository, InMemorySettingsRepository>();

        services.AddScoped<IContextService, ContextService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IDecisionEngine, DecisionEngine>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddScoped<IDataExportService, DataExportService>();

        return services;
    }

    public static IServiceCollection AddZoeLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger(), dispose: true);
        });

        return services;
    }

    public static async Task EnsureZoeDatabaseCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZoeDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
