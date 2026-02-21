using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Diagnostics;

/// <summary>
/// Extension methods for configuring diagnostics in the Blazor Store library.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Adds diagnostic services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxHistorySize">The maximum number of history entries to keep. Defaults to 100.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddStoreDiagnostics(
        this IServiceCollection services,
        int maxHistorySize = 100)
    {
        services.AddSingleton<IDiagnosticsService>(sp =>
            new DiagnosticsService(maxHistorySize));

        return services;
    }

    /// <summary>
    /// Adds diagnostics middleware to the store builder.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="diagnosticsService">The diagnostics service instance. If null, attempts to resolve from service provider.</param>
    /// <returns>The store builder for method chaining.</returns>
    /// <remarks>
    /// This middleware will collect diagnostic data about state updates, including timing,
    /// state diffs, and action metadata. Ensure that <see cref="AddStoreDiagnostics"/> has been
    /// called to register the diagnostics service before using this extension.
    /// </remarks>
    public static StoreBuilder<TState> WithDiagnostics<TState>(
        this StoreBuilder<TState> builder,
        IDiagnosticsService? diagnosticsService = null)
        where TState : notnull
    {
        // If diagnosticsService not provided, create a new instance
        diagnosticsService ??= new DiagnosticsService();

        var middleware = new DiagnosticsMiddleware<TState>(diagnosticsService);
        return builder.WithMiddleware(middleware);
    }
}
