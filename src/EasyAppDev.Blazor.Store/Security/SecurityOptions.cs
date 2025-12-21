// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Security options for state management.
/// Apply to persistence, tab sync, and server sync middleware.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public sealed class SecurityOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the state validator. Default is NoOpStateValidator (accepts all).
    /// </summary>
    public IStateValidator<TState> StateValidator { get; set; } = NoOpStateValidator<TState>.Instance;

    /// <summary>
    /// Gets or sets whether to reject invalid states.
    /// If false, invalid states are logged but accepted. Default is true.
    /// </summary>
    public bool RejectInvalidState { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback invoked when validation fails.
    /// </summary>
    public Action<StateValidationResult>? OnValidationFailed { get; set; }

    /// <summary>
    /// Gets or sets the fallback state to use when validation fails and RejectInvalidState is true.
    /// If null, the current state is preserved.
    /// </summary>
    public TState? FallbackState { get; set; }
}

/// <summary>
/// Attribute to mark properties that should be excluded from DevTools and persistence.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reason why this data is sensitive.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Options for filtering sensitive data from serialization.
/// Filtering is enabled by default to prevent accidental exposure of sensitive information
/// in DevTools, diagnostics, persistence, and synchronization features.
/// </summary>
/// <remarks>
/// When enabled, properties will be replaced with the <see cref="ReplacementValue"/> if:
/// <list type="bullet">
/// <item>They are marked with <see cref="SensitiveDataAttribute"/></item>
/// <item>Their name matches any entry in <see cref="FilteredPropertyNames"/></item>
/// <item>Their name contains any keyword from <see cref="FilteredPropertyNames"/> (case-insensitive)</item>
/// </list>
/// Filtering applies recursively to nested objects during serialization.
/// </remarks>
public sealed class SensitiveDataFilterOptions
{
    /// <summary>
    /// Gets or sets whether to filter properties marked with [SensitiveData].
    /// Default is true.
    /// </summary>
    public bool FilterSensitiveAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets additional property names to filter.
    /// Property names are matched case-insensitively and support partial matching.
    /// Default list includes common sensitive field names.
    /// </summary>
    public HashSet<string> FilteredPropertyNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Secret",
        "Token",
        "ApiKey",
        "AccessToken",
        "RefreshToken",
        "PrivateKey",
        "Credential",
        "Credentials",
        "Ssn",
        "SocialSecurityNumber",
        "CreditCard",
        "CardNumber",
        "Cvv",
        "Pin"
    };

    /// <summary>
    /// Gets or sets the replacement value for filtered properties.
    /// Default is "[FILTERED]".
    /// </summary>
    public string ReplacementValue { get; set; } = "[FILTERED]";

    /// <summary>
    /// Gets or sets whether filtering is enabled.
    /// Default is true to prevent accidental exposure of sensitive data.
    /// Set to false only if you are certain your state contains no sensitive information.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum serialization size in bytes before rejecting.
    /// Prevents memory exhaustion from maliciously large payloads.
    /// Default is 1MB (1,048,576 bytes).
    /// </summary>
    public int MaxSerializationSizeBytes { get; set; } = 1_048_576;
}
