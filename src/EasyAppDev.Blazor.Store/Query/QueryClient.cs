// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Default implementation of the query client for managing query caching and invalidation.
/// </summary>
public sealed class QueryClient : IQueryClient, IDisposable, IAsyncDisposable
{
    private const string JsModulePath = "./_content/EasyAppDev.Blazor.Store/query-events.js";

    // JS window-event initialization states (see _jsInitState).
    private const int JsInitNotStarted = 0;
    private const int JsInitInProgress = 1;
    private const int JsInitCompleted = 2;
    private const int JsInitFailed = 3;

    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly ConcurrentDictionary<string, List<QueryRegistration>> _queryRefetchers = new();
    private readonly ConcurrentDictionary<string, bool> _invalidatedKeys = new();
    private readonly QueryClientOptions _options;
    private readonly ILogger<QueryClient>? _logger;
    private readonly Timer _cleanupTimer;
    private readonly IJSRuntime? _jsRuntime;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<QueryClient>? _dotNetRef;
    private int _jsListenerId;
    private int _jsInitState = JsInitNotStarted;
    private int _disposed; // 0 = not disposed, 1 = disposed (use int for Interlocked)

    /// <summary>
    /// Creates a new query client with the specified options.
    /// </summary>
    /// <param name="options">Optional client-level default options.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="jsRuntime">Optional JS runtime. When provided, browser window
    /// events (focus/online) trigger refetching of stale queries that have
    /// RefetchOnWindowFocus / RefetchOnReconnect enabled. When null (e.g. unit
    /// tests or non-browser hosts), the feature is simply inactive.</param>
    public QueryClient(
        IOptions<QueryClientOptions>? options = null,
        ILogger<QueryClient>? logger = null,
        IJSRuntime? jsRuntime = null)
    {
        _options = options?.Value ?? new QueryClientOptions();
        _logger = logger;
        _jsRuntime = jsRuntime;

        _cleanupTimer = new Timer(
            CleanupExpiredEntries,
            null,
            _options.CacheCleanupInterval,
            _options.CacheCleanupInterval);
    }

    /// <summary>
    /// Gets the default options for this client.
    /// </summary>
    public QueryClientOptions Options => _options;

    QueryClientOptions? IQueryClient.Options => _options;

