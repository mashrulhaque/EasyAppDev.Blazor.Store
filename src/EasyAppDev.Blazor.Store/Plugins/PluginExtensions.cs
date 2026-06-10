// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EasyAppDev.Blazor.Store.Plugins;

/// <summary>
/// Extension methods for using plugins with the store builder.
/// </summary>
public static class PluginExtensions
{
    /// <summary>
    /// Adds a plugin to the store.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <typeparam name="TPlugin">The type of plugin.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithPlugin<TState, TPlugin>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider)
        where TState : notnull
        where TPlugin : IStorePlugin<TState>, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var plugin = new TPlugin();
        return builder.WithPlugin(plugin, serviceProvider);
    }

    /// <summary>
    /// Adds a plugin instance to the store.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithPlugin<TState>(
        this StoreBuilder<TState> builder,
        IStorePlugin<TState> plugin,
        IServiceProvider serviceProvider)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        plugin.Configure(builder, serviceProvider);

        var middleware = plugin.GetMiddleware();
        if (middleware != null)
        {
            builder.WithMiddleware(middleware);
        }

        // Wrap the plugin in a PluginMiddleware so its lifecycle hooks
        // (OnStoreCreatedAsync, OnBeforeUpdateAsync, OnAfterUpdateAsync,
        // OnStoreDisposingAsync) actually fire.
        var pluginLogger = serviceProvider.GetService<ILogger<PluginMiddleware<TState>>>();
        builder.WithMiddleware(new PluginMiddleware<TState>(plugin, pluginLogger));

        return builder;
    }

    /// <summary>
    /// Adds plugins discovered from an assembly.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithPlugins<TState>(
        this StoreBuilder<TState> builder,
        Assembly assembly,
        IServiceProvider serviceProvider)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var logger = serviceProvider.GetService<ILogger<PluginHost<TState>>>();

        var pluginTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => typeof(IStorePlugin<TState>).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in pluginTypes)
        {
            try
            {
                var plugin = (IStorePlugin<TState>)Activator.CreateInstance(type)!;
                builder.WithPlugin(plugin, serviceProvider);
                logger?.LogDebug("Plugin registered from assembly: {PluginName}", plugin.Name);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to instantiate plugin type {Type}", type.FullName);
            }
        }

        return builder;
    }

    /// <summary>
    /// Adds a plugin with custom configuration.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <typeparam name="TPlugin">The type of plugin.</typeparam>
    /// <typeparam name="TOptions">The type of plugin options.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configure">Action to configure the plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithPlugin<TState, TPlugin, TOptions>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<TOptions> configure)
        where TState : notnull
        where TPlugin : ConfigurablePlugin<TState, TOptions>, new()
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TOptions();
        configure(options);

        var plugin = new TPlugin();
        plugin.SetOptions(options);

        return builder.WithPlugin(plugin, serviceProvider);
    }
}

/// <summary>
/// Base class for plugins that accept configuration options.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <typeparam name="TOptions">The type of options.</typeparam>
public abstract class ConfigurablePlugin<TState, TOptions> : StorePluginBase<TState>
    where TState : notnull
    where TOptions : class, new()
{
    /// <summary>
    /// Gets the plugin options.
    /// </summary>
    protected TOptions Options { get; private set; } = new();

    /// <summary>
    /// Sets the plugin options. Called internally by extension methods.
    /// </summary>
    internal void SetOptions(TOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }
}
