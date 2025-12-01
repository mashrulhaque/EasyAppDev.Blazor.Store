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
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a scoped store with all required utility services in a single call.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStoreWithUtilities<TState>(
        this IServiceCollection services,
        TState initialState,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddStoreUtilities();
        services.AddScopedStore(initialState, configure);
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a scoped store with all required utility services using a factory.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStoreWithUtilities<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddStoreUtilities();
        services.AddScopedStore(stateFactory, configure);
        services.AddAsyncActionExecutor<TState>();

        return services;
    }

    /// <summary>
    /// Adds a singleton store to the service collection.
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
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

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddSingleton<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddSingleton<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddSingleton<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a singleton store to the service collection using a factory.
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
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

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddSingleton<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddSingleton<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddSingleton<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection.
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        TState initialState,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var builder = StoreBuilder<TState>.Create(initialState);
            if (configure != null)
                builder = configure(builder, sp);
            return builder.Build();
        });

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddScoped<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection (legacy overload for backward compatibility).
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="initialState">The initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    [Obsolete("Use the overload that accepts Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>> for access to scoped services like IJSRuntime.")]
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        TState initialState,
        Action<StoreBuilder<TState>>? configure)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddScoped<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection using a factory.
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder. Receives the builder and service provider, returns configured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>>? configure = null)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var initialState = stateFactory(sp);
            var builder = StoreBuilder<TState>.Create(initialState);
            if (configure != null)
                builder = configure(builder, sp);
            return builder.Build();
        });

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddScoped<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a scoped store to the service collection using a factory (legacy overload for backward compatibility).
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory to create the initial state.</param>
    /// <param name="configure">Optional configuration for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    [Obsolete("Use the overload that accepts Func<StoreBuilder<TState>, IServiceProvider, StoreBuilder<TState>> for access to scoped services like IJSRuntime.")]
    public static IServiceCollection AddScopedStore<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, TState> stateFactory,
        Action<StoreBuilder<TState>>? configure)
        where TState : notnull
    {
        services.AddScoped<IStore<TState>>(sp =>
        {
            var initialState = stateFactory(sp);
            var builder = StoreBuilder<TState>.Create(initialState);
            configure?.Invoke(builder);
            return builder.Build();
        });

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddScoped<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddScoped<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds a transient store to the service collection.
    /// Creates a new store instance each time it's requested.
    /// Also registers IStateReader, IStateWriter, and IStateObservable as aliases.
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

        // Register interface aliases (required by AsyncActionExecutor and for interface segregation)
        services.AddTransient<IStateReader<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddTransient<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());
        services.AddTransient<IStateObservable<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        return services;
    }
}
