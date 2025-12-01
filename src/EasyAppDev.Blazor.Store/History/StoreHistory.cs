using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Provides undo/redo functionality by tracking state changes.
/// Also acts as middleware to intercept state updates.
/// </summary>
/// <typeparam name="TState">The type of state being tracked.</typeparam>
public sealed class StoreHistory<TState> : IStoreHistory<TState>, IMiddleware<TState>
    where TState : notnull
{
    private readonly List<HistoryEntry<TState>> _history = new();
    private readonly HistoryOptions _options;
    private readonly object _lock = new();
    private IStore<TState>? _store;
    private int _currentIndex = -1;
    private bool _isUndoRedo;
    private DateTime _lastEntryTime = DateTime.MinValue;

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
    /// </summary>
    /// <param name="store">The store to track.</param>
    internal void Initialize(IStore<TState> store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        // Add initial state
        lock (_lock)
        {
            var initialState = store.GetState();
            _history.Add(new HistoryEntry<TState>(initialState, "INITIAL", DateTime.UtcNow));
            _currentIndex = 0;
            _lastEntryTime = DateTime.UtcNow;
        }
    }

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

        TState? targetState = default;

        lock (_lock)
        {
            if (!CanUndo) return;

            _currentIndex--;
            targetState = _history[_currentIndex].State;
        }

        if (targetState != null)
        {
            _isUndoRedo = true;
            try
            {
                await _store.UpdateAsync(_ => targetState, "UNDO").ConfigureAwait(false);
            }
            finally
            {
                _isUndoRedo = false;
            }

            OnHistoryChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public async Task RedoAsync()
    {
        if (_store == null)
            throw new InvalidOperationException("History not initialized. Add to store first.");

        TState? targetState = default;

        lock (_lock)
        {
            if (!CanRedo) return;

            _currentIndex++;
            targetState = _history[_currentIndex].State;
        }

        if (targetState != null)
        {
            _isUndoRedo = true;
            try
            {
                await _store.UpdateAsync(_ => targetState, "REDO").ConfigureAwait(false);
            }
            finally
            {
                _isUndoRedo = false;
            }

            OnHistoryChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public async Task GoToAsync(int index)
    {
        if (_store == null)
            throw new InvalidOperationException("History not initialized. Add to store first.");

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
            _isUndoRedo = true;
            try
            {
                await _store.UpdateAsync(_ => targetState, $"GOTO_{index}").ConfigureAwait(false);
            }
            finally
            {
                _isUndoRedo = false;
            }

            OnHistoryChanged?.Invoke();
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
            _history.Add(new HistoryEntry<TState>(currentState, "CLEAR", DateTime.UtcNow));
            _currentIndex = 0;
            _lastEntryTime = DateTime.UtcNow;
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
        // Don't track undo/redo operations
        if (_isUndoRedo) return Task.CompletedTask;

        // Check if action should be tracked
        if (!_options.ShouldTrackAction(action)) return Task.CompletedTask;

        // Check if state actually changed
        if (EqualityComparer<TState>.Default.Equals(previousState, currentState))
            return Task.CompletedTask;

        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Check if we should group with the previous entry
            if (_options.GroupWindow > TimeSpan.Zero &&
                _currentIndex >= 0 &&
                (now - _lastEntryTime) < _options.GroupWindow)
            {
                // Replace the current entry instead of adding a new one
                _history[_currentIndex] = new HistoryEntry<TState>(currentState, action, now);
                _lastEntryTime = now;
                OnHistoryChanged?.Invoke();
                return Task.CompletedTask;
            }

            // Truncate forward history if we're not at the end
            if (_currentIndex < _history.Count - 1)
            {
                _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
            }

            // Add new entry
            _history.Add(new HistoryEntry<TState>(currentState, action, now));
            _currentIndex = _history.Count - 1;
            _lastEntryTime = now;

            // Enforce max size
            while (_history.Count > _options.MaxSize)
            {
                _history.RemoveAt(0);
                _currentIndex--;
            }
        }

        OnHistoryChanged?.Invoke();
        return Task.CompletedTask;
    }
}
