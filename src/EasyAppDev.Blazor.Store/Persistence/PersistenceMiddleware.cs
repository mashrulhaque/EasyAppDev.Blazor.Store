using System.Text;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Middleware that automatically persists state changes to storage.
/// </summary>
/// <typeparam name="TState">The type of state to persist.</typeparam>
public class PersistenceMiddleware<TState> : IMiddleware<TState>, IDisposable
    where TState : notnull
{
    private readonly IPersistenceProvider _provider;
    private readonly string _key;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _debounce;
    private readonly int _debounceMs;
    private readonly ILogger<PersistenceMiddleware<TState>>? _logger;
    private readonly PersistenceOptions<TState>? _options;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();
    private readonly MessageSigner? _messageSigner;
    private readonly JsonSerializerOptions? _filteredJsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="jsonOptions">Optional JSON serialization options.</param>
    /// <param name="debounceMs">Debounce duration in milliseconds (0 = no debounce).</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public PersistenceMiddleware(
        IPersistenceProvider provider,
        string key,
        JsonSerializerOptions? jsonOptions = null,
        int debounceMs = 0,
        ILogger<PersistenceMiddleware<TState>>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _debounceMs = debounceMs;
        _debounce = debounceMs > 0;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceMiddleware{TState}"/> class
    /// with full configuration options.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <param name="options">The persistence configuration options.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public PersistenceMiddleware(
        IPersistenceProvider provider,
        PersistenceOptions<TState> options,
        ILogger<PersistenceMiddleware<TState>>? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _key = options.Key;
        _jsonOptions = options.JsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _debounceMs = options.DebounceMs;
        _debounce = options.DebounceMs > 0;
        _logger = logger;

        // Initialize message signer if integrity check is enabled
        if (options.EnableIntegrityCheck)
        {
            if (options.SigningKey == null)
            {
                throw new InvalidOperationException(
                    "PersistenceOptions.EnableIntegrityCheck is enabled but no SigningKey was provided. " +
                    "A random per-process key would be generated, so every persisted state would fail " +
                    "integrity verification after an application restart and be silently discarded. " +
                    "Either supply a stable signing key of at least 32 bytes " +
                    "(e.g. options.SigningKey = MessageSigner.DeriveKeyFromSeed(\"your-app-name\") " +
                    "or a key provisioned by your server), or disable integrity checking " +
                    "(options.EnableIntegrityCheck = false / builder.WithoutIntegrityCheck()).");
            }

            _messageSigner = new MessageSigner(options.SigningKey);
        }

        // Initialize filtered JSON options if sensitive data filtering is enabled
        if (options.FilterSensitiveData)
        {
            var filterOptions = options.SensitiveDataFilterOptions ?? new SensitiveDataFilterOptions
            {
                Enabled = true
            };
            _filteredJsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(filterOptions);
        }
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task OnAfterUpdateAsync(
        TState previousState,
        TState currentState,
        string? action)
    {
        // Check if we should persist this change
        if (_options?.ShouldPersist != null && !_options.ShouldPersist(previousState, currentState, action))
        {
            return;
        }

        // Apply transformation before saving
        var stateToSave = _options?.TransformOnSave != null
            ? _options.TransformOnSave(currentState)
            : currentState;

        if (_debounce)
        {
            await DebouncedSaveAsync(stateToSave).ConfigureAwait(false);
        }
        else
        {
            await SaveStateAsync(stateToSave).ConfigureAwait(false);
        }
    }

    private Task DebouncedSaveAsync(TState state)
    {
        CancellationToken token;

        lock (_debounceLock)
        {
            // Cancel and dispose old CTS to prevent memory leak.
            // Cancelling the previous token coalesces pending saves so only
            // the latest state is written (last write wins).
            var oldCts = _debounceCts;
            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;

            oldCts?.Cancel();
            oldCts?.Dispose();
        }

        // Schedule the delayed save in the background so OnAfterUpdateAsync
        // returns promptly and does not stall store updates for the debounce
        // duration (updates run under the store lock).
        _ = RunDebouncedSaveAsync(state, token);
        return Task.CompletedTask;
    }

    private async Task RunDebouncedSaveAsync(TState state, CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounceMs, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                return;
            }

            await SaveStateAsync(state).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled, a newer update superseded this save
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in debounced persistence save for key: {Key}", _key);
        }
    }

    private async Task SaveStateAsync(TState state)
    {
        try
        {
            // Serialize state with optional sensitive data filtering
            var serializationOptions = _filteredJsonOptions ?? _jsonOptions;
            var stateJson = JsonSerializer.Serialize(state, serializationOptions);
            var stateBytes = Encoding.UTF8.GetByteCount(stateJson);

            // Check size limit
            if (_options != null && stateBytes > _options.MaxStateSize)
            {
                var ex = new StateSizeExceededException(stateBytes, _options.MaxStateSize);
                _logger?.LogError(ex, "State size exceeds limit for key: {Key}. Size: {Size:N0} bytes, Limit: {Limit:N0} bytes",
                    _key, stateBytes, _options.MaxStateSize);
                throw ex;
            }

            // Create wrapper with optional signature
            var wrapper = new PersistedStateWrapper
            {
                State = stateJson,
                Size = stateBytes,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Sign state if integrity check is enabled
            if (_messageSigner != null)
            {
                wrapper.Signature = _messageSigner.Sign(stateJson);
            }

            // Serialize and save wrapper
            var wrapperJson = JsonSerializer.Serialize(wrapper, _jsonOptions);

            // Check final serialized size including wrapper overhead (signature, timestamp)
            var totalBytes = Encoding.UTF8.GetByteCount(wrapperJson);
            if (_options != null && totalBytes > _options.MaxStateSize)
            {
                var ex = new StateSizeExceededException(totalBytes, _options.MaxStateSize);
                _logger?.LogError(ex, "Total payload size exceeds limit for key: {Key}. Size: {Size:N0} bytes (inner: {InnerSize:N0}), Limit: {Limit:N0} bytes",
                    _key, totalBytes, stateBytes, _options.MaxStateSize);
                throw ex;
            }

            await _provider.SaveAsync(_key, wrapperJson).ConfigureAwait(false);

            _logger?.LogDebug("Persisted state to key: {Key}. Size: {Size:N0} bytes (total: {TotalSize:N0}), Signed: {Signed}",
                _key, stateBytes, totalBytes, wrapper.Signature != null);
        }
        catch (StateSizeExceededException)
        {
            // Re-throw size exceptions
            throw;
        }
        catch (Exception ex) when (IsStorageQuotaException(ex))
        {
            // Storage quota exceeded - this is a critical error that callers should know about
            var quotaEx = new StorageQuotaExceededException(
                $"Storage quota exceeded while saving state to key '{_key}'. Consider clearing old data or using a larger storage provider.",
                ex);
            _logger?.LogError(quotaEx, "Storage quota exceeded for key: {Key}", _key);
            _options?.OnPersistenceError?.Invoke(quotaEx);
            throw quotaEx;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error persisting state to key: {Key}", _key);
            _options?.OnPersistenceError?.Invoke(ex);

            // Re-throw if configured to do so - allows callers to handle persistence failures
            if (_options?.ThrowOnPersistenceError == true)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Checks if an exception is related to storage quota exceeded.
    /// </summary>
    private static bool IsStorageQuotaException(Exception ex)
    {
        // Check for common quota-related exception messages
        var message = ex.Message?.ToLowerInvariant() ?? "";

        // Check exception type name first (most reliable)
        if (ex.GetType().Name.Contains("Quota"))
            return true;

        // Check for specific quota-related phrases (avoid overly broad patterns)
        return message.Contains("quota") ||
               message.Contains("storage full") ||
               message.Contains("quota exceeded") ||
               message.Contains("storage exceeded") ||
               message.Contains("localstorage") && message.Contains("full") ||
               message.Contains("dom exception 22") ||  // Safari quota error
               message.Contains("quotaexceedederror");  // Standard DOM exception name
    }

    /// <summary>
    /// Loads the persisted state if available.
    /// </summary>
    /// <returns>The persisted state or null if not found.</returns>
    public async Task<TState?> LoadStateAsync()
    {
        try
        {
            var json = await _provider.LoadAsync(_key).ConfigureAwait(false);
            if (json == null)
            {
                _options?.OnHydrationSkipped?.Invoke();
                return default;
            }

            // Try to deserialize as new format (with wrapper)
            string stateJson;
            PersistedStateWrapper? wrapper = null;

            try
            {
                wrapper = JsonSerializer.Deserialize<PersistedStateWrapper>(json, _jsonOptions);
            }
            catch
            {
                // Ignore deserialization errors, will fall back to legacy format
            }

            if (wrapper != null && !string.IsNullOrEmpty(wrapper.State))
            {
                // New format detected
                stateJson = wrapper.State;

                // Verify signature if integrity check is enabled
                if (_messageSigner != null && !string.IsNullOrEmpty(wrapper.Signature))
                {
                    if (!_messageSigner.Verify(stateJson, wrapper.Signature))
                    {
                        var integrityEx = new StateIntegrityException(
                            $"State integrity verification failed for key '{_key}'. The persisted state may have been tampered with.");

                        _logger?.LogError(integrityEx, "Integrity check failed for key: {Key}", _key);
                        _options?.OnHydrationFailure?.Invoke(integrityEx);

                        return default;
                    }

                    _logger?.LogDebug("State integrity verified for key: {Key}", _key);
                }
                else if (_messageSigner != null && string.IsNullOrEmpty(wrapper.Signature))
                {
                    // Wrapper exists but no signature - security bypass attempt or legacy migration
                    _logger?.LogWarning(
                        "State loaded without signature for key: {Key}. RequireSignature: {Required}",
                        _key,
                        _options?.RequireSignature ?? false);

                    // If RequireSignature is set, reject unsigned state
                    if (_options?.RequireSignature == true)
                    {
                        var integrityEx = new StateIntegrityException(
                            $"Unsigned state rejected for key '{_key}'. Integrity signing is required but no signature found.");
                        _logger?.LogError(integrityEx, "Unsigned state rejected for key: {Key}", _key);
                        _options.OnHydrationFailure?.Invoke(integrityEx);
                        return default;
                    }
                }

                _logger?.LogDebug("Loaded state from key: {Key}. Size: {Size:N0} bytes, Age: {Age}",
                    _key, wrapper.Size, DateTimeOffset.UtcNow - wrapper.Timestamp);
            }
            else
            {
                // Legacy format (plaintext JSON state) - backward compatibility
                stateJson = json;
                _logger?.LogDebug("Loaded legacy state format from key: {Key}. Consider re-saving to update to secured format.", _key);
            }

            var loadedState = JsonSerializer.Deserialize<TState>(stateJson, _jsonOptions);
            if (loadedState == null)
            {
                _options?.OnHydrationSkipped?.Invoke();
                return default;
            }

            // Validate loaded state if validator is configured
            if (_options?.StateValidator != null)
            {
                var validationResult = _options.StateValidator.Validate(loadedState);
                if (!validationResult.IsValid)
                {
                    _logger?.LogWarning(
                        "State validation failed for key {Key}: {Errors}",
                        _key,
                        string.Join(", ", validationResult.Errors));

                    _options.OnValidationFailed?.Invoke(validationResult with { Source = "Persistence" });

                    if (_options.RejectInvalidState)
                    {
                        _options.OnHydrationFailure?.Invoke(
                            new InvalidOperationException($"State validation failed: {string.Join(", ", validationResult.Errors)}"));
                        return default;
                    }
                }
            }

            // Apply transformation after loading
            var transformedState = _options?.TransformOnLoad != null
                ? _options.TransformOnLoad(loadedState)
                : loadedState;

            _options?.OnHydrationSuccess?.Invoke(transformedState);
            return transformedState;
        }
        catch (StateIntegrityException)
        {
            // Re-throw integrity exceptions
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading persisted state from key: {Key}", _key);
            _options?.OnHydrationFailure?.Invoke(ex);
            return default;
        }
    }

    /// <summary>
    /// Gets whether hydration should occur on initialization.
    /// </summary>
    public bool HydrateOnInit => _options?.HydrateOnInit ?? true;

    /// <summary>
    /// Synchronously loads the persisted state if the provider supports sync operations
    /// (i.e., WebAssembly mode via Microsoft.JSInterop.IJSInProcessRuntime).
    /// </summary>
    /// <returns>
    /// A tuple of (supported, state). If <c>supported</c> is false, sync loading is not
    /// available on this provider and the caller should fall back to async hydration.
    /// If <c>supported</c> is true but <c>state</c> is default, no persisted state was found
    /// or it failed validation/integrity checks.
    /// </returns>
    public (bool Supported, TState? State) TryLoadStateSync()
    {
        if (_provider is not LocalStorageProvider lsp || !lsp.SupportsSyncOperations)
        {
            return (false, default);
        }

        try
        {
            var json = lsp.LoadSync(_key);
            if (string.IsNullOrEmpty(json))
            {
                _options?.OnHydrationSkipped?.Invoke();
                return (true, default);
            }

            var loaded = ParsePersistedJson(json);
            return (true, loaded);
        }
        catch (StateIntegrityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading persisted state synchronously from key: {Key}", _key);
            // Surface to the browser console as well, since Debug.WriteLine is silent in Release WASM.
            Console.Error.WriteLine(
                $"[EasyAppDev.Store] Sync hydration failed for {typeof(TState).Name} (key '{_key}'): {ex.GetType().Name}: {ex.Message}");
            _options?.OnHydrationFailure?.Invoke(ex);
            return (true, default);
        }
    }

    /// <summary>
    /// Shared JSON parsing logic for both sync and async hydration paths.
    /// Handles wrapper unwrapping, signature verification, validation, and transformation.
    /// </summary>
    private TState? ParsePersistedJson(string json)
    {
        string stateJson;
        PersistedStateWrapper? wrapper = null;

        try
        {
            wrapper = JsonSerializer.Deserialize<PersistedStateWrapper>(json, _jsonOptions);
        }
        catch
        {
            // Ignore - will fall back to legacy plain-JSON format below.
        }

        if (wrapper != null && !string.IsNullOrEmpty(wrapper.State))
        {
            stateJson = wrapper.State;

            if (_messageSigner != null && !string.IsNullOrEmpty(wrapper.Signature))
            {
                if (!_messageSigner.Verify(stateJson, wrapper.Signature))
                {
                    var integrityEx = new StateIntegrityException(
                        $"State integrity verification failed for key '{_key}'. The persisted state may have been tampered with.");
                    _logger?.LogError(integrityEx, "Integrity check failed for key: {Key}", _key);
                    _options?.OnHydrationFailure?.Invoke(integrityEx);
                    return default;
                }
            }
            else if (_messageSigner != null && string.IsNullOrEmpty(wrapper.Signature))
            {
                _logger?.LogWarning(
                    "State loaded without signature for key: {Key}. RequireSignature: {Required}",
                    _key,
                    _options?.RequireSignature ?? false);

                if (_options?.RequireSignature == true)
                {
                    var integrityEx = new StateIntegrityException(
                        $"Unsigned state rejected for key '{_key}'. Integrity signing is required but no signature found.");
                    _logger?.LogError(integrityEx, "Unsigned state rejected for key: {Key}", _key);
                    _options.OnHydrationFailure?.Invoke(integrityEx);
                    return default;
                }
            }
        }
        else
        {
            // Legacy plain-JSON format (pre-wrapper).
            stateJson = json;
        }

        var loadedState = JsonSerializer.Deserialize<TState>(stateJson, _jsonOptions);
        if (loadedState == null)
        {
            _options?.OnHydrationSkipped?.Invoke();
            return default;
        }

        if (_options?.StateValidator != null)
        {
            var validationResult = _options.StateValidator.Validate(loadedState);
            if (!validationResult.IsValid)
            {
                _logger?.LogWarning(
                    "State validation failed for key {Key}: {Errors}",
                    _key,
                    string.Join(", ", validationResult.Errors));
                _options.OnValidationFailed?.Invoke(validationResult with { Source = "Persistence" });

                if (_options.RejectInvalidState)
                {
                    _options.OnHydrationFailure?.Invoke(
                        new InvalidOperationException(
                            $"State validation failed: {string.Join(", ", validationResult.Errors)}"));
                    return default;
                }
            }
        }

        var transformedState = _options?.TransformOnLoad != null
            ? _options.TransformOnLoad(loadedState)
            : loadedState;

        _options?.OnHydrationSuccess?.Invoke(transformedState);
        return transformedState;
    }

    /// <summary>
    /// Disposes the middleware, cancelling any pending debounced save and
    /// releasing the message signer and debounce cancellation token source.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources held by this middleware.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        lock (_debounceLock)
        {
            try
            {
                _debounceCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed - nothing to cancel
            }

            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        _messageSigner?.Dispose();
    }
}
