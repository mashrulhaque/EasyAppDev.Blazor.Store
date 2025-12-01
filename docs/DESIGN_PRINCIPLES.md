# Design Principles

> The guiding principles behind every decision

## Core Philosophy

**Simple. Type-Safe. Pleasant.**

We build a library that developers *want* to use. Not because they have to, but because it makes their lives easier.

---

## The Principles

### 1. Simplicity Over Features

**What it means:**
- Every feature must justify its complexity
- The default path should require minimal code
- Advanced features should not complicate basic usage

**In practice:**
```csharp
// Good: Simple default
builder.Services.AddStore(new CounterState(0));

// Good: Progressive complexity
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithDevTools(sp, "Counter")
        .WithPersistence(sp, "counter")
);

// Bad: Forcing complexity
builder.Services.AddStore(new StoreOptions<CounterState>
{
    InitialState = new CounterState(0),
    Comparer = EqualityComparer<CounterState>.Default,
    MiddlewarePipeline = new MiddlewarePipeline<CounterState>(),
    // ... many required options
});
```

**When to add a feature:**
1. Does it solve a common problem?
2. Can it be optional?
3. Does it complicate the simple case?
4. Is there a simpler alternative?

---

### 2. Immutability is Non-Negotiable

**What it means:**
- State must never be mutated
- Every API must produce new state
- Mutability is a bug, not a feature

**In practice:**
```csharp
// Good: Immutable update
public CounterState Increment() => this with { Count = Count + 1 };

// Bad: Mutation
public void Increment() { Count++; }  // Never!

// Good: Immutable collections
public record TodoState(ImmutableList<Todo> Items);

// Bad: Mutable collections
public record TodoState(List<Todo> Items);  // Can be mutated!
```

**Why:**
- Predictable state changes
- Easy debugging (compare references)
- Thread safety
- Time-travel debugging
- Undo/redo support

---

### 3. Type Safety First

**What it means:**
- Leverage the C# compiler
- Catch errors at compile time
- No magic strings for critical paths

**In practice:**
```csharp
// Good: Typed actions
public record Increment;
await store.Dispatch(new Increment());

// Bad: String actions
await store.Dispatch("INCREMENT");  // Typo-prone

// Good: Typed selectors
var count = store.Select(s => s.Count);

// Bad: String selectors
var count = store.Select("count");  // No compile-time check
```

**Where we compromise:**
- Action names for DevTools (optional, debugging only)
- Persistence keys (runtime, but validated)

---

### 4. Async by Default

**What it means:**
- Primary APIs are async
- Synchronous is the exception
- Never block the UI thread

**In practice:**
```csharp
// Good: Async first
await store.UpdateAsync(s => s.Increment());

// Deprecated: Synchronous
store.Update(s => s.Increment());  // Marked [Obsolete]

// Good: Async state methods
public async Task<UserState> LoadUser(int id, IUserService service)
{
    var user = await service.GetUserAsync(id);
    return this with { User = user };
}
```

**Why:**
- Blazor is async by nature
- Prevents UI freezing
- Enables concurrent operations
- Avoids deadlocks

---

### 5. Fail Gracefully

**What it means:**
- Optional features should not crash the app
- Provide fallbacks for missing dependencies
- Log warnings, don't throw exceptions (for optional features)

**In practice:**
```csharp
// Good: Graceful degradation
public async Task OnAfterUpdateAsync(...)
{
    if (!_initialized || _devToolsModule == null)
        return;  // DevTools not available, skip silently

    try
    {
        await _devToolsModule.InvokeVoidAsync(...);
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "DevTools communication failed");
        // Continue - app still works
    }
}

// Bad: Crashing on optional feature
public async Task OnAfterUpdateAsync(...)
{
    await _devToolsModule.InvokeVoidAsync(...);  // Throws if not available
}
```

**When to throw:**
- Required parameters are null
- State invariants are violated
- Security boundaries are crossed

---

### 6. Composition Over Inheritance

**What it means:**
- Prefer small, focused components
- Enable mixing and matching
- Avoid deep inheritance hierarchies

**In practice:**
```csharp
// Good: Interface composition
public interface IStore<TState> :
    IStateReader<TState>,
    IStateWriter<TState>,
    IStateObservable<TState>
{ }

// Components depend on what they need
public class ReadOnlyComponent
{
    public ReadOnlyComponent(IStateReader<CounterState> reader) { }
}

// Bad: Monolithic interface
public interface IStore<TState>
{
    TState GetState();
    void Update(...);
    Task UpdateAsync(...);
    IDisposable Subscribe(...);
    void AddMiddleware(...);
    void EnableDevTools(...);
    // ... 20 more methods
}
```

---

### 7. Test Everything

**What it means:**
- If it's not tested, it's broken
- Tests are documentation
- Tests prevent regressions

