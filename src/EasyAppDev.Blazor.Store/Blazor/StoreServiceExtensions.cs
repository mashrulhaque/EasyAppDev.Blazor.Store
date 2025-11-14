using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Extension methods for registering stores and related services with dependency injection.
/// </summary>
public static class StoreServiceExtensions
{
    /// <summary>
    /// Registers core utility services required by StoreComponent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStoreUtilities(this IServiceCollection services)
    {
        // Register utility managers as scoped (per component/connection)
        services.AddScoped<IDebounceManager, DebounceManager>();
        services.AddScoped<IThrottleManager, ThrottleManager>();
        services.AddScoped<ILazyCache, LazyCache>();

        return services;
    }

    /// <summary>
    /// Registers the async action executor for a specific state type.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncActionExecutor<TState>(this IServiceCollection services)
        where TState : notnull
    {
        services.AddScoped<IAsyncActionExecutor<TState>, AsyncActionExecutor<TState>>();
        return services;
    }

    /// <summary>
    /// Adds a singleton store with all required utility services in a single call.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStoreWithUtilities<TState>(
        this IServiceCollection services,
        TState initialState,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddStoreUtilities();
        services.AddStore(initialState, configure);

        // Register IStateWriter<TState> as an alias for IStore<TState> (required by AsyncActionExecutor)
        services.AddSingleton<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a scoped store with all required utility services in a single call.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStoreWithUtilities<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddStoreUtilities();
        services.AddScopedStore(initialState, configure);

        // Register IStateWriter<TState> as an alias for IStore<TState> (required by AsyncActionExecutor)
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a scoped store with all required utility services using a factory.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStoreWithUtilities<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddStoreUtilities();
        services.AddScopedStore(stateFactory, configure);

        // Register IStateWriter<TState> as an alias for IStore<TState> (required by AsyncActionExecutor)
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a singleton store to the service collection.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStore<TState>(
        this IServiceCollection services,
        TState initialState,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddSingleton<IStore<TState>>(sp =>
        {
            var builder = StoreBuilder<TState>.Create(initialState);
            if (configure != null)
                builder = configure(builder, sp);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds a singleton store to the service collection using a factory.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStore<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddSingleton<IStore<TState>>(sp =>
        {
            var initialState = stateFactory(sp);
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection using a factory.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var initialState = stateFactory(sp);
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds a transient store to the service collection.
    /// Creates a new store instance each time it's requested.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTransientStore<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Action<StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddTransient<IStore<TState>>(sp =>
        {
            var initialState = stateFactory(sp);
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        return services;
    }
}
