// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Extension methods for registering query services.
/// </summary>
public static class QueryServiceExtensions
{
    /// <summary>
    /// Adds the query client and related services to the service collection.
    /// </summary>
    /// <remarks>
    /// The <see cref="IQueryClient"/> is registered as a scoped service so that on
    /// Blazor Server each user circuit gets its own isolated query cache (a singleton
    /// would leak cached data across users). On Blazor WebAssembly the scope is the
    /// application, so behavior is unchanged (effectively a singleton per app).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(this IServiceCollection services)
    {
        return services.AddQueryClient(_ => { });
    }

    /// <summary>
    /// Adds the query client with custom options.
    /// </summary>
    /// <remarks>
    /// The <see cref="IQueryClient"/> is registered as a scoped service so that on
    /// Blazor Server each user circuit gets its own isolated query cache (a singleton
    /// would leak cached data across users). On Blazor WebAssembly the scope is the
    /// application, so behavior is unchanged (effectively a singleton per app).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure query client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(
        this IServiceCollection services,
        Action<QueryClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddScoped<IQueryClient>(CreateQueryClient);

        return services;
    }

    /// <summary>
    /// Adds the query client with a configuration section.
    /// </summary>
    /// <remarks>
    /// The <see cref="IQueryClient"/> is registered as a scoped service so that on
    /// Blazor Server each user circuit gets its own isolated query cache (a singleton
    /// would leak cached data across users). On Blazor WebAssembly the scope is the
    /// application, so behavior is unchanged (effectively a singleton per app).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryClient(
        this IServiceCollection services,
        QueryClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddScoped<IQueryClient>(CreateQueryClient);

        return services;
    }

    /// <summary>
    /// Creates the scoped <see cref="QueryClient"/>, resolving the optional
    /// <see cref="IJSRuntime"/> when available so window-focus / reconnect
    /// refetching works in browser hosts. In non-browser environments (unit
    /// tests, console apps) the JS runtime is simply absent and the client
    /// works without window-event refetching.
    /// </summary>
    private static QueryClient CreateQueryClient(IServiceProvider sp) => new(
        sp.GetService<IOptions<QueryClientOptions>>(),
        sp.GetService<ILogger<QueryClient>>(),
        sp.GetService<IJSRuntime>());
}
