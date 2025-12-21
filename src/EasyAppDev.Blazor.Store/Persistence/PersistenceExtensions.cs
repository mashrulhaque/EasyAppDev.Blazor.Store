// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Extension methods for configuring secure persistence.
/// </summary>
public static class PersistenceExtensions
{
    /// <summary>
    /// Configures persistence with all security features enabled.
    /// Includes integrity checking, sensitive data filtering, and size limits.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="maxSizeBytes">Maximum allowed state size in bytes (default: 1 MB).</param>
    /// <param name="signingKey">Optional custom signing key for integrity checks. If null, a random key is generated.</param>
    /// <returns>The configured builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddStore(
    ///     new AppState(),
    ///     (store, sp) => store.WithSecurePersistence(
    ///         new LocalStorageProvider(sp.GetRequiredService&lt;IJSRuntime&gt;()),
    ///         "app-state"
    ///     )
    /// );
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithSecurePersistence<TState>(
        this StoreBuilder<TState> builder,
        IPersistenceProvider provider,
        string key,
        int maxSizeBytes = 1_048_576,
        byte[]? signingKey = null)
        where TState : notnull
    {
        var options = new PersistenceOptions<TState>
        {
            Key = key,
            EnableIntegrityCheck = true,
            SigningKey = signingKey,
            MaxStateSize = maxSizeBytes,
            FilterSensitiveData = true,
            HydrateOnInit = true
        };

        return builder.WithPersistence(provider, options);
    }

    /// <summary>
    /// Configures persistence with custom security options.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The store builder.</param>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="configure">Action to configure persistence options.</param>
    /// <returns>The configured builder for chaining.</returns>
    /// <example>
    /// <code>
    /// .WithSecurePersistence(provider, "app-state", opts => opts
    ///     .WithIntegrityCheck(customKey)
    ///     .WithSizeLimit(2_097_152) // 2 MB
    ///     .WithSensitiveDataFiltering()
    ///     .WithDebounce(500))
    /// </code>
    /// </example>
    public static StoreBuilder<TState> WithSecurePersistence<TState>(
        this StoreBuilder<TState> builder,
        IPersistenceProvider provider,
        string key,
        Action<PersistenceOptionsBuilder<TState>> configure)
        where TState : notnull
    {
        var optionsBuilder = new PersistenceOptionsBuilder<TState>(key);
        configure(optionsBuilder);
        var options = optionsBuilder.Build();

        return builder.WithPersistence(provider, options);
    }

    /// <summary>
    /// Adds integrity checking with a custom signing key.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="signingKey">The signing key (must be at least 32 bytes).</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithIntegrityCheck<TState>(
        this PersistenceOptionsBuilder<TState> builder,
        byte[] signingKey)
        where TState : notnull
    {
        builder.EnableIntegrityCheck = true;
        builder.SigningKey = signingKey;
        return builder;
    }

    /// <summary>
    /// Enables integrity checking with an auto-generated key.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithIntegrityCheck<TState>(
        this PersistenceOptionsBuilder<TState> builder)
        where TState : notnull
    {
        builder.EnableIntegrityCheck = true;
        builder.SigningKey = null;
        return builder;
    }

    /// <summary>
    /// Disables integrity checking (not recommended for production).
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithoutIntegrityCheck<TState>(
        this PersistenceOptionsBuilder<TState> builder)
        where TState : notnull
    {
        builder.EnableIntegrityCheck = false;
        return builder;
    }

    /// <summary>
    /// Sets the maximum allowed state size.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="maxSizeBytes">The maximum size in bytes.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithSizeLimit<TState>(
        this PersistenceOptionsBuilder<TState> builder,
        int maxSizeBytes)
        where TState : notnull
    {
        builder.MaxStateSize = maxSizeBytes;
        return builder;
    }

    /// <summary>
    /// Enables sensitive data filtering with default options.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithSensitiveDataFiltering<TState>(
        this PersistenceOptionsBuilder<TState> builder)
        where TState : notnull
    {
        builder.FilterSensitiveData = true;
        return builder;
    }

    /// <summary>
    /// Enables sensitive data filtering with custom options.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="filterOptions">The filter options.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithSensitiveDataFiltering<TState>(
        this PersistenceOptionsBuilder<TState> builder,
        SensitiveDataFilterOptions filterOptions)
        where TState : notnull
    {
        builder.FilterSensitiveData = true;
        builder.SensitiveDataFilterOptions = filterOptions;
        return builder;
    }

    /// <summary>
    /// Disables sensitive data filtering (not recommended for production).
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithoutSensitiveDataFiltering<TState>(
        this PersistenceOptionsBuilder<TState> builder)
        where TState : notnull
    {
        builder.FilterSensitiveData = false;
        return builder;
    }

    /// <summary>
    /// Configures debouncing for persistence writes.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="debounceMs">The debounce duration in milliseconds.</param>
    /// <returns>The builder for chaining.</returns>
    public static PersistenceOptionsBuilder<TState> WithDebounce<TState>(
        this PersistenceOptionsBuilder<TState> builder,
        int debounceMs)
        where TState : notnull
    {
        builder.DebounceMs = debounceMs;
        return builder;
    }
}