**In practice:**
```csharp
// Every public API has tests
[Fact]
public void Increment_ShouldIncreaseCountByOne()
{
    var state = new CounterState(5);
    var newState = state.Increment();

    newState.Count.Should().Be(6);
    state.Count.Should().Be(5);  // Original unchanged
}

// Edge cases are covered
[Fact]
public void Update_WithNullUpdater_ShouldThrow()
{
    var store = CreateStore(new CounterState(0));

    var act = () => store.UpdateAsync(null!);

    act.Should().ThrowAsync<ArgumentNullException>();
}
```

**Test pyramid:**
- 80% Unit tests (state methods, utilities)
- 15% Integration tests (store + middleware)
- 5% Component tests (bUnit)

---

### 8. Convention Over Configuration

**What it means:**
- Sensible defaults
- Zero-config should work
- Configuration is opt-in

**In practice:**
```csharp
// Good: Works without configuration
builder.Services.AddStore(new CounterState(0));
// Uses default comparer, no middleware, no persistence

// Good: Configure only what you need
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store.WithDevTools(sp, "Counter")
);

// Bad: Requiring configuration
builder.Services.AddStore(new StoreConfig<CounterState>
{
    State = new CounterState(0),
    Comparer = ...,      // Required
    Logger = ...,        // Required
    Middlewares = ...,   // Required
});
```

**Conventions:**
- State type name as DevTools name
- `EqualityComparer<T>.Default` for comparison
- JSON with camelCase for persistence
- Scoped lifetime for utilities

---

### 9. Progressive Disclosure

**What it means:**
- Simple things should be simple
- Complex things should be possible
- Complexity is revealed gradually

**In practice:**

**Level 1: Basic usage**
```csharp
// Define state
public record CounterState(int Count);

// Register
builder.Services.AddStore(new CounterState(0));

// Use
@inherits StoreComponent<CounterState>
<p>Count: @State.Count</p>
```

**Level 2: With features**
```csharp
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithDevTools(sp, "Counter")
        .WithPersistence(sp, "counter")
);
```

**Level 3: Advanced**
```csharp
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithMiddleware(new CustomMiddleware())
        .WithComparer(new CustomComparer())
        .ConfigureMiddleware(opts => opts.MaxRetries = 5)
);
```

---

### 10. Explicit Over Implicit

**What it means:**
- Make behavior visible
- Avoid hidden side effects
- State changes are intentional

**In practice:**
```csharp
// Good: Explicit update
await Update(s => s.Increment(), "INCREMENT");

// Bad: Implicit update (hidden mutation)
State.Count++;  // Side effect!

// Good: Explicit subscription
_subscription = Store.Subscribe(OnStateChanged);

// Bad: Implicit subscription (magic)
// Component auto-subscribes to all stores it accesses
```

---

## Anti-Patterns

### What We Avoid

**1. God Objects**
```csharp
// Bad: One store to rule them all
public record AppState(
    UserState User,
    CartState Cart,
    ProductState Products,
    NotificationState Notifications,
    UIState UI,
    // ... 50 more properties
);
```

**2. Stringly Typed**
```csharp
// Bad: String keys everywhere
store.Dispatch("INCREMENT");
store.Select("user.profile.address.city");
```

**3. Hidden Complexity**
```csharp
// Bad: Magic behind simple API
var state = store.GetState();  // Actually makes 5 API calls
```

**4. Leaky Abstractions**
```csharp
// Bad: Exposing implementation details
public class Store<T>
{
    public SemaphoreSlim Lock { get; }  // Don't expose internals
    public List<Action<T>> Subscribers { get; }  // Don't expose internals
}
```

**5. Premature Abstraction**
```csharp
// Bad: Abstracting before needed
public interface IStateTransformer<TState, TAction>
{
    TState Transform(TState state, TAction action);
}

public interface IStateTransformerFactory<TState>
{
    IStateTransformer<TState, TAction> Create<TAction>();
}
// ... 10 more interfaces for simple increment
```

---

## Decision Framework

When making design decisions, ask:

1. **Is it simple?**
   - Can a beginner understand it?
   - Is the API surface minimal?

2. **Is it safe?**
   - Does it preserve immutability?
   - Is it type-safe?
   - Is it thread-safe?

3. **Is it necessary?**
   - Does it solve a real problem?
   - Is it commonly needed?
   - Can users implement it themselves easily?

4. **Is it consistent?**
   - Does it follow existing patterns?
   - Does it match user expectations?

5. **Is it testable?**
   - Can it be unit tested?
   - Are dependencies injectable?

---

## The Litmus Test

Before adding any feature, answer:

> "Would a developer using this for the first time find it intuitive?"

If no, reconsider the design.

---

[Back to Roadmap](ROADMAP.md)
