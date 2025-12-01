# API Design Guidelines

> How we design public APIs that developers love

## Guiding Principles

1. **Pit of Success** - Make it easy to do the right thing
2. **Progressive Disclosure** - Simple things simple, complex things possible
3. **Discoverability** - APIs should be intuitive to explore
4. **Consistency** - Similar operations should have similar APIs

---

## Method Naming

### Verb Conventions

| Verb | Meaning | Example |
|------|---------|---------|
| `Get` | Returns current value, no side effects | `GetState()` |
| `Create` | Factory method, returns new instance | `Create(initialState)` |
| `Update` | Modifies state | `UpdateAsync(updater)` |
| `With` | Builder method, returns modified builder | `WithDevTools()` |
| `Add` | Adds item to collection/registry | `AddStore()` |
| `Subscribe` | Registers callback, returns disposable | `Subscribe(callback)` |
| `Execute` | Performs action with side effects | `ExecuteAsync(action)` |
| `Build` | Finalizes builder, returns result | `Build()` |

### Async Methods

Always suffix async methods with `Async`:

```csharp
// Good
Task UpdateAsync(...)
Task<T> LoadAsync(...)

// Bad
Task Update(...)
Task<T> Load(...)
```

Exception: When the type makes async obvious:

```csharp
// Acceptable
Task<IStore<T>> CreateStore(...)  // Returns Task, clearly async
```

---

## Parameter Design

### Parameter Order

1. Required parameters first
2. Configuration/options next
3. Optional parameters last
4. Cancellation token last (if applicable)

```csharp
// Good
public Task UpdateAsync(
    Func<TState, TState> updater,    // Required
    string? action = null,            // Optional
    CancellationToken ct = default)   // Cancellation

// Bad
public Task UpdateAsync(
    string? action = null,
    Func<TState, TState> updater)  // Required after optional
```

### Avoid Boolean Parameters

```csharp
// Bad: What does 'true' mean?
await store.UpdateAsync(updater, true);

// Good: Named parameter or enum
await store.UpdateAsync(updater, notifyImmediately: true);

// Better: Separate methods
await store.UpdateAsync(updater);
await store.UpdateAndNotifyAsync(updater);
```

### Use Options Objects for Many Parameters

```csharp
// Bad: Too many parameters
public void Configure(
    int maxRetries,
    int delayMs,
    bool logErrors,
    bool stopOnError,
    string? prefix)

// Good: Options object
public void Configure(MiddlewarePipelineOptions options)

public class MiddlewarePipelineOptions
{
    public int MaxRetries { get; set; } = 3;
    public int DelayMs { get; set; } = 100;
    public bool LogErrors { get; set; } = true;
    public bool StopOnError { get; set; } = false;
    public string? Prefix { get; set; }
}
```

---

## Return Types

### Use Task for Async Operations

```csharp
// Good
public Task UpdateAsync(...)
public Task<TState> GetStateAsync(...)

// Bad: Using void for async
public async void UpdateAsync(...)  // Fire and forget - dangerous!
```

### Return IDisposable for Subscriptions

```csharp
// Good: Caller controls lifetime
public IDisposable Subscribe(Action<TState> callback)

// Usage
using var subscription = store.Subscribe(OnStateChanged);
// or
_subscription = store.Subscribe(OnStateChanged);
// ... later
_subscription.Dispose();
```

### Use Nullable for Optional Returns

```csharp
// Good: Explicit nullable
public TState? TryGetState()
public async Task<string?> LoadAsync(string key)

// Bad: Magic values
public TState GetState()  // Returns default(TState) if not found?
```

---

## Builder Pattern

### Fluent API

```csharp
// Good: Chainable methods
var store = StoreBuilder<CounterState>
    .Create(new CounterState(0))
    .WithLogging()
    .WithDevTools(sp, "Counter")
    .WithPersistence(sp, "counter")
    .Build();

// Implementation
public StoreBuilder<TState> WithLogging()
{
    _middlewares.Add(new LoggingMiddleware<TState>());
    return this;  // Return this for chaining
}
```

### Factory Method Entry Point

```csharp
// Good: Static factory
var builder = StoreBuilder<CounterState>.Create(initialState);

// Bad: Public constructor
var builder = new StoreBuilder<CounterState>(initialState);
```

### Validate on Build

```csharp
public IStore<TState> Build()
{
    // Validate configuration
    if (_initialState == null)
        throw new InvalidOperationException("Initial state is required");

    // Create and return
    return new Store<TState>(...);
}
```

---

## Extension Methods

### When to Use

- Adding functionality to interfaces
- DI registration helpers
- Utility methods for types you don't own

```csharp
// Good: DI extension
public static IServiceCollection AddStore<TState>(
    this IServiceCollection services,
    TState initialState)

// Good: Convenience method
public static Task UpdateOptimistic<TState>(
    this IStore<TState> store,
    Func<TState, TState> optimistic,
    Func<Task> action)
```

### Naming

Extension method classes: `{Target}Extensions`

