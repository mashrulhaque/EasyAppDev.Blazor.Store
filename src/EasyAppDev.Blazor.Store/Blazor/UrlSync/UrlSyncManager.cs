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

    /// <summary>
    /// Marshals work onto the Blazor dispatcher (e.g. ComponentBase.InvokeAsync).
    /// NavigateTo must run on the dispatcher; debounce continuations run on the thread pool.
    /// </summary>
    private readonly Func<Func<Task>, Task> _invokeAsync;

    private IDisposable? _storeSubscription;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();
    private TState? _lastSyncedState;
    private bool _isApplyingUrlToState;
    private volatile bool _disposed;

    // Default actions to exclude from URL sync
    private static readonly HashSet<string> DefaultExcludedActions = new()
    {
        "@@URL_SYNC",           // Prevent circular updates
        "@@URL_SYNC/FROM_URL",
        "@@SYNC",               // ServerSync conflict-resolution updates
        "@@SYNC_FULL",          // ServerSync full-state updates
        "SERVER_SYNC",          // ServerSync updates (legacy names)
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
        ILogger? logger = null,
        Func<Func<Task>, Task>? invokeAsync = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _invokeAsync = invokeAsync ?? (work => work());
        _lastSyncedState = store.GetState();
    }

    /// <summary>
    /// Starts the URL sync manager by subscribing to store changes.
    /// Uses the action-aware subscription so excluded actions are filtered out.
    /// </summary>
    public void Start()
    {
        _storeSubscription = _store.Subscribe((TState state, string? action) =>
        {
            // Layer 1: action filtering (excluded/system actions never sync to the URL)
            if (!ShouldSyncToUrl(action))
            {
                _logger?.LogTrace("Skipping URL sync - action {Action} is excluded", action);
                return;
            }

            OnStateChanged(state);
        });

        _logger?.LogDebug("UrlSyncManager started for {StateType}", typeof(TState).Name);
    }

    /// <summary>
    /// Syncs component parameters to state (URL → State direction).
    /// Called from OnParametersSetAsync in the component.
    /// Only mappings whose query parameter is actually present in the current URL
    /// are applied, so absent query params never clobber existing state.
    /// </summary>
    public async Task SyncFromComponentParametersAsync()
    {
        if (_disposed) return;

        try
        {
            _isApplyingUrlToState = true;

            var currentState = _store.GetState();
            var presentQueryParams = GetPresentQueryParamNames();
            var newState = _config.ApplyComponentParamsToState(currentState, presentQueryParams, _logger);

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
    /// Parses the current URI and returns the set of query parameter names present in it.
    /// </summary>
    private HashSet<string> GetPresentQueryParamNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var uri = new Uri(_navigationManager.Uri);
            var query = HttpUtility.ParseQueryString(uri.Query);

            foreach (var key in query.AllKeys)
            {
                if (key != null)
                {
                    result.Add(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse query string from current URI");
        }

        return result;
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
        CancellationToken token;

        lock (_debounceLock)
        {
            if (_disposed) return;

            // Cancel and dispose the old CTS to coalesce pending updates (last write wins).
            var oldCts = _debounceCts;
            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;

            oldCts?.Cancel();
            oldCts?.Dispose();
        }

        _ = RunDebouncedSyncAsync(newState, token);
    }

    /// <summary>
    /// Waits out the debounce window and then performs the URL sync on the
    /// Blazor dispatcher (NavigateTo must not run on a thread-pool thread).
    /// </summary>
    private async Task RunDebouncedSyncAsync(TState newState, CancellationToken token)
    {
        try
        {
            var debounceMs = (int)_config.Debounce.TotalMilliseconds;
            await Task.Delay(debounceMs, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // Superseded by a newer update or disposed
        }

        if (token.IsCancellationRequested || _disposed) return;

        try
        {
            await _invokeAsync(() =>
            {
                SyncToUrl(newState);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error syncing state to URL");
            _config.OnError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Performs the actual navigation to update URL.
    /// Rebuilds from the full current URI so unrelated query parameters and the
    /// fragment survive, and emits null values so stale parameters are removed.
    /// </summary>
    private void SyncToUrl(TState newState)
    {
        if (_disposed) return;

        try
        {
            // Build query parameters from state. Null values remove the parameter,
            // so stale query params are cleaned up (an all-null dictionary must
            // still navigate to clear them).
            var queryParams = _config.BuildUrlQueryParams(newState);

            // Rebuild from the FULL current URI so unrelated query params (utm_*, etc.)
            // are preserved. GetUriWithQueryParameters merges with the existing query.
            var currentUri = _navigationManager.Uri;

            // GetUriWithQueryParameters drops the fragment - preserve it manually.
            var fragment = string.Empty;
            var fragmentIndex = currentUri.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                fragment = currentUri[fragmentIndex..];
                currentUri = currentUri[..fragmentIndex];
            }

            var newUrl = _navigationManager.GetUriWithQueryParameters(currentUri, queryParams) + fragment;

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
        // ("SYNC_" matches TabSyncMiddleware which dispatches "SYNC_{action}")
        if (action.StartsWith("SERVER_", StringComparison.Ordinal) ||
            action.StartsWith("SYNC_", StringComparison.Ordinal) ||
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

        _logger?.LogDebug("UrlSyncManager disposed for {StateType}", typeof(TState).Name);
    }
}
