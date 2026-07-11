using Microsoft.Extensions.DependencyInjection;
using Zoe.AI.Interfaces;
using Zoe.AI.Services;
using Zoe.Application.Interfaces;

namespace Zoe.AI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZoeAi(this IServiceCollection services)
    {
        services.AddSingleton<ILlmClient, RuleBasedLlmClient>();
        services.AddScoped<ICoachService, CoachService>();
        return services;
    }
}
