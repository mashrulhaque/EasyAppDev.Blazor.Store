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
