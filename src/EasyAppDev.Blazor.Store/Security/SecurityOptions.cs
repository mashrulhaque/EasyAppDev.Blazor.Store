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
/// </summary>
public sealed class SensitiveDataFilterOptions
{
    /// <summary>
    /// Gets or sets whether to filter properties marked with [SensitiveData].
    /// Default is true.
    /// </summary>
    public bool FilterSensitiveAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets additional property names to filter.
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
        "Credentials"
    };

    /// <summary>
    /// Gets or sets the replacement value for filtered properties.
    /// Default is "[FILTERED]".
    /// </summary>
    public string ReplacementValue { get; set; } = "[FILTERED]";

    /// <summary>
    /// Gets or sets whether filtering is enabled. Default is false for backward compatibility.
    /// </summary>
    public bool Enabled { get; set; }
}
