using EasyAppDev.Blazor.Store.Core;
#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics;
#endif
using EasyAppDev.Blazor.Store.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Convenience extension methods for <see cref="StoreBuilder{TState}"/> to reduce boilerplate.
/// </summary>
public static class StoreBuilderExtensions
{
    /// <summary>
    /// Applies default middleware configuration: DevTools (DEBUG only) and Logging.
    /// Works in all render modes (Server, WebAssembly, Auto) with lazy IJSRuntime resolution.
    /// DevTools are automatically disabled in Release builds for security.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="storeName">The name to display in DevTools. Defaults to the state type name.</param>
    /// <returns>The configured builder for chaining.</returns>
    public static StoreBuilder<TState> WithDefaults<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string? storeName = null)
        where TState : notnull
    {
        return builder.WithDefaults(serviceProvider, storeName, includeDevTools: true);
    }

    /// <summary>
    /// Applies default middleware configuration with explicit DevTools control.
    /// DevTools are only active in DEBUG builds and can be further controlled via the includeDevTools parameter.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="storeName">The name to display in DevTools. Defaults to the state type name.</param>
    /// <param name="includeDevTools">Whether to include DevTools (only works in DEBUG builds).</param>
    /// <returns>The configured builder for chaining.</returns>
    public static StoreBuilder<TState> WithDefaults<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string? storeName,
        bool includeDevTools)
        where TState : notnull
    {
#if DEBUG
        if (includeDevTools)
        {
            builder = builder.WithDevTools(serviceProvider, storeName ?? typeof(TState).Name);
        }
#endif
        return builder.WithLogging();
    }

    /// <summary>
    /// Adds persistence middleware with automatic LocalStorageProvider creation.
    /// Note: In Blazor Server/United, this method is disabled due to IJSRuntime scoping issues.
    /// Use AddScopedStore with persistence instead.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving IJSRuntime.</param>
    /// <param name="key">The storage key for persisting state.</param>
    /// <returns>The configured builder for chaining.</returns>
    [Obsolete("Persistence with singleton stores doesn't work in Blazor Server/United due to scoped IJSRuntime. Persistence is disabled.")]
    public static StoreBuilder<TState> WithPersistence<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string key)
        where TState : notnull
    {
        // Skip persistence in Blazor Server/United scenarios
        // IJSRuntime is scoped and cannot be resolved during singleton store creation
        Console.WriteLine($"Warning: Persistence skipped for {typeof(TState).Name}. Use AddScopedStore for persistence in Blazor Server/United.");
        return builder;
    }

    /// <summary>
    /// Adds diagnostics middleware if IDiagnosticsService is registered.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving IDiagnosticsService.</param>
    /// <returns>The configured builder for chaining.</returns>
    public static StoreBuilder<TState> WithDiagnosticsIfAvailable<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider)
        where TState : notnull
    {
#if DEBUG
        var diagnosticsService = serviceProvider.GetService<IDiagnosticsService>();
        if (diagnosticsService != null)
        {
            return builder.WithDiagnostics(diagnosticsService);
        }
#endif
        return builder;
    }
}
