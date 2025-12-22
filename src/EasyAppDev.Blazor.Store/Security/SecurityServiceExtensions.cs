// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Extension methods for registering security services.
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// Adds the security audit logger to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure audit options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurityAuditLogger(
        this IServiceCollection services,
        Action<SecurityAuditOptions>? configureOptions = null)
    {
        var options = new SecurityAuditOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();

        return services;
    }

    /// <summary>
    /// Adds the security audit logger with a custom implementation.
    /// </summary>
    /// <typeparam name="TLogger">The logger implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurityAuditLogger<TLogger>(
        this IServiceCollection services)
        where TLogger : class, ISecurityAuditLogger
    {
        services.AddSingleton<ISecurityAuditLogger, TLogger>();
        return services;
    }

    /// <summary>
    /// Adds a state validator to the service collection.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateValidator<TState, TValidator>(
        this IServiceCollection services)
        where TState : notnull
        where TValidator : class, IStateValidator<TState>
    {
        services.AddSingleton<IStateValidator<TState>, TValidator>();
        return services;
    }

    /// <summary>
    /// Adds a state validator instance to the service collection.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validator">The validator instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateValidator<TState>(
        this IServiceCollection services,
        IStateValidator<TState> validator)
        where TState : notnull
    {
        services.AddSingleton(validator);
        return services;
    }

    /// <summary>
    /// Adds a state validator using a factory function.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validatorFactory">Factory function to create the validator.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateValidator<TState>(
        this IServiceCollection services,
        Func<IServiceProvider, IStateValidator<TState>> validatorFactory)
        where TState : notnull
    {
        services.AddSingleton(validatorFactory);
        return services;
    }

    /// <summary>
    /// Adds a state validator using a validation function.
    /// Creates a FuncStateValidator that wraps the provided function.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validateFunc">Function that validates state and returns errors if invalid.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddStateValidator&lt;AppState&gt;(state =>
    /// {
    ///     var errors = new List&lt;string&gt;();
    ///     if (state.Count &lt; 0) errors.Add("Count cannot be negative");
    ///     if (string.IsNullOrEmpty(state.Name)) errors.Add("Name is required");
    ///     return errors;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddStateValidator<TState>(
        this IServiceCollection services,
        Func<TState, IEnumerable<string>> validateFunc)
        where TState : notnull
    {
        var validator = new FuncStateValidator<TState>(validateFunc);
        return services.AddStateValidator(validator);
    }

    /// <summary>
    /// Discovers and registers all state validators from the specified assembly.
    /// Validators must implement IStateValidator&lt;TState&gt; and have a parameterless constructor.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan for validators.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // Register all validators from the current assembly
    /// services.AddStateValidatorsFromAssembly(typeof(Program).Assembly);
    /// </code>
    /// </example>
    public static IServiceCollection AddStateValidatorsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var validatorInterfaceType = typeof(IStateValidator<>);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            // Find all IStateValidator<T> interfaces implemented by this type
            var validatorInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorInterfaceType)
                .ToList();

            foreach (var validatorInterface in validatorInterfaces)
            {
                // Register the validator
                services.AddSingleton(validatorInterface, type);
            }
        }

        return services;
    }

    /// <summary>
    /// Discovers and registers all state validators from the assembly containing the specified type.
    /// </summary>
    /// <typeparam name="TMarker">A type in the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateValidatorsFromAssemblyContaining<TMarker>(
        this IServiceCollection services)
    {
        return services.AddStateValidatorsFromAssembly(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Adds a composite validator that combines multiple validators for a state type.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validators">The validators to combine.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompositeValidator<TState>(
        this IServiceCollection services,
        params IStateValidator<TState>[] validators)
        where TState : notnull
    {
        var composite = new CompositeStateValidator<TState>(validators);
        return services.AddStateValidator(composite);
    }

    /// <summary>
    /// Checks if a state validator is registered for the specified state type.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>True if a validator is registered, false otherwise.</returns>
    public static bool HasStateValidator<TState>(this IServiceCollection services)
        where TState : notnull
    {
        return services.Any(s => s.ServiceType == typeof(IStateValidator<TState>));
    }

    /// <summary>
    /// Ensures a state validator is registered, throwing if not found.
    /// Use this to fail fast during startup if validation is required.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="profile">The security profile for the error message.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="SecurityConfigurationException">Thrown if no validator is registered.</exception>
    public static IServiceCollection RequireStateValidator<TState>(
        this IServiceCollection services,
        SecurityProfile profile = SecurityProfile.Production)
        where TState : notnull
    {
        if (!services.HasStateValidator<TState>())
        {
            throw SecurityConfigurationException.MissingValidator(typeof(TState).Name, profile);
        }
        return services;
    }
}

/// <summary>
/// A state validator that wraps a validation function.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
public sealed class FuncStateValidator<TState> : IStateValidator<TState>
    where TState : notnull
{
    private readonly Func<TState, IEnumerable<string>> _validateFunc;

    /// <summary>
    /// Creates a new function-based validator.
    /// </summary>
    /// <param name="validateFunc">Function that returns validation errors.</param>
    public FuncStateValidator(Func<TState, IEnumerable<string>> validateFunc)
    {
        _validateFunc = validateFunc ?? throw new ArgumentNullException(nameof(validateFunc));
    }

    /// <inheritdoc />
    public StateValidationResult Validate(TState state)
    {
        if (state is null)
        {
            return StateValidationResult.Failure("State cannot be null");
        }

        var errors = _validateFunc(state).ToList();
        return errors.Count > 0
            ? StateValidationResult.Failure(errors)
            : StateValidationResult.Success();
    }
}
