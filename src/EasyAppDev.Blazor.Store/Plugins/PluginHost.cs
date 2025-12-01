// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EasyAppDev.Blazor.Store.Plugins;

/// <summary>
/// Hosts and manages plugins for a store.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class PluginHost<TState> : IAsyncDisposable where TState : notnull
{
    private readonly List<IStorePlugin<TState>> _plugins = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginHost<TState>>? _logger;
    private bool _initialized;

    /// <summary>
    /// Creates a new plugin host.
    /// </summary>
    public PluginHost(IServiceProvider serviceProvider, ILogger<PluginHost<TState>>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    public IReadOnlyList<IStorePlugin<TState>> Plugins => _plugins.AsReadOnly();

    /// <summary>
    /// Registers a plugin instance.
    /// </summary>
    public PluginHost<TState> Register(IStorePlugin<TState> plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (_initialized)
        {
            throw new InvalidOperationException("Cannot register plugins after initialization");
        }

        if (_plugins.Any(p => p.Name == plugin.Name))
        {
            throw new InvalidOperationException($"Plugin '{plugin.Name}' is already registered");
        }

        _plugins.Add(plugin);
        _logger?.LogDebug("Plugin registered: {PluginName} v{Version}", plugin.Name, plugin.Version);
        return this;
    }

    /// <summary>
    /// Registers a plugin by type.
    /// </summary>
    public PluginHost<TState> Register<TPlugin>() where TPlugin : IStorePlugin<TState>, new()
    {
        return Register(new TPlugin());
    }

    /// <summary>
    /// Discovers and registers plugins from an assembly.
    /// </summary>
    public PluginHost<TState> DiscoverPlugins(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var pluginTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => typeof(IStorePlugin<TState>).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in pluginTypes)
        {
            try
            {
                var plugin = (IStorePlugin<TState>)Activator.CreateInstance(type)!;
                Register(plugin);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to instantiate plugin type {Type}", type.FullName);
            }
        }

        return this;
    }

    /// <summary>
    /// Configures all plugins with the store builder.
    /// </summary>
    internal void ConfigurePlugins(StoreBuilder<TState> builder)
    {
        ValidateDependencies();

        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Configure(builder, _serviceProvider);

                var middleware = plugin.GetMiddleware();
                if (middleware != null)
                {
                    builder.WithMiddleware(middleware);
                }

                _logger?.LogDebug("Plugin configured: {PluginName}", plugin.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to configure plugin: {PluginName}", plugin.Name);
                throw;
            }
        }
    }

    /// <summary>
    /// Initializes all plugins after the store is created.
    /// </summary>
    internal async Task InitializePluginsAsync(IStore<TState> store)
    {
        if (_initialized) return;

        foreach (var plugin in _plugins)
        {
            try
            {
                await plugin.OnStoreCreatedAsync(store).ConfigureAwait(false);
                _logger?.LogDebug("Plugin initialized: {PluginName}", plugin.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize plugin: {PluginName}", plugin.Name);
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// Gets a plugin by name.
    /// </summary>
    public IStorePlugin<TState>? GetPlugin(string name)
    {
        return _plugins.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Gets a plugin by type.
    /// </summary>
    public TPlugin? GetPlugin<TPlugin>() where TPlugin : IStorePlugin<TState>
    {
        return _plugins.OfType<TPlugin>().FirstOrDefault();
    }

    private void ValidateDependencies()
    {
        var pluginNames = new HashSet<string>(_plugins.Select(p => p.Name));

        foreach (var plugin in _plugins)
        {
            foreach (var dependency in plugin.Dependencies)
            {
                if (!pluginNames.Contains(dependency))
                {
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.Name}' requires missing dependency: '{dependency}'");
                }
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                await plugin.OnStoreDisposingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing plugin: {PluginName}", plugin.Name);
            }
        }

        _plugins.Clear();
    }
}
