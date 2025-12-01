// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.Plugins;

/// <summary>
/// Middleware that delegates to all registered plugins.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
internal sealed class PluginMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    private readonly IReadOnlyList<IStorePlugin<TState>> _plugins;

    public PluginMiddleware(IReadOnlyList<IStorePlugin<TState>> plugins)
    {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
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
}
