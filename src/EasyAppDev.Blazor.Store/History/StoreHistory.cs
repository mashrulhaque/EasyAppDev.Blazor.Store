using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Provides undo/redo functionality by tracking state changes.
/// Also acts as middleware to intercept state updates.
/// </summary>
/// <typeparam name="TState">The type of state being tracked.</typeparam>
public sealed class StoreHistory<TState> : IStoreHistory<TState>, IMiddleware<TState>, IStoreAwareMiddleware<TState>
    where TState : notnull
{
    private readonly List<HistoryEntry<TState>> _history = new();
    private readonly HistoryOptions _options;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _undoRedoLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private IStore<TState>? _store;
    private int _currentIndex = -1;
    // Use AsyncLocal to properly scope undo/redo flag to current execution context.
    // This prevents race conditions where concurrent updates could see the flag incorrectly.
    // Must be an INSTANCE field: a static would be shared across all histories of the same
    // TState, so an undo on one store would suppress tracking on unrelated stores.
    private readonly AsyncLocal<bool> _isUndoRedo = new();
    private bool _isInitialized;
    private DateTime _lastEntryTime = DateTime.MinValue;
    private long _estimatedMemoryUsage = 0;

    /// <summary>
    /// Creates a new store history tracker.
    /// </summary>
    /// <param name="options">Configuration options for history tracking.</param>
    public StoreHistory(HistoryOptions? options = null)
    {
        _options = options ?? new HistoryOptions();
    }

    /// <summary>
    /// Initializes the history with the store reference.
    /// Called automatically when the middleware is added to the store.
    /// This method is idempotent - calling it multiple times has no effect after first initialization.
    /// </summary>
    /// <param name="store">The store to track.</param>
    internal void Initialize(IStore<TState> store)
    {
        ArgumentNullException.ThrowIfNull(store);

        lock (_lock)
        {
            // Make initialization idempotent - skip if already initialized
            // (AddStoreWithHistory paths call Initialize explicitly in addition to
            // StoreBuilder.Build calling AttachStore).
            if (_store != null || _isInitialized)
                return;

            _store = store;

            // Add initial state
            var initialState = store.GetState();
            var entrySize = EstimateStateSize(initialState);
            _history.Add(new HistoryEntry<TState>(initialState, "INITIAL", DateTime.UtcNow, entrySize));
            _currentIndex = 0;
            _lastEntryTime = DateTime.UtcNow;
            _estimatedMemoryUsage = entrySize;
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Attaches the store this history belongs to. Called by <c>StoreBuilder.Build()</c>
    /// for middlewares implementing <see cref="IStoreAwareMiddleware{TState}"/>. Idempotent.
    /// </summary>
    /// <param name="store">The store to track.</param>
    public void AttachStore(IStore<TState> store) => Initialize(store);

    /// <inheritdoc />
    public bool CanUndo
    {
        get
        {
            lock (_lock)
            {
                return _currentIndex > 0;
            }
        }
    }

    /// <inheritdoc />
    public bool CanRedo
    {
        get
        {
            lock (_lock)
            {
                return _currentIndex < _history.Count - 1;
            }
        }
    }

    /// <inheritdoc />
    public int CurrentIndex
    {
        get
        {
            lock (_lock)
            {
                return _currentIndex;
            }
        }
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _history.Count;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HistoryEntry<TState>> Entries
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc />
    public event Action? OnHistoryChanged;

    /// <inheritdoc />
    public async Task UndoAsync()
    {
        if (_store == null)
            throw new InvalidOperationException("History not initialized. Add to store first.");

        await _undoRedoLock.WaitAsync().ConfigureAwait(false);
        try
        {
            TState? targetState = default;

            lock (_lock)
            {
                if (_currentIndex <= 0) return;

                _currentIndex--;
                targetState = _history[_currentIndex].State;
            }

            if (targetState != null)
            {
                _isUndoRedo.Value = true;
                try
                {
                    await _store.UpdateAsync(_ => targetState, "UNDO").ConfigureAwait(false);
                }
                finally
                {
                    _isUndoRedo.Value = false;
                }

                OnHistoryChanged?.Invoke();
            }
        }
        finally
        {
            _undoRedoLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RedoAsync()
    {
        if (_store == null)
            throw new InvalidOperationException("History not initialized. Add to store first.");

        await _undoRedoLock.WaitAsync().ConfigureAwait(false);
        try
        {
            TState? targetState = default;

            lock (_lock)
            {
                if (_currentIndex >= _history.Count - 1) return;

                _currentIndex++;
                targetState = _history[_currentIndex].State;
            }

            if (targetState != null)
            {
                _isUndoRedo.Value = true;
                try
                {
                    await _store.UpdateAsync(_ => targetState, "REDO").ConfigureAwait(false);
                }
                finally
                {
                    _isUndoRedo.Value = false;
                }

                OnHistoryChanged?.Invoke();
            }
        }
        finally
        {
            _undoRedoLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task GoToAsync(int index)
    {
        if (_store == null)
            throw new InvalidOperationException("History not initialized. Add to store first.");

        await _undoRedoLock.WaitAsync().ConfigureAwait(false);
        try
        {
            TState? targetState = default;

            lock (_lock)
            {
                if (index < 0 || index >= _history.Count)
                    throw new ArgumentOutOfRangeException(nameof(index),
                        $"Index must be between 0 and {_history.Count - 1}");

                if (index == _currentIndex) return;

                _currentIndex = index;
                targetState = _history[_currentIndex].State;
            }

            if (targetState != null)
            {
                _isUndoRedo.Value = true;
                try
                {
                    await _store.UpdateAsync(_ => targetState, $"GOTO_{index}").ConfigureAwait(false);
                }
                finally
                {
                    _isUndoRedo.Value = false;
                }

                OnHistoryChanged?.Invoke();
            }
        }
        finally
        {
            _undoRedoLock.Release();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            if (_history.Count == 0) return;

            // Keep only the current state
            var currentState = _history[_currentIndex].State;
            _history.Clear();

            // Reset memory accounting and add entry with proper size tracking
            var entrySize = EstimateStateSize(currentState);
            _history.Add(new HistoryEntry<TState>(currentState, "CLEAR", DateTime.UtcNow, entrySize));
            _currentIndex = 0;
            _lastEntryTime = DateTime.UtcNow;
            _estimatedMemoryUsage = entrySize; // Reset to just the current entry size
        }

        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    /// Called before a state update is applied.
    /// </summary>
    Task IMiddleware<TState>.OnBeforeUpdateAsync(TState currentState, string? action)
    {
        // Nothing to do before update
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after a state update has been applied.
    /// </summary>
    Task IMiddleware<TState>.OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        // Don't track undo/redo operations (AsyncLocal ensures this check is scoped to current execution context)
        if (_isUndoRedo.Value) return Task.CompletedTask;

        // Check if action should be tracked
        if (!_options.ShouldTrackAction(action)) return Task.CompletedTask;

        // Check if state actually changed
        if (EqualityComparer<TState>.Default.Equals(previousState, currentState))
            return Task.CompletedTask;

        var now = DateTime.UtcNow;
        bool shouldNotify;

        lock (_lock)
        {
            // Estimate size INSIDE the lock to ensure consistent state
            var newEntrySize = EstimateStateSize(currentState);

            // Check if we should group with the previous entry. Grouping is only valid when
            // we are at the END of the history (after an Undo/GoTo, replacing the current
            // entry would corrupt the timeline) and never into index 0 (the INITIAL entry).
            if (_options.GroupWindow > TimeSpan.Zero &&
                _currentIndex == _history.Count - 1 &&
                _currentIndex > 0 &&
                (now - _lastEntryTime) < _options.GroupWindow)
            {
                // Use cached size from entry if available, otherwise estimate
                var oldEntry = _history[_currentIndex];
                var oldEntrySize = oldEntry.EstimatedSize > 0 ? oldEntry.EstimatedSize : EstimateStateSize(oldEntry.State);
                _estimatedMemoryUsage = Math.Max(0, _estimatedMemoryUsage - oldEntrySize) + newEntrySize;

                // Replace the current entry instead of adding a new one
                _history[_currentIndex] = new HistoryEntry<TState>(currentState, action, now, newEntrySize);
                _lastEntryTime = now;
                shouldNotify = true;
            }
            else
            {
                // Truncate forward history if we're not at the end
                if (_currentIndex < _history.Count - 1)
                {
                    // Update memory estimate for removed entries using cached sizes
                    for (var i = _currentIndex + 1; i < _history.Count; i++)
                    {
                        var entry = _history[i];
                        var entrySize = entry.EstimatedSize > 0 ? entry.EstimatedSize : EstimateStateSize(entry.State);
                        _estimatedMemoryUsage = Math.Max(0, _estimatedMemoryUsage - entrySize);
                    }
                    _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
                }

                // Add new entry with cached size
                _history.Add(new HistoryEntry<TState>(currentState, action, now, newEntrySize));
                _currentIndex = _history.Count - 1;
                _lastEntryTime = now;
                _estimatedMemoryUsage += newEntrySize;

                // Enforce max count
                while (_history.Count > _options.MaxSize)
                {
                    var entry = _history[0];
                    var entrySize = entry.EstimatedSize > 0 ? entry.EstimatedSize : EstimateStateSize(entry.State);
                    _estimatedMemoryUsage = Math.Max(0, _estimatedMemoryUsage - entrySize);
                    _history.RemoveAt(0);
                    _currentIndex = Math.Max(0, _currentIndex - 1);
                }

                // Enforce max memory (if configured)
                if (_options.MaxMemoryBytes > 0)
                {
                    while (_history.Count > 1 && _estimatedMemoryUsage > _options.MaxMemoryBytes)
                    {
                        var entry = _history[0];
                        var entrySize = entry.EstimatedSize > 0 ? entry.EstimatedSize : EstimateStateSize(entry.State);
                        _estimatedMemoryUsage = Math.Max(0, _estimatedMemoryUsage - entrySize);
                        _history.RemoveAt(0);
                        _currentIndex = Math.Max(0, _currentIndex - 1);
                    }
                }

                shouldNotify = true;
            }
        }

        // Always invoke OnHistoryChanged OUTSIDE the lock for consistency
        if (shouldNotify)
        {
            OnHistoryChanged?.Invoke();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the estimated memory usage of the history in bytes.
    /// </summary>
    public long EstimatedMemoryUsage
    {
        get
        {
            lock (_lock)
            {
                return _estimatedMemoryUsage;
            }
        }
    }

    private long EstimateStateSize(TState state)
    {
        if (_options.StateSizeEstimator != null)
        {
            return _options.StateSizeEstimator(state);
        }

        // Default: use JSON serialization size as estimate
        try
        {
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            return json.Length * 2; // UTF-16 chars = 2 bytes each
        }
        catch
        {
            // Fallback: rough estimate based on type
            return 1024; // 1KB default
        }
    }
}
