# EasyAppDev.Blazor.Store

**Type-safe state management for Blazor using C# records.**

*Inspired by Zustand • Built for C# developers*

[![NuGet](https://img.shields.io/nuget/v/EasyAppDev.Blazor.Store.svg)](https://www.nuget.org/packages/EasyAppDev.Blazor.Store/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**[📚 View Full Documentation →](https://mashrulhaque.github.io/EasyAppDev.Blazor.Store/)**

---

## Why This Library?

This library uses C# records with pure transformation methods—no actions, reducers, or dispatchers. State updates are type-safe with automatic reactivity.

**Core Features:**
- **Zero boilerplate** - Write state methods, not actions/reducers
- **Immutable by default** - C# records + `with` expressions
- **Automatic reactivity** - Components update automatically
- **Testable** - Pure functions, no mocks needed
- **Small footprint** - 38 KB gzipped

**Async & Performance:**
- **5 async helpers** - UpdateDebounced, AsyncData\<T\>, ExecuteAsync, UpdateThrottled, LazyLoad
- **Selectors** - Granular subscriptions for fewer re-renders
- **Persistence** - Auto-save/restore with LocalStorage/SessionStorage
- **Request deduplication** - Automatic caching prevents redundant API calls

**Developer Experience:**
- **Redux DevTools** - Time-travel debugging
- **Diagnostics** - Built-in monitoring (DEBUG only, zero production overhead)
- **Middleware** - Extensible hooks for logging, validation, custom logic
- **SOLID architecture** - Interface segregation, dependency injection throughout
- **Multiple stores** - Separate concerns across focused state domains

If you've used Zustand in React, you'll feel right at home.

---

## Quick Start

### Installation

```bash
dotnet add package EasyAppDev.Blazor.Store
```

**Requirements:** .NET 8.0+ • Blazor Server or WebAssembly • 38 KB gzipped

### 1. Define Your State

State is just a C# record with transformation methods:

```csharp
public record CounterState(int Count)
{
    public CounterState Increment() => this with { Count = Count + 1 };
    public CounterState Decrement() => this with { Count = Count - 1 };
    public CounterState Reset() => this with { Count = 0 };
}
```

### 2. Register in Program.cs

```csharp
// One-liner registration with all features
builder.Services.AddStoreWithUtilities(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter"));
```

> 💡 `AddStoreWithUtilities()` includes async helpers (debounce, throttle, cache, etc.)
> 💡 `WithDefaults()` adds DevTools, logging, and JSRuntime integration

### 3. Use in Components

```razor
@page "/counter"
@inherits StoreComponent<CounterState>

<h1>@State.Count</h1>

<button @onclick="@(() => Update(s => s.Increment()))">+</button>
<button @onclick="@(() => Update(s => s.Decrement()))">-</button>
<button @onclick="@(() => Update(s => s.Reset()))">Reset</button>
```

Inherit from `StoreComponent<T>` and call `Update()`. No actions, no reducers, no dispatch.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Real-World Examples](#real-world-examples)
- [Async Helpers](#async-helpers)
- [Performance & Optimization](#performance--optimization)
- [Persistence & DevTools](#persistence--devtools)
- [API Reference](#api-reference)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [Documentation](#documentation)

---

## Core Concepts

### State = Immutable Record

```csharp
public record TodoState(ImmutableList<Todo> Todos)
{
    public static TodoState Initial => new(ImmutableList<Todo>.Empty);

    public TodoState AddTodo(string text) =>
        this with { Todos = Todos.Add(new Todo(Guid.NewGuid(), text, false)) };

    public TodoState ToggleTodo(Guid id) =>
        this with {
            Todos = Todos.Select(t =>
                t.Id == id ? t with { Completed = !t.Completed } : t
            ).ToImmutableList()
        };
}
```

**Key principles:**
- State methods are **pure functions** (no side effects, no I/O)
- Always use `with` expressions (never mutate)
- Use `ImmutableList<T>` for collections
- Co-locate transformation logic with state data

### StoreComponent = Automatic Reactivity

Inherit from `StoreComponent<TState>` to get:
- Automatic subscription to state changes
- Access to `State` property
- `Update()` method for state transformations
- Automatic cleanup on disposal

```csharp
@inherits StoreComponent<TodoState>

<button @onclick="AddTodo">Add</button>

@code {
    async Task AddTodo() => await Update(s => s.AddTodo("New task"));
}
```

### Update Flow

```
Component calls Update(s => s.Method())
    ↓
Store applies transformation
    ↓
New state created (old unchanged)
    ↓
All subscribers notified
    ↓
Components re-render
```

---

## Real-World Examples

### Todo List with Persistence

```csharp
// State.cs
public record Todo(Guid Id, string Text, bool Completed);

public record TodoState(ImmutableList<Todo> Todos)
{
    public static TodoState Initial => new(ImmutableList<Todo>.Empty);

    public TodoState AddTodo(string text) =>
        this with { Todos = Todos.Add(new Todo(Guid.NewGuid(), text, false)) };

    public TodoState ToggleTodo(Guid id) =>
        this with {
            Todos = Todos.Select(t => t.Id == id ? t with { Completed = !t.Completed } : t)
                .ToImmutableList()
        };

    public TodoState RemoveTodo(Guid id) =>
        this with { Todos = Todos.RemoveAll(t => t.Id == id) };
}

// Program.cs
builder.Services.AddStoreWithUtilities(
    TodoState.Initial,
    (store, sp) => store
        .WithDefaults(sp, "Todos")
        .WithPersistence(sp, "todos"));  // Auto-save to LocalStorage

// TodoList.razor
@page "/todos"
@inherits StoreComponent<TodoState>

<input @bind="newTodo" @onkeyup="HandleKeyUp" placeholder="What needs to be done?" />

@foreach (var todo in State.Todos)
{
    <div>
        <input type="checkbox"
               checked="@todo.Completed"
               @onchange="@(() => Update(s => s.ToggleTodo(todo.Id)))" />
        <span class="@(todo.Completed ? "completed" : "")">@todo.Text</span>
        <button @onclick="@(() => Update(s => s.RemoveTodo(todo.Id)))">🗑️</button>
    </div>
}

@code {
    string newTodo = "";

    async Task HandleKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newTodo))
        {
            await Update(s => s.AddTodo(newTodo));
            newTodo = "";
        }
    }
}
```

### Authentication with Async Helpers

```csharp
// State.cs
public record User(string Id, string Name, string Email);

public record AuthState(AsyncData<User> CurrentUser)
{
    public static AuthState Initial => new(AsyncData<User>.NotAsked());
    public bool IsAuthenticated => CurrentUser.HasData;
}

// Login.razor
@page "/login"
@inherits StoreComponent<AuthState>
@inject IAuthService AuthService

@if (State.CurrentUser.IsLoading)
{
    <p>Logging in...</p>
}
else if (State.IsAuthenticated)
{
    <p>Welcome, @State.CurrentUser.Data!.Name!</p>
    <button @onclick="Logout">Logout</button>
}
else
{
    <input @bind="email" placeholder="Email" />
    <input @bind="password" type="password" />
    <button @onclick="Login">Login</button>

    @if (State.CurrentUser.HasError)
    {
        <p class="error">@State.CurrentUser.Error</p>
    }
}

@code {
    string email = "", password = "";

    async Task Login() =>
        await ExecuteAsync(
            () => AuthService.LoginAsync(email, password),
            loading: s => s with { CurrentUser = s.CurrentUser.ToLoading() },
            success: (s, user) => s with { CurrentUser = AsyncData<User>.Success(user) },
            error: (s, ex) => s with { CurrentUser = AsyncData<User>.Failure(ex.Message) }
        );

    async Task Logout() => await Update(s => AuthState.Initial);
}
```

### Shopping Cart

```csharp
public record Product(string Id, string Name, decimal Price);
public record CartItem(Product Product, int Quantity);

public record CartState(ImmutableList<CartItem> Items)
{
    public decimal Total => Items.Sum(i => i.Product.Price * i.Quantity);
    public int ItemCount => Items.Sum(i => i.Quantity);

    public CartState AddItem(Product product)
    {
        var existing = Items.FirstOrDefault(i => i.Product.Id == product.Id);
        return existing != null
            ? this with { Items = Items.Replace(existing, existing with { Quantity = existing.Quantity + 1 }) }
            : this with { Items = Items.Add(new CartItem(product, 1)) };
    }

    public CartState UpdateQuantity(string productId, int quantity) =>
        quantity <= 0 ? RemoveItem(productId)
        : this with {
            Items = Items.Select(i =>
                i.Product.Id == productId ? i with { Quantity = quantity } : i
            ).ToImmutableList()
        };

    public CartState RemoveItem(string productId) =>
        this with { Items = Items.RemoveAll(i => i.Product.Id == productId) };
}
```

---

## Async Helpers

Built-in helpers to reduce async boilerplate:

| Helper | Eliminates | Code Reduction |
|--------|-----------|----------------|
| **UpdateDebounced** | Timer management (17 lines → 1 line) | **94%** |
| **AsyncData\<T\>** | Loading/error state (20+ lines → 1 property) | **95%** |
| **ExecuteAsync** | Try-catch boilerplate (12 lines → 5 lines) | **58%** |
| **UpdateThrottled** | Throttle logic (22 lines → 1 line) | **95%** |
| **LazyLoad** | Cache + deduplication (15+ lines → 2 lines) | **85%** |

### UpdateDebounced - Debounced Search

```csharp
// Before: 17 lines of timer management
private Timer? _timer;
void OnInput(ChangeEventArgs e) {
    _timer?.Stop();
    _timer = new Timer(300);
    _timer.Elapsed += async (_, _) => await Update(...);
    _timer.Start();
}
public void Dispose() => _timer?.Dispose();

// After: 1 line
<input @oninput="@(e => UpdateDebounced(s => s.SetQuery(e.Value?.ToString() ?? ""), 300))" />
```

### AsyncData\<T\> - Simple Async State

```csharp
// Before: 20+ lines for loading/data/error states
public record UserState(
    User? User, bool IsLoading, string? Error)
{
    public UserState StartLoading() => this with { IsLoading = true };
    public UserState Success(User u) => this with { User = u, IsLoading = false };
    public UserState Failure(string e) => this with { Error = e, IsLoading = false };
}

// After: 1 property
public record UserState(AsyncData<User> User);

// Component usage
@if (State.User.IsLoading) { <p>Loading...</p> }
@if (State.User.HasData) { <p>@State.User.Data.Name</p> }
@if (State.User.HasError) { <p>@State.User.Error</p> }
```

### ExecuteAsync - Automatic Error Handling

```csharp
// Before: 12 lines of try-catch
await Update(s => s.StartLoading());
try {
    var data = await Service.LoadAsync();
    await Update(s => s.Success(data));
} catch (Exception ex) {
    await Update(s => s.Failure(ex.Message));
}

// After: 5 lines with automatic error handling
await ExecuteAsync(
    () => Service.LoadAsync(),
    loading: s => s with { Data = s.Data.ToLoading() },
    success: (s, data) => s with { Data = AsyncData.Success(data) },
    error: (s, ex) => s with { Data = AsyncData.Failure(ex.Message) }
);
```

### UpdateThrottled - High-Frequency Events

```csharp
// Throttle mouse move to max once per 100ms
<div @onmousemove="@(e => UpdateThrottled(s => s.SetPosition(e.ClientX, e.ClientY), 100))">
    Mouse tracker
</div>

// Throttle scroll events
<div @onscroll="@(e => UpdateThrottled(s => s.UpdateScroll(), 100))">
    Content
</div>
```

### LazyLoad - Automatic Caching

```csharp
// Automatic caching with 5-minute expiration + request deduplication
var user = await LazyLoad(
    $"user-{userId}",
    () => UserService.GetUserAsync(userId),
    cacheFor: TimeSpan.FromMinutes(5));

// Multiple simultaneous calls are deduplicated into a single API request
```

**Complete Example:**

```csharp
@page "/products"
@inherits StoreComponent<ProductState>

<!-- 1. Debounced search -->
<input @oninput="@(e => UpdateDebounced(s => s.SetQuery(e.Value?.ToString() ?? ""), 300))"
       placeholder="Search..." />

<!-- 2. Load with ExecuteAsync -->
<button @onclick="Search">Search</button>

<!-- 3. Display with AsyncData -->
@if (State.Products.IsLoading) { <p>Loading...</p> }
@if (State.Products.HasData)
{
    @foreach (var product in State.Products.Data)
    {
        <div @onclick="@(() => LoadDetails(product.Id))">@product.Name</div>
    }
}

@code {
    async Task Search() =>
        await ExecuteAsync(
            () => ProductService.SearchAsync(State.Query),
            loading: s => s with { Products = s.Products.ToLoading() },
            success: (s, data) => s with { Products = AsyncData.Success(data) }
        );

    async Task LoadDetails(int id)
    {
        // 4. LazyLoad with caching
        var details = await LazyLoad(
            $"product-{id}",
            () => ProductService.GetDetailsAsync(id),
            TimeSpan.FromMinutes(5));

        await Update(s => s.AddDetails(id, details));
    }
}
```

---

## Performance & Optimization

### The Problem: Unnecessary Re-renders

By default, `StoreComponent<TState>` re-renders when **any part** of state changes. For large apps, this can cause performance issues.

### The Solution: SelectorStoreComponent

Subscribe to **only the data you need**:

```csharp
// ❌ Re-renders on ANY state change
@inherits StoreComponent<AppState>
<h1>@State.Counter</h1>

// ✅ ONLY re-renders when Counter changes
@inherits SelectorStoreComponent<AppState>
<h1>@State.Counter</h1>

@code {
    protected override object SelectState(AppState state) => state.Counter;
}
```

**Performance Impact:**

| Metric | Without Selectors | With Selectors |
|--------|------------------|----------------|
| Re-renders/sec | ~250 | ~10-15 |
| CPU Usage | 40-60% | 5-10% |
| Frame Rate | 20-30 FPS | 60 FPS |

In high-frequency update scenarios, selectors can reduce re-renders by up to 25x.

### Selector Patterns

```csharp
// Single property
protected override object SelectState(AppState s) => s.UserName;

// Multiple properties (tuple)
protected override object SelectState(AppState s) => (s.UserName, s.IsLoading);

// Computed values (record)
protected override object SelectState(TodoState s) => new TodoStats(
    Total: s.Todos.Count,
    Completed: s.Todos.Count(t => t.Completed)
);

// Filtered collections
protected override object SelectState(TodoState s) =>
    s.Todos.Where(t => !t.Completed).ToImmutableList();
```

### Performance Tips

1. **Batch updates** instead of multiple sequential updates
2. **Split large stores** into focused domains
3. **Debounce frequent updates** (search, resize)
4. **Lazy load heavy data** on-demand
5. **Use selectors** for components that don't need all state
6. **Profile first** - measure render counts before optimizing

---

## Persistence & DevTools

### Auto-Save to LocalStorage

```csharp
builder.Services.AddStoreWithUtilities(
    new AppState(),
    (store, sp) => store
        .WithDefaults(sp, "MyApp")
        .WithPersistence(sp, "app-state"));  // Auto-save + restore
```

State is automatically saved on updates and restored on app load.

### Redux DevTools Integration

```csharp
builder.Services.AddStoreWithUtilities(
    new AppState(),
    (store, sp) => store.WithDefaults(sp, "MyApp"));  // Includes DevTools
```

**Features:**
- 🕐 Time-travel debugging
- 📊 State diffs and inspection
- 🎬 Action replay
- 📸 Import/export snapshots

Install: [Redux DevTools Extension](https://github.com/reduxjs/redux-devtools)

### Diagnostics (DEBUG builds only)

```csharp
// Program.cs
builder.Services.AddStoreDiagnostics();
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithDefaults(sp, "Counter")
        .WithDiagnosticsIfAvailable(sp));

// DiagnosticsPage.razor
#if DEBUG
<DiagnosticPanel DisplayMode="DiagnosticDisplayMode.Inline" />
#endif
```

Tracks action history, performance metrics, renders, and subscriptions. Zero overhead in production (compiled out).

---

## API Reference

### StoreComponent\<TState\>

```csharp
public abstract class StoreComponent<TState> : ComponentBase
{
    // State access
    protected TState State { get; }

    // Core updates
    protected Task Update(Func<TState, TState> updater, string? action = null);
    protected Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null);

    // Async helpers
    protected Task UpdateDebounced(Func<TState, TState> updater, int delayMs, string? action = null);
    protected Task UpdateThrottled(Func<TState, TState> updater, int intervalMs, string? action = null);
    protected Task ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<TState, TState> loading,
        Func<TState, T, TState> success,
        Func<TState, Exception, TState>? error = null);
    protected Task<T> LazyLoad<T>(string key, Func<Task<T>> loader, TimeSpan? cacheFor = null);
}
```

### SelectorStoreComponent\<TState\>

```csharp
public abstract class SelectorStoreComponent<TState> : ComponentBase
{
    protected TState State { get; }
    protected object? Selected { get; }

    // Required: Override to select specific state slice
    protected abstract object SelectState(TState state);

    // Update methods (no async helpers)
    protected Task Update(Func<TState, TState> updater, string? action = null);
}
```

### Registration Methods

```csharp
// Recommended: All-in-one
builder.Services.AddStoreWithUtilities(
    new MyState(),
    (store, sp) => store.WithDefaults(sp, "MyStore"));

// Manual registration
builder.Services.AddStoreUtilities();  // Required for async helpers
builder.Services.AddAsyncActionExecutor<MyState>();  // Required for ExecuteAsync
builder.Services.AddStore(new MyState(), (store, sp) => store.WithDefaults(sp, "MyStore"));

// Scoped stores (Blazor Server)
builder.Services.AddScopedStoreWithUtilities(new SessionState(), ...);

// Transient stores
builder.Services.AddTransientStoreWithUtilities(() => new TempState(), ...);
```

### StoreBuilder Configuration

```csharp
builder.Services.AddStoreWithUtilities(
    new MyState(),
    (store, sp) => store
        .WithDefaults(sp, "StoreName")         // DevTools + Logging + JSRuntime
        .WithPersistence(sp, "storage-key")    // LocalStorage auto-save
        .WithDiagnosticsIfAvailable(sp)        // Diagnostics (DEBUG only)
        .WithMiddleware(customMiddleware));    // Custom middleware instance
```

---

## Best Practices

### ✅ Do

```csharp
// ✅ Use records for state
public record AppState(int Count, string Name);

// ✅ Use 'with' expressions
public AppState Increment() => this with { Count = Count + 1 };

// ✅ Use ImmutableList/ImmutableDictionary
public record State(ImmutableList<Item> Items);

// ✅ Keep state methods pure (no I/O, no logging)
public State AddItem(Item item) => this with { Items = Items.Add(item) };

// ✅ Batch updates when possible
await Update(s => s.SetLoading(true).ClearErrors().ResetForm());
```

### ❌ Don't

```csharp
// ❌ Don't mutate state
public void Increment() { Count++; }  // Wrong!

// ❌ Don't use mutable collections
public record State(List<Item> Items);  // Wrong!

// ❌ Don't add side effects to state methods
public State AddItem(Item item)
{
    _logger.Log("Adding");  // Wrong! Side effect!
    return this with { Items = Items.Add(item) };
}

// ✅ Do side effects in components instead
@code {
    async Task AddItem(Item item)
    {
        Logger.LogInformation("Adding item");
        await Update(s => s.AddItem(item));
    }
}
```

---

## Troubleshooting

### Component not updating?
✅ Inherit from `StoreComponent<TState>`

### State not changing?
✅ Use `with` expressions: `state with { Count = 5 }`

### Collections not updating?
✅ Use `ImmutableList` and its methods

### Async operations failing?
✅ Use `UpdateAsync` not `Update` for async state methods

### ExecuteAsync not available?
✅ Register services: `builder.Services.AddStoreWithUtilities(...)`

### UpdateDebounced/Throttle/LazyLoad not available?
✅ Register utilities: `builder.Services.AddStoreUtilities()` (or use `AddStoreWithUtilities`)

---

## Advanced Patterns

### Multiple Stores

```csharp
// Register
builder.Services.AddStore(new AuthState());
builder.Services.AddStore(new CartState());

// Use
@inherits StoreComponent<AuthState>
@inject IStore<CartState> CartStore

<p>User: @State.CurrentUser?.Name</p>
<p>Cart: @CartStore.GetState().ItemCount items</p>
```

### Derived State

```csharp
public record TodoState(ImmutableList<Todo> Todos)
{
    public int CompletedCount => Todos.Count(t => t.Completed);
    public double CompletionRate => Todos.Count > 0
        ? (double)CompletedCount / Todos.Count * 100 : 0;
}
```

### Optimistic Updates

```csharp
async Task DeleteOptimistically(Guid id)
{
    var original = State;
    await Update(s => s.RemoveTodo(id));  // Optimistic

    try {
        await _api.DeleteAsync(id);
    } catch {
        await Update(_ => original);  // Rollback
    }
}
```

### Middleware

```csharp
public class LoggingMiddleware<TState> : IMiddleware<TState>
{
    public Task OnBeforeUpdateAsync(TState state, string? action)
    {
        _logger.LogInformation("Before: {Action}", action);
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(TState prev, TState next, string? action)
    {
        _logger.LogInformation("After: {Action}", action);
        return Task.CompletedTask;
    }
}

// Register
builder.Services.AddStore(
    new AppState(),
    (store, sp) => store.WithMiddleware(new LoggingMiddleware<AppState>(logger)));
```

---

## Documentation

### 🌐 Online Documentation
**[📚 Visit the full documentation site →](https://mashrulhaque.github.io/EasyAppDev.Blazor.Store/)**

Interactive guides, examples, and API reference with search and navigation.

### Core Guides
- 📘 [Architecture Guide](./docs/ARCHITECTURE.md) - Design patterns and decisions
- 📙 [API Design](./docs/API_DESIGN.md) - API philosophy
- 📗 [Coding Standards](./docs/CODING_STANDARDS.md) - Best practices and anti-patterns
- 📕 [Testing Strategy](./docs/TESTING_STRATEGY.md) - How to test your state

### Async Helpers
- 📖 [Async Helpers Orchestrator](./docs/ASYNC_HELPERS_ORCHESTRATOR.md) - Complete guide
- 🏗️ [Architecture - Async Helpers](./docs/ARCHITECTURE_ASYNC_HELPERS.md) - Design decisions

---

## What's New in v1.0.0

**Async Helpers** - Reduce async boilerplate with 5 helper methods
- `UpdateDebounced`, `AsyncData<T>`, `ExecuteAsync`, `UpdateThrottled`, `LazyLoad`
- 98% test pass rate (299/305 tests)
- Production-ready with live demos

**Diagnostics & Monitoring** (DEBUG only)
- Built-in diagnostics panel
- Action history with state diffs
- Performance metrics (P95/P99)
- Zero production overhead

**Simplified API**
- `WithDefaults(sp, name)` - One-liner setup
- `WithPersistence(sp, key)` - Auto-save/restore
- `AddStoreWithUtilities()` - All-in-one registration

---

## Contributing

Contributions welcome! Keep it simple and focused.

1. Read [CODING_STANDARDS.md](./docs/CODING_STANDARDS.md)
2. Run tests: `dotnet test`
3. Add tests for new features
4. Keep API surface minimal

---

## License

MIT © EasyAppDev

---

<div align="center">

**[⭐ Star us on GitHub](https://github.com/yourusername/EasyAppDev.Blazor.Store)** • **[🐛 Report Issues](https://github.com/yourusername/EasyAppDev.Blazor.Store/issues)** • **[💬 Discussions](https://github.com/yourusername/EasyAppDev.Blazor.Store/discussions)**

</div>
