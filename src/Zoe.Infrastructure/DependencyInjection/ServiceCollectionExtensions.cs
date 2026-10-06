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

        // Registers IDbContextFactory<ZoeDbContext> (singleton) and ZoeDbContext (scoped).
        services.AddDbContextFactory<ZoeDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddScoped<IEventStore, EventStoreRepository>();
        services.AddScoped<IEventHandler, EventStoreHandler>();

        // Factory-backed so WPF singleton view-models can safely resolve these.
        services.AddSingleton<IGoalRepository, SqliteGoalRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IMemoryRepository, SqliteMemoryRepository>();

        services.AddScoped<IContextService, ContextService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IDecisionEngine, DecisionEngine>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IDataExportService, DataExportService>();
        services.AddScoped<IInterventionDeliveryService, InterventionDeliveryService>();

        // Hosts with a richer notifier (e.g. the WPF app's toasts) register their own after this.
        services.AddSingleton<INotificationService, ConsoleNotificationService>();

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
        var created = await dbContext.Database.EnsureCreatedAsync();
        if (!created)
        {
            // EnsureCreated does not evolve existing databases — add personal-state tables if missing.
            await EnsurePersonalStateTablesAsync(dbContext);
        }
    }

    private static async Task EnsurePersonalStateTablesAsync(ZoeDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Goals" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Goals" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Priority" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "CompletedAt" INTEGER NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Goals_IsActive" ON "Goals" ("IsActive");
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "UserSettings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UserSettings" PRIMARY KEY,
                "MonitoringEnabled" INTEGER NOT NULL,
                "AutoStartWithWindows" INTEGER NOT NULL,
                "WorkDayStart" INTEGER NOT NULL,
                "WorkDayEnd" INTEGER NOT NULL,
                "LifeProfile" TEXT NOT NULL,
                "InterventionCooldownMinutes" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Memories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Memories" PRIMARY KEY,
                "Category" TEXT NOT NULL,
                "Summary" TEXT NOT NULL,
                "RelevanceScore" REAL NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "LastReferencedAt" INTEGER NULL,
                "ReferenceCount" INTEGER NOT NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Memories_Category" ON "Memories" ("Category");
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Memories_RelevanceScore" ON "Memories" ("RelevanceScore");
            """);
    }
}
