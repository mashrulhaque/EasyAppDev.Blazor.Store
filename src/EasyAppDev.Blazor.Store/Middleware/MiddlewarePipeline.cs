namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Manages the execution of middleware in a pipeline.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class MiddlewarePipeline<TState> where TState : notnull
{
    private readonly IReadOnlyList<IMiddleware<TState>> _middlewares;
    private readonly ILogger<MiddlewarePipeline<TState>>? _logger;
    private readonly MiddlewarePipelineOptions _options;

    /// <summary>
    /// Creates a middleware pipeline with the specified configuration.
    /// </summary>
    /// <param name="middlewares">The collection of middleware to execute.</param>
    /// <param name="logger">Optional logger for error handling.</param>
    /// <param name="options">Optional configuration options for pipeline behavior.</param>
    public MiddlewarePipeline(
        IEnumerable<IMiddleware<TState>> middlewares,
        ILogger<MiddlewarePipeline<TState>>? logger = null,
        MiddlewarePipelineOptions? options = null)
    {
        _middlewares = middlewares?.ToList() ?? new List<IMiddleware<TState>>();
        _logger = logger;
        _options = options ?? MiddlewarePipelineOptions.Default;
    }

    /// <summary>
    /// Executes all middleware OnBeforeUpdateAsync methods in order.
    /// </summary>
    /// <param name="currentState">The current state before update.</param>
    /// <param name="action">Optional action name.</param>
    public async Task ExecuteBeforeUpdateAsync(TState currentState, string? action)
    {
        foreach (var middleware in _middlewares)
        {
            var retries = 0;
            var maxRetries = _options.MaxRetries;

            while (retries <= maxRetries)
            {
                try
                {
                    await middleware.OnBeforeUpdateAsync(currentState, action)
                        .ConfigureAwait(false);
                    break; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    retries++;

                    if (_options.LogErrors)
                    {
                        _logger?.LogError(ex,
                            "Error in middleware {MiddlewareType} during OnBeforeUpdateAsync (attempt {Attempt}/{MaxAttempts})",
                            middleware.GetType().Name,
                            retries,
                            maxRetries + 1);
                    }

                    if (retries > maxRetries)
                    {
                        if (_options.StopOnError)
                        {
                            throw; // Rethrow to stop pipeline execution
                        }
                        // Continue with next middleware
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Executes all middleware OnAfterUpdateAsync methods in order.
    /// </summary>
    /// <param name="previousState">The state before update.</param>
    /// <param name="currentState">The state after update.</param>
    /// <param name="action">Optional action name.</param>
    public async Task ExecuteAfterUpdateAsync(
        TState previousState,
        TState currentState,
        string? action)
    {
        foreach (var middleware in _middlewares)
        {
            var retries = 0;
            var maxRetries = _options.MaxRetries;

            while (retries <= maxRetries)
            {
                try
                {
                    await middleware.OnAfterUpdateAsync(previousState, currentState, action)
                        .ConfigureAwait(false);
                    break; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    retries++;

                    if (_options.LogErrors)
                    {
                        _logger?.LogError(ex,
                            "Error in middleware {MiddlewareType} during OnAfterUpdateAsync (attempt {Attempt}/{MaxAttempts})",
                            middleware.GetType().Name,
                            retries,
                            maxRetries + 1);
                    }

                    if (retries > maxRetries)
                    {
                        if (_options.StopOnError)
                        {
                            throw; // Rethrow to stop pipeline execution
                        }
                        // Continue with next middleware
                        break;
                    }
                }
            }
        }
    }
}
