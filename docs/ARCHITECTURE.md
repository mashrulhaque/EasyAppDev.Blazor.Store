# Architecture

> How EasyAppDev.Blazor.Store is built and why

## Overview

EasyAppDev.Blazor.Store is a Zustand-inspired state management library for Blazor. It prioritizes simplicity, type safety, and immutability while providing powerful features for complex applications.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Component Layer                          │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐  │
│  │  StoreComponent  │  │ SelectorComponent│  │   Custom     │  │
│  │     <TState>     │  │ <TState,TSelect> │  │  Components  │  │
│  └────────┬─────────┘  └────────┬─────────┘  └──────┬───────┘  │
└───────────┼─────────────────────┼───────────────────┼──────────┘
            │                     │                   │
            ▼                     ▼                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Store Layer                             │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                     IStore<TState>                        │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌──────────────────┐   │  │
│  │  │ StateReader │ │ StateWriter │ │ StateObservable  │   │  │
│  │  └─────────────┘ └─────────────┘ └──────────────────┘   │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Middleware Layer                           │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐  ┌────────────┐    │
│  │ DevTools │  │ Persist  │  │  Logging  │  │  Custom    │    │
│  └──────────┘  └──────────┘  └───────────┘  └────────────┘    │
└─────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────┐
│                       State Layer                               │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Immutable C# Records                         │  │
│  │   record State(int Count) {                               │  │
│  │       State Increment() => this with { Count = Count+1 }; │  │
│  │   }                                                       │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Concepts

### 1. State as Records

State is defined using C# records with transformation methods:

```csharp
public record CounterState(int Count, string? LastAction = null)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
    public CounterState Decrement() => this with { Count = Count - 1, LastAction = "DECREMENT" };
}
```

**Why Records:**
- Immutability by default
- Value equality semantics
- `with` expressions for updates
- Concise syntax
- Pattern matching support

### 2. Store

The store holds state and manages subscriptions:

```csharp
public interface IStore<TState> :
    IStateReader<TState>,      // GetState()
    IStateWriter<TState>,      // UpdateAsync()
    IStateObservable<TState>,  // Subscribe()
    IDisposable
    where TState : notnull
{
}
```

**Implementation Details:**
- Thread-safe via `SemaphoreSlim`
- Notifications outside lock (prevents deadlocks)
- Async-first design
- Configurable equality comparison

### 3. Subscriptions

Components subscribe to state changes:

```csharp
// Full state subscription
store.Subscribe(state => Console.WriteLine(state.Count));

// Selector subscription (granular)
store.Subscribe(
    selector: s => s.Count,
    callback: count => Console.WriteLine(count)
);
```

**Subscription Manager:**
- Thread-safe subscriber list
- Selector-based filtering
- Previous value tracking
- Automatic cleanup via IDisposable

### 4. Middleware

Middleware intercepts state updates:

```csharp
public interface IMiddleware<TState>
{
    Task OnBeforeUpdateAsync(TState currentState, string? action);
    Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action);
}
```

**Pipeline Execution:**
```
User Update
    │
    ▼
┌─────────────────────┐
│ OnBeforeUpdateAsync │ ← Middleware 1
│ OnBeforeUpdateAsync │ ← Middleware 2
│ OnBeforeUpdateAsync │ ← Middleware 3
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│   Apply Updater     │ ← State transformation
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│ OnAfterUpdateAsync  │ ← Middleware 1
│ OnAfterUpdateAsync  │ ← Middleware 2
│ OnAfterUpdateAsync  │ ← Middleware 3
└─────────────────────┘
    │
    ▼
Notify Subscribers
```

---

## Data Flow

### Update Flow

```
1. Component calls Update()
   │
   ▼
2. Store acquires lock
   │
   ▼
3. Middleware.OnBeforeUpdateAsync() (each middleware)
   │
   ▼
4. Updater function executed
   │
   newState = updater(currentState)
   │
   ▼
5. Equality check
   │
   ├── Equal → Skip notification
   │
   └── Not Equal → Continue
       │
       ▼
6. Middleware.OnAfterUpdateAsync() (each middleware)
   │
   ▼
7. Store releases lock
   │
   ▼
8. SubscriptionManager.NotifyAll()
   │
   ▼
9. Each subscriber callback invoked
   │
   ▼
10. Components call StateHasChanged()
```

### Component Lifecycle

