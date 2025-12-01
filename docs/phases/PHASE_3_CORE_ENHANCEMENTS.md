# Phase 3: Core Enhancements

> Version: 2.0.0 | Status: Not Started | Risk: Medium

## Overview

Phase 3 introduces foundational features that enable advanced patterns. This is a major version bump with intentional breaking changes to set up for the future.

**Goal:** Add the missing pieces that make complex applications possible.

---

## Features

### 3.1 Computed/Derived State

**Problem:**
Currently, users must manually manage derived values:
```csharp
public record CartState(ImmutableList<CartItem> Items)
{
    // Users calculate this every render
    public decimal Total => Items.Sum(i => i.Price * i.Quantity);
}
```

**Solution:** Built-in computed state with automatic memoization.

**API Design:**

**Option A: Record Property (Recommended)**
```csharp
public record CartState(ImmutableList<CartItem> Items)
{
    // Computed on access, memoized by record equality
    public decimal Total => Items.Sum(i => i.Price * i.Quantity);
    public int ItemCount => Items.Count;
    public bool IsEmpty => Items.Count == 0;
}
```
This already works with records - no library change needed. Document as best practice.

**Option B: Explicit Computed Registration**
```csharp
builder.Services.AddStore(
    new CartState(ImmutableList<CartItem>.Empty),
    (store, sp) => store
        .WithComputed("total", s => s.Items.Sum(i => i.Price * i.Quantity))
        .WithComputed("itemCount", s => s.Items.Count)
);

// Access
var total = store.GetComputed<decimal>("total");
```

**Option C: Selector Factory (Chosen)**
```csharp
// Define selectors once
public static class CartSelectors
{
    public static readonly ISelector<CartState, decimal> Total =
        Selectors.Create((CartState s) => s.Items.Sum(i => i.Price * i.Quantity));

    public static readonly ISelector<CartState, int> ItemCount =
        Selectors.Create((CartState s) => s.Items.Count);

    // Composed selectors
    public static readonly ISelector<CartState, string> Summary =
        Selectors.Create(
            Total,
            ItemCount,
            (total, count) => $"{count} items, ${total:F2}");
}

// Usage in component
var total = CartSelectors.Total.Select(State);

// Or with subscription
store.Subscribe(CartSelectors.Total, total => Console.WriteLine($"Total: {total}"));
```

**Implementation:**
```csharp
public static class Selectors
{
    public static ISelector<TState, TResult> Create<TState, TResult>(
        Func<TState, TResult> selector)
        where TState : notnull
    {
        return new MemoizedSelector<TState, TResult>(selector);
    }

    public static ISelector<TState, TResult> Create<TState, T1, T2, TResult>(
        ISelector<TState, T1> selector1,
        ISelector<TState, T2> selector2,
        Func<T1, T2, TResult> combiner)
        where TState : notnull
    {
        return new ComposedSelector<TState, T1, T2, TResult>(selector1, selector2, combiner);
    }
}
```

---

### 3.2 Store Slices / Modules

**Problem:**
Large applications have monolithic state that's hard to manage:
```csharp
public record AppState(
    UserState User,
    CartState Cart,
    ProductState Products,
    NotificationState Notifications,
    // ... 20 more properties
);
```

**Solution:** Composable store slices.

**API Design:**
```csharp
// Define slices
public record UserSlice(User? CurrentUser, bool IsLoading);
public record CartSlice(ImmutableList<CartItem> Items);
public record ProductSlice(ImmutableList<Product> Products);

// Compose into app state
public record AppState(
    UserSlice User,
    CartSlice Cart,
    ProductSlice Products);

// Register with slice configuration
builder.Services.AddStore(
    AppState.Initial,
    (store, sp) => store
        .WithSlice(s => s.User, slice => slice
            .WithDevTools(sp, "User")
            .WithPersistence(sp, "user-state"))
        .WithSlice(s => s.Cart, slice => slice
            .WithDevTools(sp, "Cart")
            .WithPersistence(sp, "cart-state"))
        .WithSlice(s => s.Products, slice => slice
            .WithDevTools(sp, "Products"))
);
```

**Slice-Scoped Components:**
```csharp
// Component only subscribes to Cart slice changes
@inherits SliceComponent<AppState, CartSlice>

@code {
    protected override CartSlice SelectSlice(AppState state) => state.Cart;

    // Only re-renders when Cart changes
    async Task AddItem(Product product)
    {
        await UpdateSlice(cart => cart with {
            Items = cart.Items.Add(new CartItem(product))
        });
    }
}
```

