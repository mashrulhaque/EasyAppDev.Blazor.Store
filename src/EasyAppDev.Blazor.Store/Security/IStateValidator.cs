// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Validates state before it is applied to the store.
/// Implement this interface to add custom validation for deserialized state
/// from external sources (persistence, tab sync, server sync).
/// </summary>
/// <typeparam name="TState">The type of state to validate.</typeparam>
public interface IStateValidator<TState> where TState : notnull
{
    /// <summary>
    /// Validates the given state.
    /// </summary>
    /// <param name="state">The state to validate.</param>
    /// <returns>A validation result indicating success or failure with details.</returns>
    StateValidationResult Validate(TState state);
}

/// <summary>
/// Result of state validation.
/// </summary>
public sealed record StateValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Gets the source of the invalid data.
    /// </summary>
    public string? Source { get; init; }

    private StateValidationResult(bool isValid, IReadOnlyList<string>? errors = null)
    {
        IsValid = isValid;
        Errors = errors ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static StateValidationResult Success() => new(true);

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static StateValidationResult Failure(params string[] errors) => new(false, errors);

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static StateValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Default state validator that accepts all states.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class NoOpStateValidator<TState> : IStateValidator<TState> where TState : notnull
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NoOpStateValidator<TState> Instance = new();

    private NoOpStateValidator() { }

    /// <inheritdoc />
    public StateValidationResult Validate(TState state) => StateValidationResult.Success();
}

/// <summary>
/// Composite validator that runs multiple validators.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class CompositeStateValidator<TState> : IStateValidator<TState> where TState : notnull
{
    private readonly IReadOnlyList<IStateValidator<TState>> _validators;

    /// <summary>
    /// Creates a composite validator from multiple validators.
    /// </summary>
    /// <param name="validators">The validators to combine.</param>
    public CompositeStateValidator(params IStateValidator<TState>[] validators)
    {
        _validators = validators ?? Array.Empty<IStateValidator<TState>>();
    }

    /// <inheritdoc />
    public StateValidationResult Validate(TState state)
    {
        var allErrors = new List<string>();

        foreach (var validator in _validators)
        {
            var result = validator.Validate(state);
            if (!result.IsValid)
            {
                allErrors.AddRange(result.Errors);
            }
        }

        return allErrors.Count > 0
            ? StateValidationResult.Failure(allErrors)
            : StateValidationResult.Success();
    }
}

/// <summary>
/// Validator that ensures state is not null.
/// Recommended as a baseline validator for all stores that handle external state.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class RequiredStateValidator<TState> : IStateValidator<TState> where TState : notnull
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly RequiredStateValidator<TState> Instance = new();

    private RequiredStateValidator() { }

    /// <inheritdoc />
    public StateValidationResult Validate(TState state)
    {
        if (state is null)
        {
            return StateValidationResult.Failure("State cannot be null");
        }

        return StateValidationResult.Success();
    }
}

/// <summary>
/// Base class for schema-based state validation.
/// Extend this class to implement custom validation rules for your state.
/// </summary>
/// <typeparam name="TState">The type of state to validate.</typeparam>
/// <remarks>
/// This validator is recommended for stores that:
/// <list type="bullet">
/// <item>Accept state from untrusted sources (persistence, tab sync, server sync)</item>
/// <item>Have business rules or constraints that must be enforced</item>
/// <item>Need to prevent invalid state from corrupting the application</item>
/// </list>
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
/// </code>
/// </example>
public abstract class SchemaStateValidator<TState> : IStateValidator<TState> where TState : notnull
{
    /// <inheritdoc />
    public StateValidationResult Validate(TState state)
    {
        if (state is null)
        {
            return StateValidationResult.Failure("State cannot be null");
        }

        return ValidateState(state);
    }

    /// <summary>
    /// Override this method to implement custom validation logic.
    /// State is guaranteed to be non-null when this method is called.
    /// </summary>
    /// <param name="state">The state to validate.</param>
    /// <returns>A validation result indicating success or failure.</returns>
    protected abstract StateValidationResult ValidateState(TState state);
}