```
Component Created
    │
    ▼
OnInitialized()
    │
    ├── Resolve services from DI
    │
    └── Subscribe to store
        │
        ▼
Component Rendered
    │
    ▼
User Interaction
    │
    └── Update(s => s.Transform())
        │
        ▼
Store Update
    │
    ▼
Subscriber Notified
    │
    └── InvokeAsync(StateHasChanged)
        │
        ▼
Component Re-rendered
    │
    ▼
Component Disposed
    │
    └── Subscription disposed
```

---

## Key Classes

### Store<TState>

```
Store<TState>
├── _state: TState                     // Current state
├── _lock: SemaphoreSlim              // Thread safety
├── _comparer: IEqualityComparer      // Change detection
├── _subscriptionManager              // Observer management
├── _middlewarePipeline               // Middleware chain
│
├── GetState()                        // Read current state
├── UpdateAsync(updater, action)      // Modify state
├── Subscribe(callback)               // Register observer
└── Dispose()                         // Cleanup
```

### StoreComponent<TState>

```
StoreComponent<TState> : ComponentBase
├── [Inject] Store                    // The store instance
├── [Inject] DebounceManager          // Debounce utility
├── [Inject] ThrottleManager          // Throttle utility
├── [Inject] LazyCache                // Cache utility
│
├── State                             // Current state property
├── Update(updater)                   // Update helper
├── UpdateAsync(asyncUpdater)         // Async update helper
├── SubscribeToSelector()             // Granular subscription
│
├── OnInitialized()                   // Subscribe to store
└── Dispose()                         // Unsubscribe
```

### StoreBuilder<TState>

```
StoreBuilder<TState>
├── _initialState: TState
├── _middlewares: List<IMiddleware>
├── _comparer: IEqualityComparer
│
├── Create(initialState)              // Factory method
├── WithMiddleware(middleware)        // Add middleware
├── WithLogging()                     // Add logging
├── WithDevTools(sp, name)            // Add DevTools
├── WithPersistence(sp, key)          // Add persistence
└── Build()                           // Create store
```

---

## Threading Model

### Thread Safety Guarantees

| Operation | Thread Safe | Notes |
|-----------|-------------|-------|
| GetState() | Yes | Returns immutable reference |
| UpdateAsync() | Yes | SemaphoreSlim protects updates |
| Subscribe() | Yes | Lock on subscriber list |
| NotifyAll() | Yes | Snapshot of subscribers taken |

### Lock Strategy

```csharp
public async Task UpdateAsync(Func<TState, TState> updater, string? action = null)
{
    await _lock.WaitAsync();  // Acquire lock
    try
    {
        // Middleware hooks (inside lock)
        // State update (inside lock)
    }
    finally
    {
        _lock.Release();  // Release lock
    }

    // Notify subscribers (OUTSIDE lock - prevents deadlocks)
    if (shouldNotify)
    {
        NotifySubscribers();
    }
}
```

**Why notify outside lock:**
- Subscriber callbacks may update other stores
- Cross-store updates could deadlock
- Component rendering should not block store

### Reentrancy Detection

```csharp
private readonly AsyncLocal<int> _updateDepth = new();

if (_updateDepth.Value > 1)
{
    _logger?.LogWarning("Reentrancy detected...");
}
```

`AsyncLocal<T>` tracks update depth across async boundaries.

---

## Dependency Injection

### Registration Patterns

**Singleton Store (default):**
```csharp
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store.WithDevTools(sp, "Counter")
);
```

**Scoped Store (per-connection in Blazor Server):**
```csharp
builder.Services.AddScopedStore(
    new UserState(),
    (store, sp) => store.WithDevTools(sp, "User")
);
```

**Service Resolution:**
```
IServiceProvider
├── IStore<CounterState>     (Singleton)
├── IStore<UserState>        (Scoped)
├── IDebounceManager         (Scoped)
├── IThrottleManager         (Scoped)
├── ILazyCache               (Scoped)
└── IAsyncActionExecutor<T>  (Scoped, per state type)
```

### Blazor Server Considerations

```
Blazor Server Circuit
├── SignalR Connection
│   ├── IJSRuntime (Scoped)
│   ├── NavigationManager (Scoped)
│   └── User-specific services
│
└── Store Registration
    ├── Singleton: Shared across all users
    │   └── Cannot use IJSRuntime directly
    │
    └── Scoped: Per-user
        └── Can use IJSRuntime via IServiceProvider
```

---

## Middleware Architecture

### Built-in Middleware

**DevToolsMiddleware:**
```
DevToolsMiddleware<TState>
├── Lazy IJSRuntime resolution
├── Redux DevTools integration
├── Action/state serialization
└── Graceful degradation
```

**PersistenceMiddleware:**
```
PersistenceMiddleware<TState>
├── IPersistenceProvider abstraction
├── JSON serialization
├── Debounced saves
└── Hydration on load
```

