using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Middleware that automatically persists state changes to storage.
/// </summary>
/// <typeparam name="TState">The type of state to persist.</typeparam>
public class PersistenceMiddleware<TState> : IMiddleware<TState>
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

    private async Task DebouncedSaveAsync(TState state)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(_debounceMs, _debounceCts.Token).ConfigureAwait(false);
            await SaveStateAsync(state).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled, next update will trigger save
        }
    }

    private async Task SaveStateAsync(TState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await _provider.SaveAsync(_key, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error persisting state to key: {Key}", _key);
        }
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

            var loadedState = JsonSerializer.Deserialize<TState>(json, _jsonOptions);
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
}
