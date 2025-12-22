// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Extension methods for adding validation to stores.
/// These extensions help ensure that stores reject invalid state from external sources.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Configures the store to require validation and reject invalid state.
    /// When called, the store will verify during Build() that a validator has been configured.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at Build() time if no validator has been configured via WithValidator().
    /// </exception>
    /// <remarks>
    /// This method should be called after configuring persistence, tab sync, or server sync
    /// to ensure validation is enforced. It verifies that a proper validator has been set.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new UserState(),
    ///     (store, sp) => store
    ///         .WithValidator(new UserStateValidator())
    ///         .WithPersistence(sp, "user-state")
    ///         .RequireValidation()
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> RequireValidation<TState>(
        this StoreBuilder<TState> builder)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.StateValidator == null)
        {
            throw new InvalidOperationException(
                "RequireValidation() was called but no validator has been configured. " +
                "Call WithValidator() before RequireValidation() to configure a state validator. " +
                "Example: builder.WithValidator(new MyStateValidator()).RequireValidation()");
        }

        return builder.WithRequiredValidation();
    }

    /// <summary>
    /// Adds default validation that performs basic null and empty checks.
    /// This provides baseline protection without requiring custom validator implementation.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// Default validation includes:
    /// <list type="bullet">
    /// <item>Null state rejection via <see cref="RequiredStateValidator{TState}"/></item>
    /// </list>
    /// For more sophisticated validation, implement <see cref="SchemaStateValidator{TState}"/>
    /// or <see cref="IStateValidator{TState}"/> and use WithValidator() instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store
    ///         .WithDefaultValidation()
    ///         .WithPersistence(sp, "app-state")
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithDefaultValidation<TState>(
        this StoreBuilder<TState> builder)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithStateValidator(RequiredStateValidator<TState>.Instance);
    }

    /// <summary>
    /// Adds a custom validator to the store.
    /// The validator will be stored in the builder and automatically used by
    /// persistence, tab sync, and server sync middleware to reject invalid state.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="validator">The validator to use for state validation.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// The validator is stored in the StoreBuilder and can be accessed via the
    /// StateValidator property. Middleware extensions (WithPersistence, WithTabSync,
    /// WithServerSync) will automatically use this validator if no validator is
    /// explicitly configured in their options.
    /// </remarks>
    /// <example>
    /// <code>
    /// public class UserStateValidator : SchemaStateValidator&lt;UserState&gt;
    /// {
    ///     protected override StateValidationResult ValidateState(UserState state)
    ///     {
    ///         var errors = new List&lt;string&gt;();
    ///
    ///         if (string.IsNullOrWhiteSpace(state.Username))
    ///             errors.Add("Username is required");
    ///
    ///         if (state.Age &lt; 0 || state.Age &gt; 150)
    ///             errors.Add("Age must be between 0 and 150");
    ///
    ///         return errors.Count &gt; 0
    ///             ? StateValidationResult.Failure(errors)
    ///             : StateValidationResult.Success();
    ///     }
    /// }
    ///
    /// builder.Services.AddStore(
    ///     new UserState(),
    ///     (store, sp) => store
    ///         .WithValidator(new UserStateValidator())
    ///         .WithPersistence(sp, "user-state")
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithValidator<TState>(
        this StoreBuilder<TState> builder,
        IStateValidator<TState> validator)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(validator);

        return builder.WithStateValidator(validator);
    }

    /// <summary>
    /// Adds validation middleware that validates all state changes.
    /// This is useful when you want to validate state regardless of the source.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="validator">The validator to use.</param>
    /// <param name="rejectInvalid">If true, throws an exception for invalid state. If false, logs a warning.</param>
    /// <param name="logger">Optional logger for validation messages.</param>
    /// <returns>The builder for chaining.</returns>
    public static StoreBuilder<TState> WithValidationMiddleware<TState>(
        this StoreBuilder<TState> builder,
        IStateValidator<TState> validator,
        bool rejectInvalid = true,
        ILogger? logger = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(validator);

        var middleware = new ValidationMiddleware<TState>(validator, rejectInvalid, logger);
        return builder.WithMiddleware(middleware).WithStateValidator(validator);
    }
}

/// <summary>
/// Middleware that validates all state changes before they are applied.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class ValidationMiddleware<TState> : IMiddleware<TState>
    where TState : notnull
{
    private readonly IStateValidator<TState> _validator;
    private readonly bool _rejectInvalid;
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new validation middleware.
    /// </summary>
    /// <param name="validator">The validator to use.</param>
    /// <param name="rejectInvalid">If true, throws an exception for invalid state.</param>
    /// <param name="logger">Optional logger for validation messages.</param>
    public ValidationMiddleware(
        IStateValidator<TState> validator,
        bool rejectInvalid = true,
        ILogger? logger = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _rejectInvalid = rejectInvalid;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        var result = _validator.Validate(currentState);

        if (!result.IsValid)
        {
            var errorMessage = $"State validation failed after action '{action ?? "unknown"}': {string.Join(", ", result.Errors)}";

            if (_rejectInvalid)
            {
                _logger?.LogError("{ErrorMessage}", errorMessage);
                throw new StateValidationException(result, action);
            }
            else
            {
                _logger?.LogWarning("{ErrorMessage}", errorMessage);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Exception thrown when state validation fails.
/// </summary>
public sealed class StateValidationException : Exception
{
    /// <summary>
    /// Gets the validation result that caused this exception.
    /// </summary>
    public StateValidationResult ValidationResult { get; }

    /// <summary>
    /// Gets the action that caused the validation failure.
    /// </summary>
    public string? Action { get; }

    /// <summary>
    /// Creates a new state validation exception.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <param name="action">The action that caused the failure.</param>
    public StateValidationException(StateValidationResult result, string? action = null)
        : base($"State validation failed: {string.Join(", ", result.Errors)}")
    {
        ValidationResult = result;
        Action = action;
    }
}
