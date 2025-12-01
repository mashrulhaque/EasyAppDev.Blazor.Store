using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Extension methods for adding functional middleware to <see cref="StoreBuilder{TState}"/>.
/// </summary>
public static class FunctionalMiddlewareExtensions
{
    /// <summary>
    /// Adds inline functional middleware that executes for both Before and After phases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="handler">
    /// The middleware handler. Receives the context and a next delegate to call.
    /// The next delegate should be awaited to continue the pipeline.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Use(async (ctx, next) =>
    /// {
    ///     Console.WriteLine($"Before: {ctx.Action}");
    ///     await next();
    ///     Console.WriteLine($"After: {ctx.Action}");
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> Use<TState>(
        this StoreBuilder<TState> builder,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.WithMiddleware(new FunctionalMiddleware<TState>(handler));
    }

    /// <summary>
    /// Adds inline functional middleware with a service provider for dependency resolution.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="handler">The middleware handler.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Use(serviceProvider, async (ctx, next) =>
    /// {
    ///     var logger = ctx.Services!.GetRequiredService&lt;ILogger&gt;();
    ///     logger.LogInformation("Action: {Action}", ctx.Action);
    ///     await next();
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> Use<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.WithMiddleware(new FunctionalMiddleware<TState>(handler, serviceProvider));
    }

    /// <summary>
    /// Adds separate handlers for Before and After phases.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="beforeHandler">Handler for the Before phase (can be null to skip).</param>
    /// <param name="afterHandler">Handler for the After phase (can be null to skip).</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Use(
    ///     beforeHandler: async (ctx, next) => {
    ///         Console.WriteLine("Starting update...");
    ///         await next();
    ///     },
    ///     afterHandler: async (ctx, next) => {
    ///         Console.WriteLine($"State changed: {ctx.NewState}");
    ///         await next();
    ///     }
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> Use<TState>(
        this StoreBuilder<TState> builder,
        Func<MiddlewareContext<TState>, Func<Task>, Task>? beforeHandler,
        Func<MiddlewareContext<TState>, Func<Task>, Task>? afterHandler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (beforeHandler == null && afterHandler == null)
        {
            throw new ArgumentException("At least one handler must be provided.");
        }

        return builder.WithMiddleware(new FunctionalMiddleware<TState>(beforeHandler, afterHandler));
    }

    /// <summary>
    /// Adds conditional middleware that only executes when the predicate returns true.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="predicate">Condition that must be true for the middleware to execute.</param>
    /// <param name="handler">The middleware handler to execute when condition is met.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// // Only log FETCH_* actions
    /// builder.UseWhen(
    ///     ctx => ctx.Action?.StartsWith("FETCH_") == true,
    ///     async (ctx, next) =>
    ///     {
    ///         Console.WriteLine($"Fetching: {ctx.Action}");
    ///         await next();
    ///     }
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> UseWhen<TState>(
        this StoreBuilder<TState> builder,
        Func<MiddlewareContext<TState>, bool> predicate,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.WithMiddleware(new ConditionalMiddleware<TState>(predicate, handler));
    }

    /// <summary>
    /// Adds conditional middleware with a service provider for dependency resolution.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="predicate">Condition that must be true for the middleware to execute.</param>
    /// <param name="handler">The middleware handler to execute when condition is met.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> UseWhen<TState>(
        this StoreBuilder<TState> builder,
        IServiceProvider serviceProvider,
        Func<MiddlewareContext<TState>, bool> predicate,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.WithMiddleware(new ConditionalMiddleware<TState>(predicate, handler, serviceProvider));
    }

    /// <summary>
    /// Adds middleware that executes only for specific action names.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="actionName">The action name to match (case-insensitive).</param>
    /// <param name="handler">The middleware handler.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.UseForAction("SAVE_USER", async (ctx, next) =>
    /// {
    ///     Console.WriteLine("Saving user...");
    ///     await next();
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> UseForAction<TState>(
        this StoreBuilder<TState> builder,
        string actionName,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.UseWhen(
            ctx => string.Equals(ctx.Action, actionName, StringComparison.OrdinalIgnoreCase),
            handler);
    }

    /// <summary>
    /// Adds middleware that executes only when actions match a prefix.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="prefix">The action name prefix to match.</param>
    /// <param name="handler">The middleware handler.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// // Matches: FETCH_USERS, FETCH_PRODUCTS, etc.
    /// builder.UseForActionPrefix("FETCH_", async (ctx, next) =>
    /// {
    ///     Console.WriteLine($"Fetching: {ctx.Action}");
    ///     await next();
    /// });
    /// </code>
    /// </example>
    public static StoreBuilder<TState> UseForActionPrefix<TState>(
        this StoreBuilder<TState> builder,
        string prefix,
        Func<MiddlewareContext<TState>, Func<Task>, Task> handler)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(handler);

        return builder.UseWhen(
            ctx => ctx.Action?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true,
            handler);
    }
}
