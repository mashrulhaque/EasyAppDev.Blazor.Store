using EasyAppDev.Blazor.Store.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Web;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Manages bidirectional synchronization between URL parameters and store state.
/// Handles circular update prevention, debouncing, and action filtering.
/// </summary>
internal sealed class UrlSyncManager<TState> : IDisposable where TState : notnull
{
    private readonly IStore<TState> _store;
    private readonly NavigationManager _navigationManager;
    private readonly UrlSyncConfiguration<TState> _config;
    private readonly ILogger? _logger;

    private IDisposable? _storeSubscription;
    private CancellationTokenSource? _debounceCts;
    private TState? _lastSyncedState;
    private bool _isApplyingUrlToState;
    private bool _disposed;

    // Default actions to exclude from URL sync
    private static readonly HashSet<string> DefaultExcludedActions = new()
    {
        "@@URL_SYNC",           // Prevent circular updates
        "@@URL_SYNC/FROM_URL",
        "SERVER_SYNC",          // ServerSync updates
        "SERVER_UPDATE",
        "CURSOR_UPDATE",        // Collaborative editing
        "CURSOR_MOVE",
        "PRESENCE_UPDATE",      // User presence
        "PRESENCE_CHANGE",
        "TAB_SYNC",             // Just in case
        "HYDRATE"               // Persistence hydration
    };

    public UrlSyncManager(
        IStore<TState> store,
        NavigationManager navigationManager,
        UrlSyncConfiguration<TState> config,
        ILogger? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _lastSyncedState = store.GetState();
    }

    /// <summary>
    /// Starts the URL sync manager by subscribing to store changes.
    /// </summary>
    public void Start()
    {
        // Subscribe to store with action tracking (requires extended Subscribe overload)
        // For Phase 1, we'll use the standard Subscribe and infer action from context
        _storeSubscription = _store.Subscribe(OnStateChanged);

        _logger?.LogDebug("UrlSyncManager started for {StateType}", typeof(TState).Name);
    }

    /// <summary>
    /// Syncs component parameters to state (URL → State direction).
    /// Called from OnParametersSetAsync in the component.
    /// </summary>
    public async Task SyncFromComponentParametersAsync()
    {
        if (_disposed) return;

        try
        {
            _isApplyingUrlToState = true;

            var currentState = _store.GetState();
            var newState = _config.ApplyComponentParamsToState(currentState);

            // Only update if state actually changed
            if (!EqualityComparer<TState>.Default.Equals(currentState, newState))
            {
                _logger?.LogDebug("Syncing URL params to state for {StateType}", typeof(TState).Name);

                await _store.UpdateAsync(_ => newState, "@@URL_SYNC/FROM_URL");
                _lastSyncedState = newState;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error syncing URL params to state");
            _config.OnError?.Invoke(ex);
        }
        finally
        {
            _isApplyingUrlToState = false;
        }
    }

    /// <summary>
    /// Syncs state to URL (State → URL direction).
    /// Called when store state changes.
    /// </summary>
    private void OnStateChanged(TState newState)
    {
        if (_disposed) return;

        // Layer 2: Navigation flag (prevent circular update during URL → State)
        if (_isApplyingUrlToState)
        {
            _logger?.LogTrace("Skipping URL sync - applying URL to state");
            return;
        }

        // Layer 3: Value comparison (only sync if URL-relevant properties changed)
        if (_lastSyncedState != null && !_config.HasUrlRelevantChanges(_lastSyncedState, newState))
        {
            _logger?.LogTrace("Skipping URL sync - no URL-relevant changes");
            return;
        }

        // State changed, sync to URL with debouncing
        SyncToUrlDebounced(newState);
    }

    /// <summary>
    /// Schedules a debounced URL update.
    /// Cancels previous pending updates.
    /// </summary>
    private void SyncToUrlDebounced(TState newState)
    {
        // Cancel previous debounce
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        var cts = _debounceCts;
        var debounceMs = (int)_config.Debounce.TotalMilliseconds;

        _ = Task.Delay(debounceMs, cts.Token).ContinueWith(task =>
        {
            if (!cts.IsCancellationRequested && !_disposed)
            {
                try
                {
                    SyncToUrl(newState);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error syncing state to URL");
                    _config.OnError?.Invoke(ex);
                }
            }
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Performs the actual navigation to update URL.
    /// </summary>
    private void SyncToUrl(TState newState)
    {
        if (_disposed) return;

        try
        {
            // Build query parameters from state
            var queryParams = _config.BuildUrlQueryParams(newState);

            if (queryParams.Count == 0)
            {
                _logger?.LogTrace("No query params to sync");
                return;
            }

            // Build new URL
            var currentUri = new Uri(_navigationManager.Uri);
            var basePath = currentUri.GetLeftPart(UriPartial.Path);

            var newUrl = _navigationManager.GetUriWithQueryParameters(basePath, queryParams);

            // Check if URL actually changed
            if (string.Equals(_navigationManager.Uri, newUrl, StringComparison.Ordinal))
            {
                _logger?.LogTrace("Skipping navigation - URL unchanged");
                return;
            }

            // Warn if URL is getting too long
            if (newUrl.Length > 1800)
            {
                _logger?.LogWarning(
                    "URL length ({Length}) approaching browser limit (2000 chars). " +
                    "Consider using persistence instead of URL sync for large state.",
                    newUrl.Length);
            }

            // Navigate with configured mode
            var replace = _config.NavigationMode == UrlSyncNavigationMode.Replace;

            _logger?.LogDebug("Navigating to {Url} (replace={Replace})", newUrl, replace);

            _navigationManager.NavigateTo(newUrl, forceLoad: false, replace: replace);

            _lastSyncedState = newState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error navigating to new URL");
            _config.OnError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Determines if a state update action should trigger URL sync.
    /// Layer 1 of circular update prevention.
    /// </summary>
    private bool ShouldSyncToUrl(string? action)
    {
        if (string.IsNullOrEmpty(action))
            return true;  // User-initiated (no action name)

        // Check default exclusions
        if (DefaultExcludedActions.Contains(action))
            return false;

        // Check user-configured exclusions
        if (_config.ExcludedActions.Contains(action))
            return false;

        // Check prefix patterns
        if (action.StartsWith("SERVER_", StringComparison.Ordinal) ||
            action.StartsWith("CURSOR_", StringComparison.Ordinal) ||
            action.StartsWith("PRESENCE_", StringComparison.Ordinal) ||
            action.StartsWith("@@URL_SYNC", StringComparison.Ordinal))
            return false;

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _storeSubscription?.Dispose();
        _storeSubscription = null;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        _logger?.LogDebug("UrlSyncManager disposed for {StateType}", typeof(TState).Name);
    }
}
