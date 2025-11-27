using Microsoft.Extensions.DependencyInjection;

namespace PostHog.AI;

/// <summary>
/// DI helpers for the AI package.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers AI tracking services. Assumes <see cref="IPostHogClient"/> is already registered.
    /// </summary>
    public static IServiceCollection AddPostHogAi(this IServiceCollection services)
    {
        #if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(services);
        #else
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        #endif

        services.AddSingleton<IAiEventTracker, AiEventTracker>();
        return services;
    }
}