**Implementation:**
```csharp
public class SliceComponent<TState, TSlice> : ComponentBase, IDisposable
    where TState : notnull
    where TSlice : notnull
{
    [Inject] protected IStore<TState> Store { get; set; } = default!;

    protected abstract TSlice SelectSlice(TState state);

    protected TSlice Slice => SelectSlice(Store.GetState());

    protected Task UpdateSlice(Func<TSlice, TSlice> updater)
    {
        return Store.UpdateAsync(state =>
        {
            var currentSlice = SelectSlice(state);
            var newSlice = updater(currentSlice);
            return UpdateState(state, newSlice);
        });
    }

    // Uses reflection or source generator to update slice in state
    protected abstract TState UpdateState(TState state, TSlice newSlice);
}
```

---

### 3.3 Functional Middleware Syntax

**Problem:**
Current middleware requires class boilerplate:
```csharp
public class LoggingMiddleware<TState> : IMiddleware<TState>
{
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        Console.WriteLine($"Before: {action}");
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        Console.WriteLine($"After: {action}");
        return Task.CompletedTask;
    }
}
```

**Solution:** Functional middleware for simple cases.

**API Design:**
```csharp
builder.Services.AddStore(state, (store, sp) => store
    // Simple logging
    .Use(async (ctx, next) =>
    {
        Console.WriteLine($"Before: {ctx.Action}");
        await next();
        Console.WriteLine($"After: {ctx.Action}");
    })

    // Performance tracking
    .Use(async (ctx, next) =>
    {
        var sw = Stopwatch.StartNew();
        await next();
        sw.Stop();
        if (sw.ElapsedMilliseconds > 100)
        {
            logger.LogWarning("Slow update: {Action} took {Ms}ms", ctx.Action, sw.ElapsedMilliseconds);
        }
    })

    // Conditional middleware
    .UseWhen(
        ctx => ctx.Action?.StartsWith("FETCH_") == true,
        async (ctx, next) =>
        {
            // Only runs for FETCH_* actions
            await next();
        }
    )
);
```

**Implementation:**
```csharp
public record MiddlewareContext<TState>(
    TState CurrentState,
    TState? NewState,
    string? Action,
    IServiceProvider Services);

public delegate Task MiddlewareDelegate<TState>(MiddlewareContext<TState> context);

public static class StoreBuilderExtensions
{
    public static StoreBuilder<TState> Use<TState>(
        this StoreBuilder<TState> builder,
        Func<MiddlewareContext<TState>, Func<Task>, Task> middleware)
        where TState : notnull
    {
        return builder.WithMiddleware(new FunctionalMiddleware<TState>(middleware));
    }

    public static StoreBuilder<TState> UseWhen<TState>(
        this StoreBuilder<TState> builder,
        Func<MiddlewareContext<TState>, bool> predicate,
        Func<MiddlewareContext<TState>, Func<Task>, Task> middleware)
        where TState : notnull
    {
        return builder.Use(async (ctx, next) =>
        {
            if (predicate(ctx))
                await middleware(ctx, next);
            else
                await next();
        });
    }
}
```

---

### 3.4 Improved Persistence API

**Problem:**
Current hydration API is awkward:
```csharp
// Async builder is confusing
var builder = await StoreBuilder.Create(state).WithHydratedStateAsync(provider, key);
```

**Solution:** Cleaner hydration with events.

**API Design:**
```csharp
builder.Services.AddStore(
    CartState.Empty,
    (store, sp) => store
        .WithPersistence(new PersistenceOptions
        {
            Provider = sp.GetRequiredService<IPersistenceProvider>(),
            Key = "cart-state",
            DebounceMs = 500,

            // New: Hydration control
            HydrateOnInit = true,
            OnHydrationSuccess = state => logger.LogInformation("Loaded cart with {Count} items", state.Items.Count),
            OnHydrationFailure = ex => logger.LogWarning(ex, "Failed to load cart"),
            OnHydrationSkipped = () => logger.LogDebug("No persisted cart found"),

            // New: Selective persistence
            ShouldPersist = (prev, curr, action) => action != "TEMP_UPDATE",

            // New: State transformation on load
            TransformOnLoad = state => state with {
                // Clear sensitive data on reload
                CheckoutInProgress = false
            }
        })
);
```

