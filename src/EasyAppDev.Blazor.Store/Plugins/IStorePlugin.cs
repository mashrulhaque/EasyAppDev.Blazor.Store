// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.Plugins;

/// <summary>
/// Defines a plugin that can extend store functionality.
/// Plugins can add middleware, respond to lifecycle events, and provide custom features.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public interface IStorePlugin<TState> where TState : notnull
{
    /// <summary>
    /// Gets the unique name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Gets the plugin dependencies (names of required plugins).
    /// </summary>
    IReadOnlyList<string> Dependencies => Array.Empty<string>();

    /// <summary>
    /// Gets the middleware provided by this plugin, if any.
    /// </summary>
    IMiddleware<TState>? GetMiddleware() => null;

    /// <summary>
    /// Called when the plugin is configured during store building.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <param name="services">The service provider.</param>
    void Configure(StoreBuilder<TState> builder, IServiceProvider services) { }

    /// <summary>
    /// Called when the store is created and ready.
    /// </summary>
    /// <param name="store">The created store.</param>
    Task OnStoreCreatedAsync(IStore<TState> store) => Task.CompletedTask;

    /// <summary>
    /// Called before each state update.
    /// </summary>
    /// <param name="currentState">The current state.</param>
    /// <param name="action">The action name.</param>
    Task OnBeforeUpdateAsync(TState currentState, string? action) => Task.CompletedTask;

    /// <summary>
    /// Called after each state update.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="action">The action name.</param>
    Task OnAfterUpdateAsync(TState previousState, TState newState, string? action) => Task.CompletedTask;

    /// <summary>
    /// Called when the store is being disposed.
    /// </summary>
    Task OnStoreDisposingAsync() => Task.CompletedTask;
}

/// <summary>
/// Base implementation of IStorePlugin with common functionality.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public abstract class StorePluginBase<TState> : IStorePlugin<TState> where TState : notnull
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    /// <inheritdoc />
    public virtual IMiddleware<TState>? GetMiddleware() => null;

    /// <inheritdoc />
    public virtual void Configure(StoreBuilder<TState> builder, IServiceProvider services) { }

    /// <inheritdoc />
    public virtual Task OnStoreCreatedAsync(IStore<TState> store) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnBeforeUpdateAsync(TState currentState, string? action) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnAfterUpdateAsync(TState previousState, TState newState, string? action) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnStoreDisposingAsync() => Task.CompletedTask;
}
