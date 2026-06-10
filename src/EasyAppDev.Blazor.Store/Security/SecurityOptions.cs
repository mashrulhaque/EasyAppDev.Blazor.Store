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
    /// Gets or sets the state validator. Default is null (no validation).
    /// </summary>
    /// <remarks>
    /// <para><b>SECURITY WARNING:</b></para>
    /// <para>
    /// When accepting state from external sources (persistence, tab sync, server sync),
    /// you SHOULD configure a validator to prevent accepting malicious or corrupted state.
    /// </para>
    /// <para>
    /// If <see cref="RequireValidation"/> is true and this property is null,
    /// middleware initialization will throw an exception.
    /// </para>
    /// </remarks>
    public IStateValidator<TState>? StateValidator { get; set; }

    /// <summary>
    /// Gets or sets whether a validator is required.
    /// When true, middleware will throw if no validator is configured.
    /// Default is false for backward compatibility, but true is recommended for new projects.
    /// </summary>
    /// <remarks>
    /// Set to true in production environments to ensure state validation is not accidentally disabled.
    /// </remarks>
    public bool RequireValidation { get; set; }

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

    /// <summary>
    /// Validates the security configuration and throws if invalid.
    /// </summary>
    /// <param name="middlewareName">The name of the middleware for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown if RequireValidation is true but no validator is configured.</exception>
    public void Validate(string middlewareName)
    {
        if (RequireValidation && StateValidator == null)
        {
            throw new InvalidOperationException(
                $"{middlewareName} requires a StateValidator to be configured for security. " +
                $"Either provide a StateValidator via options.StateValidator = new YourValidator() " +
                $"or set RequireValidation = false (not recommended for production). " +
                $"Example: options.StateValidator = new MyStateValidator()");
        }
    }
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
/// Attribute to mark properties that should always be included in serialization,
/// overriding any sensitive data filtering rules.
/// </summary>
/// <remarks>
/// Use this attribute sparingly. Only apply it to properties that:
/// <list type="bullet">
/// <item>Have names matching filter keywords but are NOT actually sensitive</item>
/// <item>Are safe to expose in DevTools, logs, and diagnostics</item>
/// </list>
/// Example: A property named "TokenCount" that just holds a number, not a security token.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class AlwaysIncludeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reason why this property should always be included.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Options for filtering sensitive data from serialization.
/// Filtering is enabled by default to prevent accidental exposure of sensitive information
/// in DevTools, diagnostics, persistence, and synchronization features.
/// </summary>
/// <remarks>
/// When enabled, properties will be filtered if:
/// <list type="bullet">
/// <item>They are marked with <see cref="SensitiveDataAttribute"/></item>
/// <item>Their name matches any entry in <see cref="FilteredPropertyNames"/></item>
/// <item>Their name contains any keyword from <see cref="FilteredPropertyNames"/> as a whole
/// camelCase/underscore token (case-insensitive) when <see cref="UseExactMatch"/> is false</item>
/// <item>Their name matches any pattern in <see cref="FilteredPropertyPatterns"/> (regex)</item>
/// </list>
/// Filtered string properties are replaced with <see cref="ReplacementValue"/>; filtered
/// non-string properties are replaced with the default value of their type so the produced
/// JSON always deserializes back into the original state type.
/// Filtering applies recursively to nested objects during serialization, up to <see cref="MaxRecursionDepth"/>.
/// Properties marked with <see cref="AlwaysIncludeAttribute"/> are never filtered.
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
    /// Property names are matched case-insensitively.
    /// When <see cref="UseExactMatch"/> is false (default), token-boundary matching is used:
    /// the property name is split on camelCase/underscore boundaries and a keyword matches
    /// whole tokens or consecutive token sequences. For example "Pin" matches "Pin" and
    /// "UserPin" but NOT "ShippingAddress"; "CardNumber" matches "CreditCardNumber".
    /// Note this is deliberately conservative: "TokenCount" contains the token "Token" and
    /// IS filtered - use <see cref="AlwaysIncludeAttribute"/> to opt such properties out.
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
        "Pin",
        "EncryptionKey",
        "ConnectionString",
        "BearerToken",
        "AuthToken",
        "SessionId",
        "SessionToken"
    };

    /// <summary>
    /// Gets or sets whether to use exact match for property names.
    /// When true, property names must exactly match entries in <see cref="FilteredPropertyNames"/>.
    /// When false (default), token-boundary matching is used (keywords match whole
    /// camelCase/underscore tokens or consecutive token sequences within the property name).
    /// </summary>
    public bool UseExactMatch { get; set; } = false;

    /// <summary>
    /// Gets or sets regex patterns for filtering properties.
    /// Properties whose names match any pattern will be filtered.
    /// Patterns are evaluated case-insensitively.
    /// </summary>
    /// <remarks>
    /// Use this for complex matching scenarios. Example patterns:
    /// <list type="bullet">
    /// <item><c>^.*Key$</c> - matches any property ending with "Key"</item>
    /// <item><c>^(api|auth).*</c> - matches properties starting with "api" or "auth"</item>
    /// </list>
    /// </remarks>
    public List<string> FilteredPropertyPatterns { get; set; } = new();

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

    /// <summary>
    /// Gets or sets the maximum recursion depth for filtering nested objects.
    /// Prevents stack overflow from deeply nested or circular object graphs.
    /// Default is 32.
    /// </summary>
    public int MaxRecursionDepth { get; set; } = 32;
}
