using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.DevTools;
using EasyAppDev.Blazor.Store.Persistence;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Fluent builder for creating and configuring <see cref="IStore{TState}"/> instances.
/// </summary>
public class StoreBuilder<TState> where TState : notnull
{
    private readonly TState _initialState;
    private IEqualityComparer<TState>? _comparer;
    private readonly List<IMiddleware<TState>> _middlewares = new();
    private ILogger<MiddlewarePipeline<TState>>? _middlewarePipelineLogger;
    private ILogger<Store<TState>>? _storeLogger;
    private ILogger<SubscriptionManager<TState>>? _subscriptionManagerLogger;
    private MiddlewarePipelineOptions? _middlewareOptions;
    private StoreErrorHandler<TState>? _errorHandler;

    private StoreBuilder(TState initialState)
    {
        _initialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
    }

    /// <summary>
    /// Creates a new store builder with the specified initial state.
    /// </summary>
    /// <param name="initialState">The initial state for the store.</param>
    /// <returns>A new store builder instance.</returns>
    public static StoreBuilder<TState> Create(TState initialState)
    {
        return new StoreBuilder<TState>(initialState);
    }

    /// <summary>
    /// Sets a custom equality comparer for state comparison.
    /// </summary>
    /// <param name="comparer">The equality comparer to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithComparer(IEqualityComparer<TState> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        return this;
    }

    /// <summary>
    /// Adds middleware to the store.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithMiddleware(IMiddleware<TState> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Adds multiple middlewares to the store.
    /// </summary>
    /// <param name="middlewares">The middlewares to add.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithMiddlewares(params IMiddleware<TState>[] middlewares)
    {
        ArgumentNullException.ThrowIfNull(middlewares);
        _middlewares.AddRange(middlewares);
        return this;
    }

    /// <summary>
    /// Enables logging middleware.
    /// </summary>
    /// <param name="logger">Optional custom logging function.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithLogging(Action<string>? logger = null)
    {
        var loggingMiddleware = logger != null
            ? new LoggingMiddleware<TState>(logger)
            : new LoggingMiddleware<TState>();

        return WithMiddleware(loggingMiddleware);
    }

    /// <summary>
    /// Sets a logger for the middleware pipeline.
    /// </summary>
    /// <param name="logger">The logger to use for the middleware pipeline.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithLogger(ILogger<MiddlewarePipeline<TState>> logger)
    {
        _middlewarePipelineLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Sets a logger for store operations.
    /// </summary>
    /// <param name="logger">The logger to use for the store.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithStoreLogger(ILogger<Store<TState>> logger)
    {
        _storeLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Sets a logger for subscription manager.
    /// </summary>
    /// <param name="logger">The logger to use for the subscription manager.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithSubscriptionManagerLogger(ILogger<SubscriptionManager<TState>> logger)
    {
        _subscriptionManagerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Sets configuration options for middleware pipeline behavior.
    /// </summary>
    /// <param name="options">The middleware pipeline options.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithMiddlewareOptions(MiddlewarePipelineOptions options)
    {
        _middlewareOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Configures middleware pipeline behavior.
    /// </summary>
    /// <param name="configure">Action to configure middleware options.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> ConfigureMiddleware(Action<MiddlewarePipelineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = _middlewareOptions ?? new MiddlewarePipelineOptions();
        configure(options);
        _middlewareOptions = options;
        return this;
    }

    /// <summary>
    /// Registers a centralized error handler for store operations.
    /// Errors from middleware, subscribers, and persistence are routed to this handler.
    /// </summary>
    /// <param name="errorHandler">The error handler delegate.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.OnError(error =>
    /// {
    ///     logger.LogError(error.Exception,
    ///         "Store error in {Location}: {Message}",
    ///         error.Location,
    ///         error.Exception.Message);
    ///
    ///     // Report to error tracking service
    ///     errorTracker.CaptureException(error.Exception, new Dictionary&lt;string, object&gt;
    ///     {
    ///         ["store"] = typeof(TState).Name,
    ///         ["action"] = error.Action ?? "unknown",
    ///         ["location"] = error.Location.ToString()
    ///     });
    /// });
    /// </code>
    /// </example>
    public StoreBuilder<TState> OnError(StoreErrorHandler<TState> errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        return this;
    }

    /// <summary>
    /// Registers an error handler using an action that receives the error.
    /// </summary>
    /// <param name="handler">The error handler action.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> OnError(Action<StoreError<TState>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _errorHandler = handler.Invoke;
        return this;
    }

    /// <summary>
    /// Enables Redux DevTools integration with lazy IJSRuntime resolution.
    /// Works in all render modes: Server, WebAssembly, and Auto (Server → WASM).
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve IJSRuntime on-demand.</param>
    /// <param name="storeName">The name to display in DevTools. Defaults to the state type name.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithDevTools(IServiceProvider serviceProvider, string? storeName = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var devToolsMiddleware = new DevToolsMiddleware<TState>(
            serviceProvider,
            storeName ?? typeof(TState).Name);

        return WithMiddleware(devToolsMiddleware);
    }

    /// <summary>
    /// Adds state persistence with the provided provider.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="jsonOptions">Optional JSON serialization options.</param>
    /// <param name="debounceMs">Debounce duration in milliseconds.</param>
    /// <returns>The builder instance for chaining.</returns>
    public StoreBuilder<TState> WithPersistence(
        IPersistenceProvider provider,
        string key,
        JsonSerializerOptions? jsonOptions = null,
        int debounceMs = 0)
    {
        var hydratedBuilder = TryLoadPersistedState(key, jsonOptions);

        var middleware = new PersistenceMiddleware<TState>(
            provider,
            key,
            jsonOptions,
            debounceMs);

        return hydratedBuilder.WithMiddleware(middleware);
    }

    /// <summary>
    /// Adds state persistence with full configuration options.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="options">The persistence configuration options.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithPersistence(provider, new PersistenceOptions&lt;CartState&gt;
    /// {
    ///     Key = "cart-state",
    ///     DebounceMs = 500,
    ///     OnHydrationSuccess = state => logger.LogInformation("Loaded {Count} items", state.Items.Count),
    ///     ShouldPersist = (prev, curr, action) => action != "TEMP_UPDATE",
    ///     TransformOnLoad = state => state with { CheckoutInProgress = false }
    /// })
    /// </code>
    /// </example>
    public StoreBuilder<TState> WithPersistence(
        IPersistenceProvider provider,
        PersistenceOptions<TState> options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var middleware = new PersistenceMiddleware<TState>(provider, options);
        return WithMiddleware(middleware);
    }

    /// <summary>
    /// Attempts to load persisted state. Since synchronous JS interop is not available
    /// without explicit IJSInProcessRuntime, this method now returns the current builder.
    /// Use <see cref="WithHydratedStateAsync"/> for async state hydration.
    /// </summary>
    private StoreBuilder<TState> TryLoadPersistedState(
        string key,
        JsonSerializerOptions? jsonOptions)
    {
        // Synchronous hydration is not supported without IJSInProcessRuntime.
        // Use WithHydratedStateAsync for async state hydration.
        return this;
    }

    /// <summary>
    /// Loads initial state from the persistence provider.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="jsonOptions">Optional JSON serialization options.</param>
    /// <returns>The builder instance with loaded state.</returns>
    public async Task<StoreBuilder<TState>> WithHydratedStateAsync(
        IPersistenceProvider provider,
        string key,
        JsonSerializerOptions? jsonOptions = null)
    {
        try
        {
            var json = await provider.LoadAsync(key).ConfigureAwait(false);
            if (json != null)
            {
                var options = jsonOptions ?? new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var loadedState = JsonSerializer.Deserialize<TState>(json, options);
                if (loadedState != null)
                {
                    var builder = new StoreBuilder<TState>(loadedState)
                    {
                        _comparer = this._comparer,
                        _middlewarePipelineLogger = this._middlewarePipelineLogger,
                        _storeLogger = this._storeLogger,
                        _subscriptionManagerLogger = this._subscriptionManagerLogger,
                        _middlewareOptions = this._middlewareOptions,
                        _errorHandler = this._errorHandler
                    };
                    builder._middlewares.AddRange(this._middlewares);
                    return builder;
                }
            }
        }
        catch (Exception ex)
        {
            // Log at Debug level - user can enable diagnostics if needed
            System.Diagnostics.Debug.WriteLine(
                $"[EasyAppDev.Store] Failed to async hydrate state for {typeof(TState).Name} from key '{key}': {ex.Message}");
        }

        return this;
    }

    /// <summary>
    /// Builds the configured store instance.
    /// </summary>
    /// <returns>A new <see cref="IStore{TState}"/> instance.</returns>
    public IStore<TState> Build()
    {
        var subscriptionManager = new SubscriptionManager<TState>(_subscriptionManagerLogger);

        return new Store<TState>(
            initialState: _initialState,
            subscriptionManager: subscriptionManager,
            comparer: _comparer,
            middlewares: _middlewares,
            middlewarePipelineLogger: _middlewarePipelineLogger,
            logger: _storeLogger,
            middlewareOptions: _middlewareOptions,
            errorHandler: _errorHandler);
    }
}