**Simpler Overload:**
```csharp
.WithPersistence(sp, "cart-state")                    // Basic
.WithPersistence(sp, "cart-state", debounceMs: 500)   // With debounce
.WithPersistence(sp, new PersistenceOptions {...})    // Full control
```

---

### 3.5 Structured Error Boundaries

**Problem:**
Errors in subscribers or middleware can be hard to track.

**Solution:** Centralized error handling.

**API Design:**
```csharp
builder.Services.AddStore(state, (store, sp) => store
    .OnError(error =>
    {
        logger.LogError(error.Exception,
            "Store error in {Location}: {Message}",
            error.Location,
            error.Exception.Message);

        // Optionally report to error tracking
        errorTracker.CaptureException(error.Exception, new Dictionary<string, object>
        {
            ["store"] = typeof(TState).Name,
            ["action"] = error.Action ?? "unknown",
            ["location"] = error.Location.ToString()
        });
    })
);

public record StoreError<TState>(
    Exception Exception,
    TState? State,
    string? Action,
    ErrorLocation Location);

public enum ErrorLocation
{
    Middleware,
    Updater,
    Subscriber,
    Persistence,
    DevTools
}
```

**Component-Level Error Handling:**
```csharp
@inherits StoreComponent<CartState>

@code {
    protected override void OnStoreError(StoreError<CartState> error)
    {
        // Component-specific error handling
        ShowErrorToast(error.Exception.Message);
    }
}
```

---

## Implementation Priority

### Must Have (v2.0.0)
1. Selector factory with composition
2. Functional middleware syntax
3. Improved persistence API
4. Structured error boundaries

### Should Have (v2.1.0)
1. Store slices basic support
2. SliceComponent base class

### Nice to Have (v2.2.0)
1. Advanced slice configuration
2. Slice-specific DevTools

---

## Breaking Changes

### API Changes
| Before | After |
|--------|-------|
| `Update(...)` | Removed (use `UpdateAsync`) |
| `WithDevTools(string)` | Removed (use `WithDevTools(IServiceProvider, string)`) |
| `WithPersistence(provider, key)` | `WithPersistence(sp, key)` or `WithPersistence(sp, options)` |

### Behavioral Changes
- Middleware now receives full context object
- Error handling is centralized by default
- Persistence hydration is async and event-driven

---

## Migration Guide

### From 1.2.x to 2.0.0

**1. Persistence API**
```csharp
// Before
.WithPersistence(provider, "key")

// After (simple)
.WithPersistence(sp, "key")

// After (with options)
.WithPersistence(sp, new PersistenceOptions
{
    Key = "key",
    DebounceMs = 500
})
```

**2. Error Handling**
```csharp
// Before: Errors logged but not centralized

// After: Add error handler
.OnError(error => logger.LogError(error.Exception, "Store error"))
```

**3. Selectors (new feature)**
```csharp
// Define selectors
public static class MySelectors
{
    public static readonly ISelector<MyState, int> Count =
        Selectors.Create((MyState s) => s.Items.Count);
}

// Use in component
var count = MySelectors.Count.Select(State);
```

---

## Testing Requirements

### New Test Areas
```
tests/EasyAppDev.Blazor.Store.Tests/
├── Selectors/
│   ├── SelectorFactoryTests.cs
│   ├── ComposedSelectorTests.cs
│   └── SelectorPerformanceTests.cs
├── Middleware/
│   ├── FunctionalMiddlewareTests.cs
│   └── ConditionalMiddlewareTests.cs
├── Persistence/
│   ├── PersistenceOptionsTests.cs
│   └── HydrationEventTests.cs
└── ErrorHandling/
    ├── StoreErrorTests.cs
    └── ErrorBoundaryTests.cs
```

### Performance Tests
- Selector memoization efficiency
- Middleware chain overhead
- Error handling impact

---

## Success Criteria

1. Computed state is simple and intuitive
2. Middleware is easy to write inline
3. Persistence is flexible and observable
4. Errors never silently fail
5. Migration from 1.x is straightforward

---

[← Phase 2](PHASE_2_CLEANUP.md) | [Back to Roadmap](../ROADMAP.md) | [Phase 4 →](PHASE_4_ADVANCED_FEATURES.md)
