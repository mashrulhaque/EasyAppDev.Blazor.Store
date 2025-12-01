using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Extension methods for adding history/undo-redo support to stores.
/// </summary>
public static class HistoryExtensions
{
    /// <summary>
    /// Adds history tracking to the store with default options.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="historyOut">
    /// Output parameter that receives the history instance.
    /// Use this to access undo/redo functionality.
    /// </param>
    /// <returns>The builder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// IStoreHistory&lt;DocumentState&gt;? history;
    /// builder.Services.AddStore(
    ///     DocumentState.Empty,
    ///     (store, sp) => store.WithHistory(out history)
    /// );
    ///
    /// // Later in a component
    /// await history.UndoAsync();
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithHistory<TState>(
        this StoreBuilder<TState> builder,
        out IStoreHistory<TState> historyOut)
        where TState : notnull
    {
        return builder.WithHistory(null, out historyOut);
    }

    /// <summary>
    /// Adds history tracking to the store with custom options.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="configure">Action to configure history options.</param>
    /// <param name="historyOut">
    /// Output parameter that receives the history instance.
    /// </param>
    /// <returns>The builder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// IStoreHistory&lt;DocumentState&gt;? history;
    /// builder.Services.AddStore(
    ///     DocumentState.Empty,
    ///     (store, sp) => store.WithHistory(
    ///         opts => opts
    ///             .WithMaxSize(100)
    ///             .ExcludeActions("CURSOR_MOVE", "SELECTION_CHANGE")
    ///             .GroupActions(TimeSpan.FromMilliseconds(500)),
    ///         out history
    ///     )
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithHistory<TState>(
        this StoreBuilder<TState> builder,
        Action<HistoryOptions>? configure,
        out IStoreHistory<TState> historyOut)
        where TState : notnull
    {
        var options = new HistoryOptions();
        configure?.Invoke(options);

        var history = new StoreHistory<TState>(options);
        historyOut = history;

        return builder.WithMiddleware(history);
    }

    /// <summary>
    /// Registers store history as a service for dependency injection.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="history">The history instance to register.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// IStoreHistory&lt;DocumentState&gt;? history;
    /// builder.Services.AddStore(
    ///     DocumentState.Empty,
    ///     (store, sp) => store.WithHistory(out history)
    /// );
    /// builder.Services.AddStoreHistory(history!);
    ///
    /// // In a component
    /// @inject IStoreHistory&lt;DocumentState&gt; History
    ///
    /// &lt;button @onclick="History.UndoAsync" disabled="@(!History.CanUndo)"&gt;Undo&lt;/button&gt;
    /// </code>
    /// </example>
    public static IServiceCollection AddStoreHistory<TState>(
        this IServiceCollection services,
        IStoreHistory<TState> history)
        where TState : notnull
    {
        services.AddSingleton(history);
        return services;
    }

    /// <summary>
    /// Adds a store with history tracking and registers both as services.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configureHistory">Optional action to configure history options.</param>
    /// <param name="configureStore">Optional action to further configure the store.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddStoreWithHistory(
    ///     DocumentState.Empty,
    ///     opts => opts.WithMaxSize(100).ExcludeActions("CURSOR"),
    ///     (store, sp) => store.WithDevTools(sp, "Document")
    /// );
    ///
    /// // Both IStore&lt;DocumentState&gt; and IStoreHistory&lt;DocumentState&gt;
    /// // are available via DI
    /// </code>
    /// </example>
    public static IServiceCollection AddStoreWithHistory<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<HistoryOptions>? configureHistory = null,
        Action<StoreBuilder<TState>, IServiceProvider>? configureStore = null)
        where TState : notnull
    {
        var historyOptions = new HistoryOptions();
        configureHistory?.Invoke(historyOptions);

        var history = new StoreHistory<TState>(historyOptions);

        services.AddSingleton<IStoreHistory<TState>>(history);

        services.AddSingleton<IStore<TState>>(sp =>
        {
            var builder = StoreBuilder<TState>.Create(initialState)
                .WithMiddleware(history);

            configureStore?.Invoke(builder, sp);

            var store = builder.Build();

            // Initialize history with store reference
            history.Initialize(store);

            return store;
        });

        return services;
    }

    /// <summary>
    /// Adds a scoped store with history tracking.
    /// Use for Blazor Server where each circuit needs its own store.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configureHistory">Optional action to configure history options.</param>
    /// <param name="configureStore">Optional action to further configure the store.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStoreWithHistory<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<HistoryOptions>? configureHistory = null,
        Action<StoreBuilder<TState>, IServiceProvider>? configureStore = null)
        where TState : notnull
    {
        services.AddScoped(sp =>
        {
            var historyOptions = new HistoryOptions();
            configureHistory?.Invoke(historyOptions);
            return new StoreHistory<TState>(historyOptions);
        });

        services.AddScoped<IStoreHistory<TState>>(sp =>
            sp.GetRequiredService<StoreHistory<TState>>());

        services.AddScoped<IStore<TState>>(sp =>
        {
            var history = sp.GetRequiredService<StoreHistory<TState>>();

            var builder = StoreBuilder<TState>.Create(initialState)
                .WithMiddleware(history);

            configureStore?.Invoke(builder, sp);

            var store = builder.Build();

            // Initialize history with store reference
            history.Initialize(store);

            return store;
        });

        return services;
    }
}
