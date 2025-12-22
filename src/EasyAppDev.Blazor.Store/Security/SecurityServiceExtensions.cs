// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

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
}
