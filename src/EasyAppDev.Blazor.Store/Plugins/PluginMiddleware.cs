// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Plugins;

/// <summary>
/// Middleware that delegates store lifecycle and update hooks to registered plugins.
/// Fires <see cref="IStorePlugin{TState}.OnStoreCreatedAsync"/> when the store is
/// attached and <see cref="IStorePlugin{TState}.OnStoreDisposingAsync"/> when the
/// store disposes its middlewares.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
internal sealed class PluginMiddleware<TState> : IMiddleware<TState>, IStoreAwareMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private readonly IReadOnlyList<IStorePlugin<TState>> _plugins;
    private readonly ILogger? _logger;
    private IStore<TState>? _store;
    private bool _disposed;

    public PluginMiddleware(IReadOnlyList<IStorePlugin<TState>> plugins, ILogger? logger = null)
    {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        _logger = logger;
    }

    public PluginMiddleware(IStorePlugin<TState> plugin, ILogger? logger = null)
        : this(new[] { plugin ?? throw new ArgumentNullException(nameof(plugin)) }, logger)
    {
    }

    /// <summary>
    /// Attaches the store and fires <see cref="IStorePlugin{TState}.OnStoreCreatedAsync"/>
    /// for each plugin (fire-and-forget with error logging). Idempotent: a second
    /// call with the same store is a no-op.
    /// </summary>
    /// <param name="store">The store this middleware is attached to.</param>
    public void AttachStore(IStore<TState> store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (_store != null)
            return;

        _store = store;

        foreach (var plugin in _plugins)
        {
            var currentPlugin = plugin;
            _ = Task.Run(async () =>
            {
                try
                {
                    await currentPlugin.OnStoreCreatedAsync(store).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Plugin {PluginName} OnStoreCreatedAsync failed", currentPlugin.Name);
                }
            });
        }
    }

    public async Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnBeforeUpdateAsync(currentState, action).ConfigureAwait(false);
        }
    }

    public async Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        foreach (var plugin in _plugins)
        {
            await plugin.OnAfterUpdateAsync(previousState, currentState, action).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invoked by the store's middleware disposal pipeline. Calls
    /// <see cref="IStorePlugin{TState}.OnStoreDisposingAsync"/> on each plugin.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var plugin in _plugins)
        {
            try
            {
                await plugin.OnStoreDisposingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Plugin {PluginName} OnStoreDisposingAsync failed", plugin.Name);
            }
        }
    }
}