**LoggingMiddleware:**
```
LoggingMiddleware<TState>
├── Action logging
├── State diff logging
└── Custom logger support
```

### Middleware Pipeline Options

```csharp
public class MiddlewarePipelineOptions
{
    public int MaxRetries { get; set; } = 3;
    public bool StopOnError { get; set; } = false;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
```

---

## Selectors

### Memoization Strategy

```csharp
public TResult Select(TState state)
{
    // Cache hit: same state reference
    if (_hasCache && ReferenceEquals(_lastState, state))
    {
        return _cachedResult!;
    }

    // Compute new result
    var result = _selector(state);

    // Update cache
    _lastState = state;
    _cachedResult = result;
    _hasCache = true;

    return result;
}
```

### Composed Selectors

```
Selector A ─────┐
                ├──► Combiner ──► Result
Selector B ─────┘

// Only recomputes when A or B changes
```

---

## Async Actions

### AsyncData<T> State Machine

```
                  ┌─────────────┐
                  │  NotAsked   │
                  └──────┬──────┘
                         │ Start request
                         ▼
                  ┌─────────────┐
            ┌─────│   Loading   │─────┐
            │     └─────────────┘     │
            │                         │
     Success│                         │Failure
            ▼                         ▼
     ┌─────────────┐           ┌─────────────┐
     │   Success   │           │   Failure   │
     │   (Data)    │           │   (Error)   │
     └─────────────┘           └─────────────┘
```

### AsyncActionExecutor

```csharp
await executor.ExecuteAsync(
    asyncAction: () => api.FetchDataAsync(),
    loading: s => s with { IsLoading = true },
    success: (s, data) => s with { Data = data, IsLoading = false },
    error: (s, ex) => s with { Error = ex.Message, IsLoading = false }
);
```

---

## Package Structure

```
EasyAppDev.Blazor.Store/
├── Core/
│   ├── Store.cs                 // Main store implementation
│   ├── StoreBuilder.cs          // Fluent builder
│   ├── IStore.cs                // Main interface
│   ├── IStateReader.cs          // Read interface
│   ├── IStateWriter.cs          // Write interface
│   ├── IStateObservable.cs      // Subscribe interface
│   └── SubscriptionManager.cs   // Subscription handling
│
├── Blazor/
│   ├── StoreComponent.cs        // Base component
│   └── SelectorStoreComponent.cs // Selector component
│
├── Middleware/
│   ├── IMiddleware.cs           // Middleware interface
│   ├── MiddlewarePipeline.cs    // Pipeline orchestration
│   └── LoggingMiddleware.cs     // Logging implementation
│
├── DevTools/
│   ├── DevToolsMiddleware.cs    // DevTools integration
│   └── devtools.js              // JS interop
│
├── Persistence/
│   ├── IPersistenceProvider.cs  // Provider interface
│   ├── LocalStorageProvider.cs  // LocalStorage impl
│   ├── SessionStorageProvider.cs // SessionStorage impl
│   └── PersistenceMiddleware.cs // Auto-save middleware
│
├── Selectors/
│   ├── ISelector.cs             // Selector interface
│   └── MemoizedSelector.cs      // Memoized implementation
│
├── AsyncActions/
│   ├── AsyncData.cs             // Async state wrapper
│   ├── AsyncAction.cs           // Action wrapper
│   └── AsyncActionExecutor.cs   // Execution helper
│
├── Utilities/
│   ├── DebounceManager.cs       // Debounce utility
│   ├── ThrottleManager.cs       // Throttle utility
│   └── LazyCache.cs             // Caching utility
│
└── Extensions/
    └── ServiceCollectionExtensions.cs // DI helpers
```

---

## Performance Considerations

### Memory
- State references are cheap (immutable, shared)
- Selector results are cached
- Subscriptions are lightweight

### CPU
- Equality checks prevent unnecessary updates
- Memoization prevents recomputation
- Async operations don't block UI

### Recommendations
1. Use selectors for expensive computations
2. Use selector subscriptions for large states
3. Debounce rapid updates (typing, dragging)
4. Throttle high-frequency events (scroll, resize)

---

## Security

### State Isolation
- Scoped stores isolate user data
- Singleton stores share data (be careful)

### Persistence
- LocalStorage is visible in browser tools
- Don't persist sensitive data without encryption
- Consider server-side persistence for sensitive data

### DevTools
- Disable in production for sensitive data
- State is visible in Redux DevTools extension

---

[Back to Roadmap](ROADMAP.md)