/// <summary>
/// Builder for creating persistence options with a fluent API.
/// </summary>
/// <typeparam name="TState">The type of state being persisted.</typeparam>
public class PersistenceOptionsBuilder<TState> where TState : notnull
{
    private readonly string _key;

    /// <summary>
    /// Gets or sets whether to enable integrity checking.
    /// </summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>
    /// Gets or sets the signing key for integrity verification.
    /// </summary>
    public byte[]? SigningKey { get; set; }

    /// <summary>
    /// Gets or sets the maximum state size in bytes.
    /// </summary>
    public int MaxStateSize { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets whether to filter sensitive data.
    /// </summary>
    public bool FilterSensitiveData { get; set; } = true;

    /// <summary>
    /// Gets or sets the sensitive data filter options.
    /// </summary>
    public SensitiveDataFilterOptions? SensitiveDataFilterOptions { get; set; }

    /// <summary>
    /// Gets or sets the debounce duration in milliseconds.
    /// </summary>
    public int DebounceMs { get; set; }

    /// <summary>
    /// Gets or sets whether to hydrate state on initialization.
    /// </summary>
    public bool HydrateOnInit { get; set; } = true;

    /// <summary>
    /// Gets or sets the state validator.
    /// </summary>
    public IStateValidator<TState>? StateValidator { get; set; }

    /// <summary>
    /// Gets or sets whether to reject invalid states.
    /// </summary>
    public bool RejectInvalidState { get; set; } = true;

    /// <summary>
    /// Gets or sets the transformation to apply when loading state.
    /// </summary>
    public Func<TState, TState>? TransformOnLoad { get; set; }

    /// <summary>
    /// Gets or sets the transformation to apply when saving state.
    /// </summary>
    public Func<TState, TState>? TransformOnSave { get; set; }

    /// <summary>
    /// Gets or sets the callback for hydration success.
    /// </summary>
    public Action<TState>? OnHydrationSuccess { get; set; }

    /// <summary>
    /// Gets or sets the callback for hydration failure.
    /// </summary>
    public Action<Exception>? OnHydrationFailure { get; set; }

    /// <summary>
    /// Creates a new persistence options builder.
    /// </summary>
    /// <param name="key">The storage key.</param>
    public PersistenceOptionsBuilder(string key)
    {
        _key = key;
    }

    /// <summary>
    /// Builds the persistence options.
    /// </summary>
    /// <returns>The configured persistence options.</returns>
    public PersistenceOptions<TState> Build()
    {
        return new PersistenceOptions<TState>
        {
            Key = _key,
            EnableIntegrityCheck = EnableIntegrityCheck,
            SigningKey = SigningKey,
            MaxStateSize = MaxStateSize,
            FilterSensitiveData = FilterSensitiveData,
            SensitiveDataFilterOptions = SensitiveDataFilterOptions,
            DebounceMs = DebounceMs,
            HydrateOnInit = HydrateOnInit,
            StateValidator = StateValidator,
            RejectInvalidState = RejectInvalidState,
            TransformOnLoad = TransformOnLoad,
            TransformOnSave = TransformOnSave,
            OnHydrationSuccess = OnHydrationSuccess,
            OnHydrationFailure = OnHydrationFailure
        };
    }
}
