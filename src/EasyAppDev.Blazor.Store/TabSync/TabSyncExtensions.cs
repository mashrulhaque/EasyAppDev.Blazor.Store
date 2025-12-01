using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.TabSync;

/// <summary>
/// Extension methods for adding cross-tab state synchronization.
/// </summary>
public static class TabSyncExtensions
{
    /// <summary>
    /// Adds cross-tab state synchronization using BroadcastChannel API.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime access.</param>
    /// <param name="configure">Optional action to configure sync options.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <remarks>
    /// Tab sync only works in WebAssembly. In Blazor Server, each user
    /// already has their own state isolated by SignalR circuit.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     CartState.Empty,
    ///     (store, sp) => store
    ///         .WithDevTools(sp, "Cart")
    ///         .WithTabSync(sp, opts => opts
    ///             .Channel("cart-sync")
    ///             .ExcludeActions("UI_STATE")
    ///             .OnReceived(action => Console.WriteLine($"Received: {action}"))
    ///         )
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithTabSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Action<TabSyncOptions>? configure = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var options = new TabSyncOptions();
        configure?.Invoke(options);

        var middleware = new TabSyncMiddleware<TState>(serviceProvider, options);
        return builder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Adds cross-tab state synchronization with a specific channel name.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">Service provider for IJSRuntime access.</param>
    /// <param name="channelName">The BroadcastChannel name to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static StoreBuilder<TState> WithTabSync<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        string channelName)
        where TState : notnull
    {
        return builder.WithTabSync(serviceProvider, opts => opts.Channel(channelName));
    }
}
