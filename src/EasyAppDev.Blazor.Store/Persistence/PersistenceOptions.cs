using System.Text.Json;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Configuration options for state persistence.
/// </summary>
/// <typeparam name="TState">The type of state being persisted.</typeparam>
public class PersistenceOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the storage key for persisting state.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets the debounce duration in milliseconds.
    /// When set to a value greater than 0, saves are debounced to reduce storage writes.
    /// Default: 0 (no debounce).
    /// </summary>
    public int DebounceMs { get; init; } = 0;

    /// <summary>
    /// Gets or sets the JSON serialization options.
    /// Default: CamelCase property naming.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; init; }

    /// <summary>
    /// Gets or sets whether to hydrate state on store initialization.
    /// Default: true.
    /// </summary>
    public bool HydrateOnInit { get; init; } = true;

    /// <summary>
    /// Gets or sets the callback invoked when hydration succeeds.
    /// Receives the loaded state.
    /// </summary>
    public Action<TState>? OnHydrationSuccess { get; init; }

    /// <summary>
    /// Gets or sets the callback invoked when hydration fails.
    /// Receives the exception that occurred.
    /// </summary>
    public Action<Exception>? OnHydrationFailure { get; init; }

    /// <summary>
    /// Gets or sets the callback invoked when no persisted state is found.
    /// </summary>
    public Action? OnHydrationSkipped { get; init; }

    /// <summary>
    /// Gets or sets a predicate that determines whether to persist a state change.
    /// Parameters: previous state, current state, action name.
    /// Return true to persist, false to skip.
    /// Default: always persist.
    /// </summary>
    public Func<TState, TState, string?, bool>? ShouldPersist { get; init; }

    /// <summary>
    /// Gets or sets a function to transform the state when loading from storage.
    /// Useful for clearing sensitive data or migrating state schema.
    /// </summary>
    public Func<TState, TState>? TransformOnLoad { get; init; }

    /// <summary>
    /// Gets or sets a function to transform the state before saving to storage.
    /// Useful for excluding sensitive data from persistence.
    /// </summary>
    public Func<TState, TState>? TransformOnSave { get; init; }

    /// <summary>
    /// Gets or sets the state validator for validating loaded state.
    /// Validates state after deserialization from storage.
    /// Default: NoOpStateValidator (accepts all states).
    /// </summary>
    public IStateValidator<TState>? StateValidator { get; init; }

    /// <summary>
    /// Gets or sets whether to reject invalid states from storage.
    /// If true, invalid states are not applied and OnHydrationFailure is called.
    /// If false, invalid states are logged but still applied.
    /// Default: true.
    /// </summary>
    public bool RejectInvalidState { get; init; } = true;

    /// <summary>
    /// Gets or sets a callback invoked when state validation fails.
    /// </summary>
    public Action<StateValidationResult>? OnValidationFailed { get; init; }

    /// <summary>
    /// Gets or sets whether to enable HMAC-based integrity checking for persisted state.
    /// When enabled, state is signed before saving and verified on load to detect tampering.
    /// This prevents malicious modification of persisted state in browser storage.
    /// Default: true.
    /// </summary>
    /// <remarks>
    /// Security implications:
    /// - Enabled (recommended): Detects tampered state, prevents loading compromised data
    /// - Disabled: Allows any state to load, vulnerable to XSS-based state injection
    /// The signing key is auto-generated per session by default. For persistent keys across sessions,
    /// provide a custom key via SigningKey property.
    /// </remarks>
    public bool EnableIntegrityCheck { get; init; } = true;

    /// <summary>
    /// Gets or sets the HMAC signing key for integrity verification.
    /// If null, a random key is generated per session (state won't be recoverable after app reload).
    /// Provide a consistent key if you need to verify state across sessions.
    /// </summary>
    /// <remarks>
    /// Key requirements:
    /// - Must be at least 32 bytes
    /// - Should be stored securely (not hardcoded in client code)
    /// - Consider deriving from user session or app secret
    /// </remarks>
    public byte[]? SigningKey { get; init; }

    /// <summary>
    /// Gets or sets the maximum allowed size of serialized state in bytes.
    /// Prevents quota exhaustion attacks and excessive storage usage.
    /// Default: 1048576 (1 MB).
    /// </summary>
    /// <remarks>
    /// Browser storage limits:
    /// - LocalStorage: typically 5-10 MB per origin
    /// - SessionStorage: typically 5-10 MB per origin
    /// Setting a reasonable limit prevents a single state from consuming all available quota.
    /// </remarks>
    public int MaxStateSize { get; init; } = 1_048_576; // 1 MB

    /// <summary>
    /// Gets or sets whether to filter sensitive data before persisting state.
    /// When enabled, properties marked with [SensitiveData] and common sensitive field names
    /// (Password, Token, ApiKey, etc.) are replaced with "[FILTERED]" before storage.
    /// Default: true.
    /// </summary>
    /// <remarks>
    /// Security implications:
    /// - Enabled (recommended): Prevents sensitive data from being stored in browser storage
    /// - Disabled: Sensitive data persists in plaintext, vulnerable to XSS attacks
    /// Note: This is a best-effort filter. Always avoid putting truly sensitive data in client state.
    /// </remarks>
    public bool FilterSensitiveData { get; init; } = true;

    /// <summary>
    /// Gets or sets the options for sensitive data filtering.
    /// Only applies when FilterSensitiveData is true.
    /// </summary>
    public SensitiveDataFilterOptions? SensitiveDataFilterOptions { get; init; }
}

/// <summary>
/// Factory for creating persistence options with a fluent API.
/// </summary>
public static class PersistenceOptions
{
    /// <summary>
    /// Creates a new persistence options builder.
    /// </summary>
    /// <typeparam name="TState">The type of state being persisted.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <returns>A new options instance.</returns>
    public static PersistenceOptions<TState> Create<TState>(string key) where TState : notnull
    {
        return new PersistenceOptions<TState> { Key = key };
    }
}