    /// <inheritdoc />
    public T? GetQueryData<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_cache.TryGetValue(key, out var entry) && entry is QueryCacheEntry<T> typedEntry)
        {
            if (DateTime.UtcNow < typedEntry.ExpiresAt)
            {
                return typedEntry.Data;
            }

            // Expired - conditionally remove only the exact expired entry instance,
            // so a concurrent SetQueryData write is never deleted.
            _cache.TryRemove(new KeyValuePair<string, object>(key, entry));
        }

        return default;
    }

    /// <inheritdoc />
    public void SetQueryData<T>(string key, T data, TimeSpan? cacheTime = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        var duration = cacheTime ?? _options.DefaultCacheTime;
        var entry = new QueryCacheEntry<T>
        {
            Data = data,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + duration
        };

        _cache[key] = entry;
        _invalidatedKeys.TryRemove(key, out _);

        _logger?.LogDebug("Query cache updated for key {Key}", key);
    }

    /// <inheritdoc />
    public void SetQueryData<T>(string key, Func<T?, T> updater)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(updater);

        var existing = GetQueryData<T>(key);
        var updated = updater(existing);
        SetQueryData(key, updated);
    }

    /// <inheritdoc />
    public void InvalidateQueries(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _invalidatedKeys[key] = true;

        // Use a tracked task for better reliability - fire and forget but logged
        _ = InvalidateQueriesInternalAsync(key);

        _logger?.LogDebug("Query invalidated for key {Key}", key);
    }

    /// <summary>
    /// Invalidates queries for the specified key and awaits the refetch operation.
    /// Use this when you need to ensure the refetch completes before continuing.
    /// </summary>
    /// <param name="key">The query key to invalidate.</param>
    public async Task InvalidateQueriesAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _invalidatedKeys[key] = true;
        await TriggerRefetchAsync(key).ConfigureAwait(false);

        _logger?.LogDebug("Query invalidated and refetched for key {Key}", key);
    }

    private async Task InvalidateQueriesInternalAsync(string key)
    {
        try
        {
            await TriggerRefetchAsync(key).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error triggering refetch for invalidated query {Key}", key);
        }
    }

    /// <inheritdoc />
    public void InvalidateQueries(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        // Include active-but-uncached queries (registered refetchers) as well as
        // cached keys, deduplicated.
        var keys = _cache.Keys
            .Concat(_queryRefetchers.Keys)
            .Distinct()
            .Where(predicate)
            .ToList();

        foreach (var key in keys)
        {
            InvalidateQueries(key);
        }
    }

    /// <inheritdoc />
    public async Task RefetchQueriesAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        await TriggerRefetchAsync(key);
    }

    /// <inheritdoc />
    public void RemoveQueries(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _cache.TryRemove(key, out _);
        _invalidatedKeys.TryRemove(key, out _);

        _logger?.LogDebug("Query removed for key {Key}", key);
    }

    /// <inheritdoc />
    public void RemoveQueries(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (var key in _cache.Keys.Where(predicate).ToList())
        {
            RemoveQueries(key);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _cache.Clear();
        _invalidatedKeys.Clear();

        _logger?.LogDebug("Query cache cleared");
    }

    QueryCacheEntry<T>? IQueryClient.GetCacheEntry<T>(string key)
    {
        if (_cache.TryGetValue(key, out var entry) && entry is QueryCacheEntry<T> typedEntry)
        {
            if (DateTime.UtcNow < typedEntry.ExpiresAt)
            {
                return typedEntry;
            }
        }

        return null;
    }

    void IQueryClient.RegisterQuery(
        string key,
        Func<Task> refetch,
        Func<bool>? shouldRefetchOnFocus,
        Func<bool>? shouldRefetchOnReconnect)
    {
        var registration = new QueryRegistration(refetch, shouldRefetchOnFocus, shouldRefetchOnReconnect);

        while (true)
        {
            var list = _queryRefetchers.GetOrAdd(key, _ => new List<QueryRegistration>());
            lock (list)
            {
                // The list may have been removed from the dictionary (when it became
                // empty) between GetOrAdd and taking the lock - retry if so.
                if (_queryRefetchers.TryGetValue(key, out var current) && ReferenceEquals(current, list))
                {
                    list.Add(registration);
                    break;
                }
            }
        }

        // Lazily wire up browser window events (focus/online) once any query is active.
        TryStartJsInitialization();
    }

    void IQueryClient.UnregisterQuery(string key, Func<Task> refetch)
    {
        if (_queryRefetchers.TryGetValue(key, out var list))
        {
            lock (list)
            {
                // Identity-based removal: the refetch delegate instance identifies
                // the registration (same scheme as before registrations carried
                // window-event predicates).
                var index = list.FindIndex(r => ReferenceEquals(r.Refetch, refetch));
                if (index >= 0)
                {
                    list.RemoveAt(index);
                }

                // Remove the dictionary entry when the per-key list becomes empty
                // to avoid unbounded growth. Conditional removal (key + exact list
                // instance) under the list lock keeps RegisterQuery race-free.
                if (list.Count == 0)
                {
                    _queryRefetchers.TryRemove(new KeyValuePair<string, List<QueryRegistration>>(key, list));
                }
            }
        }
    }

    void IQueryClient.MarkInvalidated(string key)
    {
        _invalidatedKeys[key] = true;
    }

    bool IQueryClient.IsInvalidated(string key)
    {
        return _invalidatedKeys.TryGetValue(key, out var invalidated) && invalidated;
    }

    private async Task TriggerRefetchAsync(string key)
    {
        if (!_queryRefetchers.TryGetValue(key, out var refetchers))
            return;

        List<QueryRegistration> copy;
        lock (refetchers)
        {
            copy = refetchers.ToList();
        }

        foreach (var registration in copy)
        {
            try
            {
                await registration.Refetch();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during query refetch for key {Key}", key);
            }
        }
    }

    /// <summary>
    /// Invoked from JavaScript when the browser window regains focus.
    /// Refetches every registered query whose effective RefetchOnWindowFocus
    /// setting is enabled and whose data is stale.
    /// </summary>
    [JSInvokable]
    public async Task OnWindowFocusAsync()
    {
        await RefetchOnWindowEventAsync(static r => r.ShouldRefetchOnFocus, "window focus").ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked from JavaScript when the network reconnects (window 'online' event).
    /// Refetches every registered query whose effective RefetchOnReconnect
    /// setting is enabled and whose data is stale.
    /// </summary>
    [JSInvokable]
    public async Task OnReconnectAsync()
    {
        await RefetchOnWindowEventAsync(static r => r.ShouldRefetchOnReconnect, "reconnect").ConfigureAwait(false);
    }

    private async Task RefetchOnWindowEventAsync(
        Func<QueryRegistration, Func<bool>?> predicateSelector,
        string eventName)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        // Snapshot keys and per-key registrations so concurrent
        // register/unregister can never invalidate the iteration.
        foreach (var kvp in _queryRefetchers.ToArray())
        {
            List<QueryRegistration> copy;
            lock (kvp.Value)
            {
                copy = kvp.Value.ToList();
            }

            foreach (var registration in copy)
            {
                try
                {
                    var shouldRefetch = predicateSelector(registration);
                    if (shouldRefetch is not null && shouldRefetch())
                    {
                        await registration.Refetch().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex, "Error during {Event} refetch for key {Key}", eventName, kvp.Key);
                }
            }
        }
    }

    private void TryStartJsInitialization()
    {
        if (_jsRuntime is null || Volatile.Read(ref _disposed) != 0)
            return;

        // Only one initialization attempt at a time. A prerender failure resets
        // the state to NotStarted so a later registration retries; a hard JS
        // failure latches (JsInitFailed) and disables the feature for this client.
        if (Interlocked.CompareExchange(ref _jsInitState, JsInitInProgress, JsInitNotStarted) != JsInitNotStarted)
            return;

        _ = InitializeJsEventsAsync();
    }

    private async Task InitializeJsEventsAsync()
    {
        IJSObjectReference? module = null;
        DotNetObjectReference<QueryClient>? dotNetRef = null;
        try
        {
            module = await _jsRuntime!.InvokeAsync<IJSObjectReference>(
                "import", JsModulePath).ConfigureAwait(false);
            dotNetRef = DotNetObjectReference.Create(this);
            var listenerId = await module.InvokeAsync<int>("init", dotNetRef).ConfigureAwait(false);

            _jsListenerId = listenerId;
            _jsModule = module;
            _dotNetRef = dotNetRef;
            Volatile.Write(ref _jsInitState, JsInitCompleted);

            // The client may have been disposed while the interop calls were in
            // flight - release the JS resources we just acquired.
            if (Volatile.Read(ref _disposed) != 0)
            {
                await DisposeJsResourcesAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop is not available yet (e.g. server-side prerendering).
            // Do NOT latch - reset so a later RegisterQuery retries.
            CleanupFailedJsInit(module, dotNetRef);
            Volatile.Write(ref _jsInitState, JsInitNotStarted);
        }
        catch (Exception ex)
        {
            // JSException, JSDisconnectedException, serialization errors, ... -
            // latch so we don't retry forever in an environment without the module.
            CleanupFailedJsInit(module, dotNetRef);
            Volatile.Write(ref _jsInitState, JsInitFailed);
            _logger?.LogDebug(
                ex, "Failed to initialize window focus/reconnect refetching; feature disabled for this query client");
        }
    }

    private static void CleanupFailedJsInit(
        IJSObjectReference? module,
        DotNetObjectReference<QueryClient>? dotNetRef)
    {
        try
        {
            dotNetRef?.Dispose();
        }
        catch
        {
            // Best-effort cleanup only
        }

        if (module is not null)
        {
            try
            {
                _ = module.DisposeAsync();
            }
            catch
            {
                // Best-effort cleanup only (circuit may already be gone)
            }
        }
    }

    private async Task DisposeJsResourcesAsync()
    {
        var dotNetRef = Interlocked.Exchange(ref _dotNetRef, null);
        var module = Interlocked.Exchange(ref _jsModule, null);

        try
        {
            dotNetRef?.Dispose();
        }
        catch
        {
            // Best-effort cleanup only
        }

        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", _jsListenerId).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the circuit/page may already be gone
            }

            try
            {
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: the circuit/page may already be gone
            }
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        var now = DateTime.UtcNow;
        var expiredEntries = new List<KeyValuePair<string, object>>();

        // Create a snapshot of the cache to avoid concurrent modification issues
        var cacheSnapshot = _cache.ToArray();

        foreach (var kvp in cacheSnapshot)
        {
            try
            {
                // Use interface for type-safe expiration check without reflection
                if (kvp.Value is IQueryCacheEntry entry && now >= entry.ExpiresAt)
                {
                    expiredEntries.Add(kvp);
                }
            }
            catch (Exception ex)
            {
                // Entry may have been modified/removed during iteration
                _logger?.LogDebug(ex, "Error checking cache entry {Key} during cleanup", kvp.Key);
            }
        }

        foreach (var kvp in expiredEntries)
        {
            // Conditionally remove only the exact expired entry instance so freshly
            // written data for the same key is never deleted.
            if (_cache.TryRemove(kvp))
            {
                _logger?.LogDebug("Expired cache entry removed for key {Key}", kvp.Key);
            }
        }

        // Clean up invalidation flags for keys that have neither a cache entry nor
        // any registered (active) query, to prevent unbounded growth.
        foreach (var key in _invalidatedKeys.Keys)
        {
            if (!_cache.ContainsKey(key) && !_queryRefetchers.ContainsKey(key))
            {
                _invalidatedKeys.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Disposes the query client. JS window-event resources (if any) are
    /// released best-effort in a fire-and-forget manner; prefer
    /// <see cref="DisposeAsync"/> when an async context is available.
    /// </summary>
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cleanupTimer.Dispose();

        // Best-effort fire-and-forget; all exceptions are contained inside.
        _ = DisposeJsResourcesAsync();

        _cache.Clear();
        _queryRefetchers.Clear();
        _invalidatedKeys.Clear();
    }

    /// <summary>
    /// Disposes the query client and asynchronously releases the JS
    /// window-event resources (focus/online listeners), if initialized.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cleanupTimer.Dispose();

        await DisposeJsResourcesAsync().ConfigureAwait(false);

        _cache.Clear();
        _queryRefetchers.Clear();
        _invalidatedKeys.Clear();
    }

    /// <summary>
    /// A registered active query: its refetch delegate (which also serves as the
    /// registration identity for unregistration) plus optional predicates that
    /// decide participation in window-focus / reconnect refetching.
    /// </summary>
    internal sealed record QueryRegistration(
        Func<Task> Refetch,
        Func<bool>? ShouldRefetchOnFocus,
        Func<bool>? ShouldRefetchOnReconnect);
}
