// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Predefined security profiles for common deployment scenarios.
/// Use these profiles to quickly configure appropriate security settings.
/// </summary>
public enum SecurityProfile
{
    /// <summary>
    /// Development profile: DevTools enabled, permissive validation, verbose logging.
    /// Suitable for local development and debugging.
    /// </summary>
    /// <remarks>
    /// Features enabled:
    /// - DevTools integration (DEBUG builds only)
    /// - Logging middleware
    /// - Sensitive data filtering (warns but doesn't block)
    /// - No message signing required
    /// - State validation optional
    /// </remarks>
    Development,

    /// <summary>
    /// Production profile: Security-first configuration for deployed applications.
    /// All security features enabled with strict validation.
    /// </summary>
    /// <remarks>
    /// Features enabled:
    /// - No DevTools (even in DEBUG builds)
    /// - Logging middleware (errors only)
    /// - Sensitive data filtering (strict)
    /// - Message signing required for TabSync/ServerSync
    /// - State validation required
    /// - Fail-fast on security misconfigurations
    /// </remarks>
    Production,

    /// <summary>
    /// Strict profile: Maximum security with zero tolerance for misconfigurations.
    /// Use for high-security applications handling sensitive data.
    /// </summary>
    /// <remarks>
    /// Features enabled:
    /// - All Production features
    /// - Throws on any security warning
    /// - Requires explicit validator registration
    /// - Requires explicit signing key configuration
    /// - Reduced message age tolerance (10s instead of 30s)
    /// - Audit logging enabled
    /// </remarks>
    Strict,

    /// <summary>
    /// Custom profile: No automatic configuration, all settings manual.
    /// Use when you need fine-grained control over each security feature.
    /// </summary>
    Custom
}

/// <summary>
/// Configuration options for secure store registration.
/// </summary>
public sealed class SecureStoreOptions
{
    /// <summary>
    /// Gets or sets the security profile to apply.
    /// Default is <see cref="SecurityProfile.Development"/> when ASPNETCORE_ENVIRONMENT is "Development",
    /// otherwise <see cref="SecurityProfile.Production"/>.
    /// </summary>
    public SecurityProfile? Profile { get; set; }

    /// <summary>
    /// Gets or sets the store name for DevTools and logging.
    /// If not set, the state type name is used.
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// Gets or sets the persistence key for localStorage/sessionStorage.
    /// If null, persistence is disabled.
    /// </summary>
    public string? PersistenceKey { get; set; }

    /// <summary>
    /// Gets or sets whether to enable cross-tab synchronization.
    /// Default is false.
    /// </summary>
    public bool EnableTabSync { get; set; }

    /// <summary>
    /// Gets or sets the TabSync channel name.
    /// If null, defaults to "{StoreName}-sync".
    /// </summary>
    public string? TabSyncChannel { get; set; }

    /// <summary>
    /// Gets or sets whether to require a registered state validator.
    /// When true (default for Production/Strict), throws if no validator is found in DI.
    /// </summary>
    public bool? RequireValidator { get; set; }

    /// <summary>
    /// Gets or sets whether to enable undo/redo history.
    /// Default is false.
    /// </summary>
    public bool EnableHistory { get; set; }

    /// <summary>
    /// Gets or sets the maximum history entries.
    /// Only applies when <see cref="EnableHistory"/> is true.
    /// Default is 50.
    /// </summary>
    public int MaxHistoryEntries { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum history memory in MB.
    /// Only applies when <see cref="EnableHistory"/> is true.
    /// Default is 10.
    /// </summary>
    public int MaxHistoryMemoryMB { get; set; } = 10;

    /// <summary>
    /// Gets or sets the signing key for persistence and TabSync integrity checks.
    /// If null, a key is derived from the application origin.
    /// </summary>
    /// <remarks>
    /// For persistence to survive app restarts, provide a permanent key.
    /// Use <see cref="MessageSigner.DeriveKeyFromPassphrase"/> to generate a consistent key.
    /// </remarks>
    public byte[]? SigningKey { get; set; }

    /// <summary>
    /// Gets or sets whether to use scoped store registration.
    /// Set to true for Blazor Server with JS interop features (DevTools, Persistence, TabSync).
    /// Default is false (singleton).
    /// </summary>
    public bool UseScoped { get; set; }

    /// <summary>
    /// Gets or sets a transformation function applied before persisting state.
    /// Use to exclude sensitive fields from storage.
    /// </summary>
    public Func<object, object>? TransformOnSave { get; set; }

    /// <summary>
    /// Gets or sets additional configuration to apply after profile defaults.
    /// </summary>
    public Action<SecureStoreOptions>? AdditionalConfiguration { get; set; }

    /// <summary>
    /// Gets or sets whether to throw exceptions on security configuration warnings.
    /// Default is true for Strict profile, false otherwise.
    /// </summary>
    public bool? ThrowOnSecurityWarnings { get; set; }

    /// <summary>
    /// Gets or sets the debounce interval for persistence in milliseconds.
    /// Default is 100.
    /// </summary>
    public int PersistenceDebounceMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to filter sensitive data in DevTools/logging.
    /// Default is true.
    /// </summary>
    public bool FilterSensitiveData { get; set; } = true;
}