```csharp
public static class StoreExtensions { }
public static class ServiceCollectionExtensions { }
```

---

## Interface Design

### Interface Segregation

Split large interfaces into focused ones:

```csharp
// Good: Focused interfaces
public interface IStateReader<TState>
{
    TState GetState();
}

public interface IStateWriter<TState>
{
    Task UpdateAsync(Func<TState, TState> updater, string? action = null);
}

public interface IStateObservable<TState>
{
    IDisposable Subscribe(Action<TState> callback);
}

// Composed interface
public interface IStore<TState> :
    IStateReader<TState>,
    IStateWriter<TState>,
    IStateObservable<TState>,
    IDisposable
{ }
```

### Default Interface Methods

Use sparingly for backward compatibility:

```csharp
public interface IStore<TState>
{
    // New method with default implementation
    Task<TResult> SelectAsync<TResult>(Func<TState, TResult> selector)
        => Task.FromResult(selector(GetState()));
}
```

---

## Error Handling

### Throw Early

```csharp
public Task UpdateAsync(Func<TState, TState> updater, string? action = null)
{
    // Validate immediately
    ArgumentNullException.ThrowIfNull(updater);
    ThrowIfDisposed();

    // Then do work
    return UpdateInternalAsync(updater, action);
}
```

### Use Specific Exceptions

```csharp
// Good: Specific exceptions
throw new ArgumentNullException(nameof(updater));
throw new ObjectDisposedException(nameof(Store<TState>));
throw new InvalidOperationException("Updater returned null");

// Bad: Generic exception
throw new Exception("Something went wrong");
```

### Document Exceptions

```csharp
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="updater"/> is null.
/// </exception>
/// <exception cref="ObjectDisposedException">
/// Thrown when the store has been disposed.
/// </exception>
public Task UpdateAsync(Func<TState, TState> updater, string? action = null)
```

---

## Generics

### Constraints

Use constraints to enable functionality:

```csharp
// Good: Constraint enables features
public class Store<TState> where TState : notnull

// Enables:
// - Non-nullable state guarantee
// - Better null analysis

// Bad: Unnecessary constraint
public class Store<TState> where TState : class, new()
// Why require parameterless constructor?
```

### Type Parameter Naming

| Count | Convention | Example |
|-------|------------|---------|
| 1 | `T` or `TDescriptive` | `T`, `TState` |
| 2+ | Descriptive names | `TState`, `TResult` |

```csharp
// Good
public interface ISelector<TState, TResult>
public class Store<TState>

// Bad
public interface ISelector<T, U>  // What are T and U?
```

---

## Async API Design

### Return Task, Not void

```csharp
// Good
public Task UpdateAsync(...)

// Bad
public async void UpdateAsync(...)  // Can't be awaited!
```

### Support Cancellation

```csharp
public async Task<T> LoadAsync(
    string key,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ... work
}
```

### ConfigureAwait in Libraries

```csharp
// In library code
await _lock.WaitAsync().ConfigureAwait(false);

// In application code (Blazor)
await store.UpdateAsync(...);  // No ConfigureAwait needed
```

---

## Callback Design

### Use Func for Transformations

```csharp
// Good: Returns transformed value
Task UpdateAsync(Func<TState, TState> updater)

// Usage
await store.UpdateAsync(s => s with { Count = s.Count + 1 });
```

### Use Action for Side Effects

```csharp
// Good: No return value
IDisposable Subscribe(Action<TState> callback)

// Usage
store.Subscribe(state => Console.WriteLine(state.Count));
```

### Provide Overloads

```csharp
// Basic callback
IDisposable Subscribe(Action<TState> callback);

// With selector
IDisposable Subscribe<TSelected>(
    Func<TState, TSelected> selector,
    Action<TSelected> callback);

// With comparer
IDisposable Subscribe<TSelected>(
    Func<TState, TSelected> selector,
    Action<TSelected> callback,
    IEqualityComparer<TSelected> comparer);
```

---

## Deprecation

### Mark Obsolete

```csharp
[Obsolete("Use UpdateAsync instead. This method can cause deadlocks.", error: false)]
public void Update(Func<TState, TState> updater, string? action = null)
{
    UpdateAsync(updater, action).GetAwaiter().GetResult();
}
```

### Deprecation Timeline

1. **v1.x**: Mark `[Obsolete]` with warning
2. **v2.0**: Mark `[Obsolete(error: true)]`
3. **v3.0**: Remove entirely

---

## API Checklist

Before releasing a public API:

- [ ] **Named correctly** - Clear, consistent naming
- [ ] **Documented** - XML docs with examples
- [ ] **Null-safe** - Proper null handling
- [ ] **Thread-safe** - Documented threading behavior
- [ ] **Testable** - Can be unit tested
- [ ] **Extensible** - Virtual methods where appropriate
- [ ] **Disposable** - Proper resource cleanup
- [ ] **Async** - Async-first design
- [ ] **Exception-safe** - Proper exception handling
- [ ] **Backward compatible** - Doesn't break existing code

---

[Back to Roadmap](ROADMAP.md)
