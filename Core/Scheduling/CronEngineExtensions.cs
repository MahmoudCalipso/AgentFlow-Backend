using Microsoft.Extensions.DependencyInjection;
using AgentFlow.Backend.Core.Scheduling;

namespace AgentFlow.Backend.Core.Scheduling;

public static class CronEngineExtensions
{
    public static IServiceCollection AddCronEngine(this IServiceCollection services)
    {
        services.AddSingleton<CronEngine>();
        return services;
    }
}
