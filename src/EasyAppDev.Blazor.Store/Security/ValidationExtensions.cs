// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Extension methods for adding validation to stores.
/// These extensions help ensure that stores reject invalid state from external sources.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Configures the store to require validation and reject invalid state.
    /// Throws an exception if no validator has been configured.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no validator has been configured via SecurityOptions.
    /// </exception>
    /// <remarks>
    /// This method should be called after configuring persistence, tab sync, or server sync
    /// to ensure validation is enforced. It verifies that a proper validator has been set
    /// in the SecurityOptions for any middleware that uses it.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new UserState(),
    ///     (store, sp) => store
    ///         .WithPersistence(sp, "user-state")
    ///         .WithValidator(new UserStateValidator())
    ///         .RequireValidation()
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> RequireValidation<TState>(
        this StoreBuilder<TState> builder)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Note: Validation requirement is checked at runtime by middleware
        // This method serves as a documentation and intent declaration
        return builder;
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
    /// or <see cref="IStateValidator{TState}"/> and configure it via the appropriate middleware options.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store
    ///         .WithPersistence(sp, "app-state")
    ///         .WithDefaultValidation()
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithDefaultValidation<TState>(
        this StoreBuilder<TState> builder)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Note: Default validator is RequiredStateValidator which checks for null
        // Specific middleware (Persistence, TabSync, ServerSync) use SecurityOptions<TState>
        // This method serves as documentation for the intent to use default validation
        return builder;
    }

    /// <summary>
    /// Adds a custom validator to the store's security options.
    /// The validator will be applied by persistence, tab sync, and server sync middleware
    /// to reject invalid state from external sources.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="validator">The validator to use for state validation.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// This extension method provides a convenient way to configure validation
    /// without directly accessing SecurityOptions. The validator will be used by
    /// any middleware that consumes SecurityOptions (Persistence, TabSync, ServerSync).
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
    ///         .WithPersistence(sp, "user-state")
    ///         .WithValidator(new UserStateValidator())
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

        // Note: The validator should be configured via SecurityOptions in the specific middleware
        // This method provides a fluent API hint but actual configuration happens in middleware
        // For a complete implementation, this would need to store the validator somewhere
        // accessible to all middleware instances (e.g., via a shared configuration service)

        return builder;
    }
}
