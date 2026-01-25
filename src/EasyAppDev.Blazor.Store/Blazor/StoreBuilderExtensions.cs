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
    /// Requires the store to be registered as scoped (via AddScopedStore or AddScopedStoreWithUtilities)
    /// so that IJSRuntime is available during store creation.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving IJSRuntime.</param>
    /// <param name="key">The storage key for persisting state.</param>
    /// <returns>The configured builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when IJSRuntime cannot be resolved. This typically happens when using
    /// singleton store registration (AddStore) in Blazor Server. Use AddScopedStore instead.
    /// </exception>
    /// <example>
    /// <code>
    /// // Works with scoped store registration
    /// builder.Services.AddScopedStoreWithUtilities(new CounterState(0), (store, sp) => store
    ///     .WithPersistence(sp, nameof(CounterState)));
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithPersistence<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string key)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var jsRuntime = serviceProvider.GetService<IJSRuntime>();
        if (jsRuntime == null)
        {
            throw new InvalidOperationException(
                $"IJSRuntime could not be resolved for persistence of {typeof(TState).Name}. " +
                "This typically happens when using AddStore (singleton) in Blazor Server/United. " +
                "Use AddScopedStore or AddScopedStoreWithUtilities instead, which creates the store " +
                "within a circuit where IJSRuntime is available.");
        }

        var provider = new LocalStorageProvider(jsRuntime);
        return builder.WithPersistence(provider, key);
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
