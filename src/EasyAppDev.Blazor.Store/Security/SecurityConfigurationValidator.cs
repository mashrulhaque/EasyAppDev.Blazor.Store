// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Severity level for security configuration warnings.
/// </summary>
public enum SecurityWarningSeverity
{
    /// <summary>
    /// Informational - configuration choice that might not be optimal.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - configuration that may lead to security issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - configuration that creates security vulnerabilities.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - configuration that must be fixed before production use.
    /// </summary>
    Critical
}

/// <summary>
/// Represents a security configuration warning or issue.
/// </summary>
public sealed record SecurityConfigurationWarning
{
    /// <summary>
    /// Gets the severity level of this warning.
    /// </summary>
    public SecurityWarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets the component that generated this warning (e.g., "TabSync", "ServerSync").
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// Gets the warning code for programmatic identification.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the recommended remediation action.
    /// </summary>
    public string? Remediation { get; init; }
}

/// <summary>
/// Result of security configuration validation.
/// </summary>
public sealed class SecurityConfigurationResult
{
    /// <summary>
    /// Gets the list of warnings found during validation.
    /// </summary>
    public IReadOnlyList<SecurityConfigurationWarning> Warnings { get; }

    /// <summary>
    /// Gets whether the configuration is valid (no errors or critical issues).
    /// </summary>
    public bool IsValid => !Warnings.Any(w =>
        w.Severity == SecurityWarningSeverity.Error ||
        w.Severity == SecurityWarningSeverity.Critical);

    /// <summary>
    /// Gets whether the configuration has any warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    /// <param name="warnings">The warnings found during validation.</param>
    public SecurityConfigurationResult(IReadOnlyList<SecurityConfigurationWarning> warnings)
    {
        Warnings = warnings;
    }

    /// <summary>
    /// Creates an empty (valid) result.
    /// </summary>
    public static SecurityConfigurationResult Success() => new(Array.Empty<SecurityConfigurationWarning>());
}

/// <summary>
/// Interface for validating security configurations.
/// </summary>
public interface ISecurityConfigurationValidator
{
    /// <summary>
    /// Validates the security configuration and returns any warnings.
    /// </summary>
    /// <returns>Validation result containing any warnings or errors.</returns>
    SecurityConfigurationResult Validate();
}

/// <summary>
/// Validates TabSync security configuration.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
public sealed class TabSyncConfigurationValidator<TState> : ISecurityConfigurationValidator
    where TState : notnull
{
    private readonly TabSync.TabSyncOptions _options;
    private readonly IStateValidator<TState>? _storeValidator;
    private readonly bool _isProduction;

    /// <summary>
    /// Creates a new TabSync configuration validator.
    /// </summary>
    /// <param name="options">The TabSync options to validate.</param>
    /// <param name="storeValidator">The store's configured validator, if any.</param>
    /// <param name="isProduction">Whether this is a production environment.</param>
    public TabSyncConfigurationValidator(
        TabSync.TabSyncOptions options,
        IStateValidator<TState>? storeValidator = null,
        bool isProduction = false)
    {
        _options = options;
        _storeValidator = storeValidator;
        _isProduction = isProduction;
    }

    /// <inheritdoc />
    public SecurityConfigurationResult Validate()
    {
        var warnings = new List<SecurityConfigurationWarning>();

        // Check message signing configuration
        if (!_options.EnableMessageSigning && _isProduction)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "TabSync",
                Code = "TABSYNC_NO_SIGNING",
                Message = "Message signing is disabled in what appears to be a production environment. " +
                         "This allows malicious tabs to inject forged state updates.",
                Remediation = "Enable message signing: options.EnableMessageSigning = true"
            });
        }

        if (_options.EnableMessageSigning && _options.SigningKey == null && !_options.DeriveKeyFromOrigin)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Error,
                Component = "TabSync",
                Code = "TABSYNC_NO_SHARED_KEY",
                Message = "Message signing is enabled but no shared signing key is configured. " +
                         "Each tab will generate its own random key, causing all cross-tab signature verification to fail.",
                Remediation = "Either set SigningKey to a shared key derived from MessageSigner.DeriveKeyFromSeed(), " +
                             "or set DeriveKeyFromOrigin = true to auto-derive from window.location.origin"
            });
        }

        // Check state validation
        if (_options.StateValidator == null && _storeValidator == null)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = _isProduction ? SecurityWarningSeverity.Warning : SecurityWarningSeverity.Info,
                Component = "TabSync",
                Code = "TABSYNC_NO_VALIDATOR",
                Message = "No state validator is configured for TabSync. " +
                         "State received from other tabs will be applied without validation.",
                Remediation = "Configure a state validator: builder.WithValidator(new MyValidator()) or " +
                             "options.StateValidator = new StateValidatorWrapper<TState>(validator)"
            });
        }

        // Check timestamp validation
        if (!_options.ValidateTimestamp)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "TabSync",
                Code = "TABSYNC_NO_TIMESTAMP",
                Message = "Timestamp validation is disabled. This makes the system vulnerable to replay attacks.",
                Remediation = "Enable timestamp validation: options.ValidateTimestamp = true"
            });
        }

        // Check for overly large message size limit
        if (_options.MaxMessageSizeBytes > 10_000_000) // 10MB
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "TabSync",
                Code = "TABSYNC_LARGE_MESSAGE_LIMIT",
                Message = $"Maximum message size is set very high ({_options.MaxMessageSizeBytes / 1_000_000}MB). " +
                         "This could allow denial-of-service attacks via large payloads.",
                Remediation = "Consider reducing MaxMessageSizeBytes to a reasonable limit (e.g., 1-5MB)"
            });
        }

        return new SecurityConfigurationResult(warnings);
    }
}

