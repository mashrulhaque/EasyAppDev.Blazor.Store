namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Middleware implementation that wraps a functional delegate.
/// Allows inline middleware definition without creating a class.
/// </summary>
/// <remarks>
/// <para>
/// Note: The <c>next</c> parameter passed to handlers is a no-op delegate since
/// IMiddleware implementations are invoked in parallel by the MiddlewarePipeline,
/// not chained sequentially. The parameter exists for compatibility with express-style
/// middleware patterns. If you need true chaining, implement a custom pipeline.
/// </para>
/// <para>
/// <b>Lifetime Consideration:</b> If you pass an <see cref="IServiceProvider"/> to the constructor,
/// ensure it has a lifetime equal to or longer than the middleware. Passing a scoped provider
/// to a singleton middleware will cause issues when the scope is disposed. For Blazor Server,
/// prefer resolving services within your handler using the application's root provider or
/// use <see cref="IServiceScopeFactory"/> to create scopes as needed.
/// </para>
/// </remarks>
/// <typeparam name="TState">The type of state.</typeparam>
public class FunctionalMiddleware<TState> : IMiddleware<TState>
    where TState : notnull
{
    private readonly Func<MiddlewareContext<TState>, Func<Task>, Task>? _beforeHandler;
    private readonly Func<MiddlewareContext<TState>, Func<Task>, Task>? _afterHandler;
    private readonly Func<MiddlewareContext<TState>, Func<Task>, Task>? _handler;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Creates a functional middleware with a single handler for both phases.
    /// The handler is called during both Before and After phases.
    /// </summary>
    /// <param name="handler">The middleware handler function.</param>
    /// <param name="serviceProvider">
    /// Optional service provider for dependency resolution. Must have a lifetime
    /// equal to or longer than the middleware. See class remarks for details.
    /// </param>
    public FunctionalMiddleware(
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler,
        IServiceProvider? serviceProvider = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a functional middleware with separate handlers for Before and After phases.
    /// </summary>
    /// <param name="beforeHandler">Handler for the Before phase (can be null to skip).</param>
    /// <param name="afterHandler">Handler for the After phase (can be null to skip).</param>
    /// <param name="serviceProvider">
    /// Optional service provider for dependency resolution. Must have a lifetime
    /// equal to or longer than the middleware. See class remarks for details.
    /// </param>
    public FunctionalMiddleware(
        Func<MiddlewareContext<TState>, Func<Task>, Task>? beforeHandler,
        Func<MiddlewareContext<TState>, Func<Task>, Task>? afterHandler,
        IServiceProvider? serviceProvider = null)
    {
        _beforeHandler = beforeHandler;
        _afterHandler = afterHandler;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        var context = new MiddlewareContext<TState>(
            CurrentState: currentState,
            NewState: default,
            Action: action,
            Services: _serviceProvider,
            Phase: MiddlewarePhase.Before);

        if (_handler != null)
        {
            await _handler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
        else if (_beforeHandler != null)
        {
            await _beforeHandler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        var context = new MiddlewareContext<TState>(
            CurrentState: previousState,
            NewState: currentState,
            Action: action,
            Services: _serviceProvider,
            Phase: MiddlewarePhase.After);

        if (_handler != null)
        {
            await _handler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
        else if (_afterHandler != null)
        {
            await _afterHandler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Middleware that conditionally executes based on a predicate.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ConditionalMiddleware<TState> : IMiddleware<TState>
    where TState : notnull
{
    private readonly Func<MiddlewareContext<TState>, bool> _predicate;
    private readonly Func<MiddlewareContext<TState>, Func<Task>, Task> _handler;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Creates a conditional middleware that only executes when the predicate returns true.
    /// </summary>
    /// <param name="predicate">Condition that must be true for the middleware to execute.</param>
    /// <param name="handler">The middleware handler to execute when condition is met.</param>
    /// <param name="serviceProvider">Optional service provider for dependency resolution.</param>
    public ConditionalMiddleware(
        Func<MiddlewareContext<TState>, bool> predicate,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler,
        IServiceProvider? serviceProvider = null)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        var context = new MiddlewareContext<TState>(
            CurrentState: currentState,
            NewState: default,
            Action: action,
            Services: _serviceProvider,
            Phase: MiddlewarePhase.Before);

        if (_predicate(context))
        {
            await _handler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        var context = new MiddlewareContext<TState>(
            CurrentState: previousState,
            NewState: currentState,
            Action: action,
            Services: _serviceProvider,
            Phase: MiddlewarePhase.After);

        if (_predicate(context))
        {
            await _handler(context, () => Task.CompletedTask).ConfigureAwait(false);
        }
    }
}
