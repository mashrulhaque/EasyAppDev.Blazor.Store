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
    /// Applies default middleware configuration: JSRuntime, DevTools, and Logging.
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
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();

        return builder.WithJSRuntime(jsRuntime)
                      .WithDevTools(storeName ?? typeof(TState).Name)
                      .WithLogging();
    }

    /// <summary>
    /// Adds persistence middleware with automatic LocalStorageProvider creation.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving IJSRuntime.</param>
    /// <param name="key">The storage key for persisting state.</param>
    /// <returns>The configured builder for chaining.</returns>
    public static StoreBuilder<TState> WithPersistence<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string key)
        where TState : notnull
    {
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
        var localStorage = new LocalStorageProvider(jsRuntime);
        return builder.WithPersistence(localStorage, key);
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
