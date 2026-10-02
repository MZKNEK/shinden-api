using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ShindenAPI;

/// <summary>Dependency injection registration helpers for Shinden.NET.</summary>
public static class ShindenServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Shinden client backed by <see cref="IHttpClientFactory"/>.
    /// The factory owns the created <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public static IServiceCollection AddShinden(
        this IServiceCollection services,
        Func<ConfigBuilder, Config> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        services.AddHttpClient();
        services.Add(new ServiceDescriptor(typeof(Shinden), serviceProvider =>
        {
            var config = configure(new ConfigBuilder())
                ?? throw new InvalidOperationException("The Shinden configuration cannot be null.");
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var logger = serviceProvider.GetService<ILogger<Shinden>>();
            return new Shinden(config, logger!, client: factory.CreateClient());
        }, lifetime));

        return services;
    }
}
