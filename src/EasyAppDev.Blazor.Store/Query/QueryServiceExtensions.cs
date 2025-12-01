// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Extension methods for registering query services.
/// </summary>
public static class QueryServiceExtensions
{
    /// <summary>
    /// Adds the query client and related services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(this IServiceCollection services)
    {
        return services.AddQueryClient(_ => { });
    }

    /// <summary>
    /// Adds the query client with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure query client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(
        this IServiceCollection services,
        Action<QueryClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IQueryClient, QueryClient>();

        return services;
    }

    /// <summary>
    /// Adds the query client with a configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(
        this IServiceCollection services,
        QueryClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddSingleton<IQueryClient, QueryClient>();

        return services;
    }
}
