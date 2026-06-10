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
    /// <summary>
    /// Enables enhanced Redux DevTools integration with full time-travel support.
    /// <para>
    /// Activation is gated at RUNTIME via <see cref="DevToolsOptions{TState}.Enabled"/>:
    /// by default DevTools are only active when a debugger is attached. Set
    /// <c>options.Enabled = true</c> to force-enable (use with care - DevTools expose
    /// application state) or <c>options.Enabled = false</c> to force-disable.
    /// </para>
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
    /// By default DevTools are only active at runtime when a debugger is attached;
    /// see <see cref="DevToolsOptions{TState}.Enabled"/>.
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
}
