using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyMusic.Common.Services.BackgroundJobs;

public static class BackgroundJobServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="T"/> as a singleton and exposes that same instance as an
    /// <see cref="IQueuedBackgroundJob"/>, without starting it as a hosted service.
    /// </summary>
    public static IServiceCollection AddBackgroundJob<T>(this IServiceCollection services)
        where T : class, IQueuedBackgroundJob
    {
        services.AddSingleton<T>();
        services.AddSingleton<IQueuedBackgroundJob>(sp => sp.GetRequiredService<T>());

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="T"/> as a singleton started as a hosted service, and exposes that same instance as
    /// an <see cref="IQueuedBackgroundJob"/>.
    /// </summary>
    public static IServiceCollection AddHostedBackgroundJob<T>(this IServiceCollection services)
        where T : class, IHostedService, IQueuedBackgroundJob
    {
        services.AddBackgroundJob<T>();
        services.AddHostedService(sp => sp.GetRequiredService<T>());

        return services;
    }
}