/// <summary>
/// Validates ServerSync security configuration.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
public sealed class ServerSyncConfigurationValidator<TState> : ISecurityConfigurationValidator
    where TState : notnull
{
    private readonly ServerSync.ServerSyncOptions<TState> _options;
    private readonly bool _isProduction;

    /// <summary>
    /// Creates a new ServerSync configuration validator.
    /// </summary>
    /// <param name="options">The ServerSync options to validate.</param>
    /// <param name="isProduction">Whether this is a production environment.</param>
    public ServerSyncConfigurationValidator(
        ServerSync.ServerSyncOptions<TState> options,
        bool isProduction = false)
    {
        _options = options;
        _isProduction = isProduction;
    }

    /// <inheritdoc />
    public SecurityConfigurationResult Validate()
    {
        var warnings = new List<SecurityConfigurationWarning>();

        // Check state validation
        if (_options.RequireValidation && _options.StateValidator == null)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Critical,
                Component = "ServerSync",
                Code = "SERVERSYNC_REQUIRE_VALIDATOR",
                Message = "RequireValidation is true but no StateValidator is configured. " +
                         "Middleware initialization will fail.",
                Remediation = "Configure a state validator: options.StateValidator = new MyValidator()"
            });
        }

        if (_options.StateValidator == null && !_options.RequireValidation)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = _isProduction ? SecurityWarningSeverity.Warning : SecurityWarningSeverity.Info,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_VALIDATOR",
                Message = "No state validator is configured and RequireValidation is false. " +
                         "State received from the server will be applied without validation.",
                Remediation = "Configure a state validator and set RequireValidation = true"
            });
        }

        // Check message signing
        if (!_options.EnableMessageSigning && _isProduction)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Info,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_SIGNING",
                Message = "Message signing is disabled. Consider enabling it for additional integrity protection.",
                Remediation = "Enable message signing: options.EnableMessageSigning = true"
            });
        }

        // Check rate limiting
        if (_options.RateLimitPerSecond == 0)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = _isProduction ? SecurityWarningSeverity.Warning : SecurityWarningSeverity.Info,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_RATE_LIMIT",
                Message = "Rate limiting is disabled. This could allow denial-of-service attacks.",
                Remediation = "Enable rate limiting: options.RateLimitPerSecond = 10 (or appropriate value)"
            });
        }

        // Check session validation
        if (!_options.RequireSessionValidation && _isProduction)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_SESSION_VALIDATION",
                Message = "Session validation on reconnect is disabled. " +
                         "This could allow session hijacking attacks.",
                Remediation = "Enable session validation: options.RequireSessionValidation = true"
            });
        }

        // Check version jump detection
        if (!_options.RejectSuspiciousVersions && _isProduction)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Info,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_VERSION_CHECK",
                Message = "Suspicious version jump detection is disabled. " +
                         "Large version jumps may bypass conflict resolution.",
                Remediation = "Enable version checking: options.RejectSuspiciousVersions = true"
            });
        }

        // Check sensitive data filtering
        if (!_options.FilterSensitiveData)
        {
            warnings.Add(new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "ServerSync",
                Code = "SERVERSYNC_NO_FILTER",
                Message = "Sensitive data filtering is disabled. " +
                         "Properties marked with [SensitiveData] may be sent to the server.",
                Remediation = "Enable filtering: options.FilterSensitiveData = true"
            });
        }

        return new SecurityConfigurationResult(warnings);
    }
}

/// <summary>
/// Extension methods for security configuration validation.
/// </summary>
public static class SecurityConfigurationValidatorExtensions
{
    /// <summary>
    /// Validates the security configuration and logs any warnings.
    /// </summary>
    /// <param name="validator">The validator to use.</param>
    /// <param name="logger">Optional logger to write warnings to.</param>
    /// <param name="throwOnError">If true, throws an exception for Error or Critical severity warnings.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown if throwOnError is true and errors are found.</exception>
    public static SecurityConfigurationResult ValidateAndLog(
        this ISecurityConfigurationValidator validator,
        ILogger? logger = null,
        bool throwOnError = false)
    {
        var result = validator.Validate();

        foreach (var warning in result.Warnings)
        {
            var message = $"[{warning.Code}] {warning.Message}";
            if (warning.Remediation != null)
            {
                message += $" Remediation: {warning.Remediation}";
            }

            switch (warning.Severity)
            {
                case SecurityWarningSeverity.Info:
                    logger?.LogInformation("[Security] {Message}", message);
                    break;
                case SecurityWarningSeverity.Warning:
                    logger?.LogWarning("[Security] {Message}", message);
                    break;
                case SecurityWarningSeverity.Error:
                case SecurityWarningSeverity.Critical:
                    logger?.LogError("[Security] {Message}", message);
                    break;
            }
        }

        if (throwOnError && !result.IsValid)
        {
            var errors = result.Warnings
                .Where(w => w.Severity == SecurityWarningSeverity.Error ||
                           w.Severity == SecurityWarningSeverity.Critical)
                .ToList();

            throw new InvalidOperationException(
                $"Security configuration validation failed with {errors.Count} error(s): " +
                string.Join("; ", errors.Select(e => $"[{e.Code}] {e.Message}")));
        }

        return result;
    }

    /// <summary>
    /// Determines if the current environment appears to be production.
    /// Uses common environment detection patterns.
    /// </summary>
    /// <returns>True if this appears to be a production environment.</returns>
    public static bool IsProductionEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                  Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.IsNullOrEmpty(env))
        {
#if DEBUG
            return false;
#else
            return true; // Assume production if not DEBUG and no environment variable
#endif
        }

        return env.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
               env.Equals("Staging", StringComparison.OrdinalIgnoreCase);
    }
}
