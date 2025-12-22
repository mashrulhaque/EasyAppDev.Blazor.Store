// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Exception thrown when a security configuration is invalid or insecure.
/// This exception is designed to fail fast during application startup,
/// preventing deployment of misconfigured applications.
/// </summary>
public sealed class SecurityConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Gets the security component that triggered the exception.
    /// </summary>
    public string Component { get; }

    /// <summary>
    /// Gets the security profile being used.
    /// </summary>
    public SecurityProfile Profile { get; }

    /// <summary>
    /// Gets the list of configuration errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets the list of configuration warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Creates a new security configuration exception.
    /// </summary>
    /// <param name="component">The component that detected the issue.</param>
    /// <param name="message">The error message.</param>
    /// <param name="profile">The security profile being used.</param>
    /// <param name="errors">Optional list of specific errors.</param>
    /// <param name="warnings">Optional list of warnings.</param>
    public SecurityConfigurationException(
        string component,
        string message,
        SecurityProfile profile = SecurityProfile.Custom,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
        : base(FormatMessage(component, message, errors, warnings))
    {
        Component = component;
        Profile = profile;
        Errors = errors ?? Array.Empty<string>();
        Warnings = warnings ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an exception for missing validator configuration.
    /// </summary>
    public static SecurityConfigurationException MissingValidator(
        string stateTypeName,
        SecurityProfile profile)
    {
        return new SecurityConfigurationException(
            "SecureStore",
            $"No IStateValidator<{stateTypeName}> registered in DI. " +
            $"Profile '{profile}' requires explicit state validation. " +
            "Register a validator using services.AddStateValidator<TState, TValidator>() " +
            "or set RequireValidator = false (not recommended for production).",
            profile,
            new[] { $"Missing validator for {stateTypeName}" });
    }

    /// <summary>
    /// Creates an exception for missing signing key with message signing enabled.
    /// </summary>
    public static SecurityConfigurationException MissingSigningKey(
        string component,
        SecurityProfile profile)
    {
        return new SecurityConfigurationException(
            component,
            $"Message signing is enabled but no signing key is configured. " +
            $"Profile '{profile}' requires explicit key configuration. " +
            "Either set SigningKey, enable DeriveKeyFromOrigin, or disable message signing.",
            profile,
            new[] { "Message signing enabled without shared key" });
    }

    /// <summary>
    /// Creates an exception for insecure configuration in production.
    /// </summary>
    public static SecurityConfigurationException InsecureProductionConfig(
        string component,
        string issue,
        string recommendation)
    {
        return new SecurityConfigurationException(
            component,
            $"Insecure configuration detected in production: {issue}. {recommendation}",
            SecurityProfile.Production,
            new[] { issue });
    }

    private static string FormatMessage(
        string component,
        string message,
        IReadOnlyList<string>? errors,
        IReadOnlyList<string>? warnings)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{component}] Security Configuration Error: {message}");

        if (errors?.Count > 0)
        {
            sb.AppendLine("Errors:");
            foreach (var error in errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        if (warnings?.Count > 0)
        {
            sb.AppendLine("Warnings:");
            foreach (var warning in warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
