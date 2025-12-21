// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Extension methods for configuring enhanced DevTools in the store builder.
/// </summary>
public static class DevToolsBuilderExtensions
{
#if DEBUG
    /// <summary>
    /// Enables enhanced Redux DevTools integration with full time-travel support.
    /// WARNING: DevTools are only available in DEBUG builds. In Release builds, this method is a no-op.
    /// DevTools expose your application state and should never be used in production.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime resolution.</param>
    /// <param name="configure">Action to configure DevTools options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithEnhancedDevTools(sp, options =>
    /// {
    ///     options.Name = "MyStore";
    ///     options.EnableStateEditing = false;  // Disabled by default for security
    ///     options.MaxHistory = 50;
    ///     options.StateSanitizer = state => state with { Password = "***" };
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithEnhancedDevTools<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<DevToolsOptions<TState>>? configure = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var options = DevToolsOptions<TState>.Default();
        configure?.Invoke(options);

        var logger = serviceProvider.GetService<ILogger<EnhancedDevToolsMiddleware<TState>>>();
        var middleware = new EnhancedDevToolsMiddleware<TState>(serviceProvider, options, logger);

        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Enables enhanced Redux DevTools with default configuration.
    /// WARNING: DevTools are only available in DEBUG builds. In Release builds, this method is a no-op.
    /// DevTools expose your application state and should never be used in production.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime resolution.</param>
    /// <param name="storeName">The name to display in DevTools.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithEnhancedDevTools<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string storeName)
        where TState : notnull
    {
        return builder.WithEnhancedDevTools(serviceProvider, options =>
        {
            options.Name = storeName;
        });
    }
#else
    /// <summary>
    /// DevTools stub for Release builds. This method does nothing in production.
    /// DevTools are disabled in Release builds for security reasons.
    /// </summary>
    public static StoreBuilder<TState> WithEnhancedDevTools<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<DevToolsOptions<TState>>? configure = null)
        where TState : notnull
    {
        // No-op in Release builds
        return builder;
    }

    /// <summary>
    /// DevTools stub for Release builds. This method does nothing in production.
    /// DevTools are disabled in Release builds for security reasons.
    /// </summary>
    public static StoreBuilder<TState> WithEnhancedDevTools<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string storeName)
        where TState : notnull
    {
        // No-op in Release builds
        return builder;
    }
#endif
}
