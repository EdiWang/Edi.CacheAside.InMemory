using Microsoft.Extensions.DependencyInjection;

namespace Edi.CacheAside.InMemory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryCacheAside(this IServiceCollection services, Action<CacheAsideOptions>? configure = null)
    {
        services.AddMemoryCache();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ICacheAside, MemoryCacheAside>();
        return services;
    }
}