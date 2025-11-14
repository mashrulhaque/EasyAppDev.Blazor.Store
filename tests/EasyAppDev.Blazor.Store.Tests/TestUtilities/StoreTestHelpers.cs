using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Tests.TestUtilities;

/// <summary>
/// Helper methods for creating stores in tests.
/// </summary>
public static class StoreTestHelpers
{
    /// <summary>
    /// Creates a store with the specified initial state using the builder pattern.
    /// </summary>
    /// <typeparam name="T">The type of state.</typeparam>
    /// <param name="initialState">The initial state.</param>
    /// <returns>A configured store instance.</returns>
    public static IStore<T> CreateStore<T>(T initialState) where T : notnull
        => StoreBuilder<T>.Create(initialState).Build();

    /// <summary>
    /// Creates a store with the specified initial state and custom comparer.
    /// </summary>
    /// <typeparam name="T">The type of state.</typeparam>
    /// <param name="initialState">The initial state.</param>
    /// <param name="comparer">The equality comparer for state comparison.</param>
    /// <returns>A configured store instance.</returns>
    public static IStore<T> CreateStore<T>(T initialState, IEqualityComparer<T> comparer)
        where T : notnull
        => StoreBuilder<T>.Create(initialState).WithComparer(comparer).Build();

    /// <summary>
    /// Creates a store with the specified initial state and middleware.
    /// </summary>
    /// <typeparam name="T">The type of state.</typeparam>
    /// <param name="initialState">The initial state.</param>
    /// <param name="middlewares">The middleware instances to add.</param>
    /// <returns>A configured store instance.</returns>
    public static IStore<T> CreateStoreWithMiddleware<T>(T initialState, params IMiddleware<T>[] middlewares)
        where T : notnull
    {
        var builder = StoreBuilder<T>.Create(initialState);
        foreach (var middleware in middlewares)
        {
            builder = builder.WithMiddleware(middleware);
        }
        return builder.Build();
    }

    /// <summary>
    /// Registers all required utility services for StoreComponent tests.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void RegisterStoreUtilities(IServiceCollection services)
    {
        services.AddStoreUtilities();
    }

    /// <summary>
    /// Registers all required utility services for StoreComponent tests with a specific state type.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="services">The service collection.</param>
    public static void RegisterStoreUtilities<TState>(IServiceCollection services)
        where TState : notnull
    {
        services.AddStoreUtilities();

        // Register IStateWriter<TState> as an alias for IStore<TState>
        // This is required for AsyncActionExecutor which depends on IStateWriter
        services.AddScoped<IStateWriter<TState>>(sp => sp.GetRequiredService<IStore<TState>>());

        services.AddAsyncActionExecutor<TState>();
    }
}
