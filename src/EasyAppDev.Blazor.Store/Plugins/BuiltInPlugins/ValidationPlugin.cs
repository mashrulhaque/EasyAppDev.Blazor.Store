// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Plugins.BuiltInPlugins;

/// <summary>
/// Plugin that validates state after each update.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ValidationPlugin<TState> : ConfigurablePlugin<TState, ValidationOptions<TState>>
    where TState : notnull
{
    private ILogger<ValidationPlugin<TState>>? _logger;

    /// <inheritdoc />
    public override string Name => "Validation";

    /// <inheritdoc />
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public override void Configure(StoreBuilder<TState> builder, IServiceProvider services)
    {
        _logger = services.GetService(typeof(ILogger<ValidationPlugin<TState>>)) as ILogger<ValidationPlugin<TState>>;
    }

    /// <inheritdoc />
    public override IMiddleware<TState>? GetMiddleware()
    {
        return new ValidationMiddleware<TState>(Options, _logger);
    }
}

/// <summary>
/// Options for the validation plugin.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ValidationOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the validator function.
    /// Returns a list of validation errors, or empty if valid.
    /// </summary>
    public Func<TState, IReadOnlyList<string>>? Validator { get; set; }

    /// <summary>
    /// Gets or sets a callback for validation errors.
    /// </summary>
    public Action<TState, IReadOnlyList<string>>? OnValidationError { get; set; }

    /// <summary>
    /// Gets or sets whether to prevent updates that fail validation.
    /// Default is false (allow updates but report errors).
    /// </summary>
    public bool PreventInvalidUpdates { get; set; } = false;

    /// <summary>
    /// Gets or sets actions to skip validation for.
    /// </summary>
    public HashSet<string> SkipActions { get; set; } = new()
    {
        "@@INIT",
        "@@JUMP_TO_STATE",
        "@@IMPORT_STATE"
    };
}

internal class ValidationMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    private readonly ValidationOptions<TState> _options;
    private readonly ILogger<ValidationPlugin<TState>>? _logger;

    public ValidationMiddleware(
        ValidationOptions<TState> options,
        ILogger<ValidationPlugin<TState>>? logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        if (_options.Validator == null)
            return Task.CompletedTask;

        if (action != null && _options.SkipActions.Contains(action))
            return Task.CompletedTask;

        var errors = _options.Validator(currentState);
        if (errors.Count > 0)
        {
            _logger?.LogWarning(
                "State validation failed after {Action}: {Errors}",
                action,
                string.Join(", ", errors));

            _options.OnValidationError?.Invoke(currentState, errors);

            if (_options.PreventInvalidUpdates)
            {
                throw new StateValidationException(errors);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Exception thrown when state validation fails and prevention is enabled.
/// </summary>
public class StateValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates a new validation exception.
    /// </summary>
    public StateValidationException(IReadOnlyList<string> errors)
        : base($"State validation failed: {string.Join(", ", errors)}")
    {
        Errors = errors;
    }
}
