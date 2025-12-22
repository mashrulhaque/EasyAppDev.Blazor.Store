# EasyAppDev.Blazor.Store

**Type-safe state management for Blazor using C# records.**

*Inspired by Zustand • Built for C# developers*

[![NuGet](https://img.shields.io/nuget/v/EasyAppDev.Blazor.Store.svg)](https://www.nuget.org/packages/EasyAppDev.Blazor.Store/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<div align="center">
  <a href="https://mashrulhaque.github.io/EasyAppDev.Blazor.Store/">
    <img src="https://img.shields.io/badge/📚_Full_Documentation-4A90E2?style=for-the-badge" alt="Documentation" />
  </a>
</div>

> **Upgrading from v1.x?** See [Breaking Changes in v2.0.0](#breaking-changes-in-v200) for migration guide.

### Supported Platforms

| .NET Version | Status |
|--------------|--------|
| .NET 8.0 | ✅ Fully Supported |
| .NET 9.0 | ✅ Fully Supported |
| .NET 10.0 | ✅ Fully Supported |

---

## Why This Library?

No actions. No reducers. No dispatchers. Just C# records with pure methods.

```csharp
// Define state as a record with transformation methods
public record CounterState(int Count)
{
    public CounterState Increment() => this with { Count = Count + 1 };
    public CounterState Decrement() => this with { Count = Count - 1 };
}

// Use in components
@inherits StoreComponent<CounterState>

<h1>@State.Count</h1>
<button @onclick="@(() => UpdateAsync(s => s.Increment()))">+</button>
```

**What you get:**
- Zero boilerplate state management
- Immutable by default (C# records + `with` expressions)
- Automatic component updates
- Redux DevTools integration (DEBUG builds only)
- Full async support with helpers
- Works with Server, WebAssembly, and Auto modes

---

## Quick Start

### 1. Install

```bash
dotnet add package EasyAppDev.Blazor.Store
```

### 2. Register

```csharp
// Program.cs
builder.Services.AddStoreWithUtilities(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter"));
```

### 3. Use

```razor
@page "/counter"
@inherits StoreComponent<CounterState>

<h1>@State.Count</h1>
<button @onclick="@(() => UpdateAsync(s => s.Increment()))">+</button>
<button @onclick="@(() => UpdateAsync(s => s.Decrement()))">-</button>
```

That's it. State updates automatically propagate to all subscribed components.

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Registration Options](#registration-options)
3. [Async Helpers](#async-helpers)
4. [Optimistic Updates](#optimistic-updates)
5. [Undo/Redo History](#undoredo-history)
6. [Query System](#query-system)
7. [Cross-Tab Sync](#cross-tab-sync)
8. [Server Sync (SignalR)](#server-sync-signalr)
9. [Immer-Style Updates](#immer-style-updates)
10. [Redux-Style Actions](#redux-style-actions)
11. [Plugin System](#plugin-system)
12. [Security](#security)
13. [Selectors & Performance](#selectors--performance)
14. [Persistence & DevTools](#persistence--devtools)
15. [Middleware](#middleware)
16. [Blazor Render Modes](#blazor-render-modes)
17. [API Reference](#api-reference)
18. [Breaking Changes in v2.0.0](#breaking-changes-in-v200)

---

## Core Concepts

### State = Immutable Record

```csharp
public record TodoState(ImmutableList<Todo> Todos)
{
    public static TodoState Initial => new(ImmutableList<Todo>.Empty);

    // Pure transformation methods - no side effects
    public TodoState AddTodo(string text) =>
        this with { Todos = Todos.Add(new Todo(Guid.NewGuid(), text, false)) };

    public TodoState ToggleTodo(Guid id) =>
        this with {
            Todos = Todos.Select(t =>
                t.Id == id ? t with { Completed = !t.Completed } : t
            ).ToImmutableList()
        };

    public TodoState RemoveTodo(Guid id) =>
        this with { Todos = Todos.RemoveAll(t => t.Id == id) };

    // Computed properties
    public int CompletedCount => Todos.Count(t => t.Completed);
}
```

### Component = StoreComponent\<T\>

```razor
@page "/todos"
@inherits StoreComponent<TodoState>

<input @bind="newTodo" @onkeyup="HandleKeyUp" />

@foreach (var todo in State.Todos)
{
    <div>
        <input type="checkbox" checked="@todo.Completed"
               @onchange="@(() => UpdateAsync(s => s.ToggleTodo(todo.Id)))" />
        @todo.Text
        <button @onclick="@(() => UpdateAsync(s => s.RemoveTodo(todo.Id)))">X</button>
    </div>
}

<p>Completed: @State.CompletedCount / @State.Todos.Count</p>

@code {
    string newTodo = "";

    async Task HandleKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(newTodo))
        {
            await UpdateAsync(s => s.AddTodo(newTodo));
            newTodo = "";
        }
    }
}
```

### Interface Segregation

The `IStore<T>` interface composes three focused interfaces:

```csharp
// Read-only state access
public interface IStateReader<TState> where TState : notnull
{
    TState GetState();
}

// State update operations
public interface IStateWriter<TState> where TState : notnull
{
    Task UpdateAsync(Func<TState, TState> updater, string? action = null);
    Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null);
}

// Subscription management
public interface IStateObservable<TState> where TState : notnull
{
    IDisposable Subscribe(Action<TState> callback);
    IDisposable Subscribe<TSelected>(Func<TState, TSelected> selector, Action<TSelected> callback);
}

// Full store interface
public interface IStore<TState> :
    IStateReader<TState>,
    IStateWriter<TState>,
    IStateObservable<TState>,
    IDisposable
    where TState : notnull
{
}
```

---

## Registration Options

### AddSecureStore (Recommended)

Automatic security configuration based on environment:

```csharp
// Simplest secure registration
builder.Services.AddSecureStore(
    new AppState(),
    "App",
    opts =>
    {
        opts.PersistenceKey = "app-state";   // LocalStorage
        opts.EnableTabSync = true;            // Cross-tab sync
        opts.EnableHistory = true;            // Undo/redo
    });
```

**Security profiles applied automatically:**
- `Development`: DevTools enabled, permissive validation
- `Production`: No DevTools, message signing enabled, validation warnings
- `Strict`: All Production features + throws on any security warning

### AddStoreWithUtilities

Standard registration with all utilities:

```csharp
builder.Services.AddStoreWithUtilities(
    TodoState.Initial,
    (store, sp) => store
        .WithDefaults(sp, "Todos")           // DevTools + Logging
        .WithPersistence(sp, "todos"));      // LocalStorage
```

### AddScopedStoreWithUtilities

Scoped store for Blazor Server per-user isolation:

```csharp
builder.Services.AddScopedStoreWithUtilities(
    new UserSessionState(),
    (store, sp) => store.WithDefaults(sp, "Session"));
```

### AddStore / AddScopedStore

Minimal registration without utilities:

```csharp
// Singleton
builder.Services.AddStore(new CounterState(0));

// Scoped
builder.Services.AddScopedStore(new CounterState(0));

// With factory
builder.Services.AddStore(
    sp => new AppState(sp.GetRequiredService<IConfiguration>()),
    (store, sp) => store.WithLogging());
```

### AddStoreWithHistory

Store with undo/redo support:

```csharp
builder.Services.AddStoreWithHistory(
    new EditorState(),
    opts => opts
        .WithMaxSize(100)
        .WithMaxMemoryMB(50)
        .ExcludeActions("CURSOR_MOVE", "SELECTION"),
    (store, sp) => store.WithDefaults(sp, "Editor")
);
```

---

## Async Helpers

Five built-in helpers eliminate async boilerplate:

### 1. AsyncData\<T\> - Loading States

```csharp
// State
public record UserState(AsyncData<User> CurrentUser)
{
    public static UserState Initial => new(AsyncData<User>.NotAsked());
}

// Component
@if (State.CurrentUser.IsLoading) { <Spinner /> }
@if (State.CurrentUser.HasData) { <p>Welcome, @State.CurrentUser.Data.Name</p> }
@if (State.CurrentUser.HasError) { <p class="error">@State.CurrentUser.Error</p> }
```

### 2. ExecuteAsync - Automatic Error Handling

```csharp
async Task LoadUser() => await ExecuteAsync(
    () => UserService.GetCurrentUserAsync(),
    loading: s => s with { CurrentUser = s.CurrentUser.ToLoading() },
    success: (s, user) => s with { CurrentUser = AsyncData<User>.Success(user) },
    error: (s, ex) => s with { CurrentUser = AsyncData<User>.Failure(ex.Message) }
);
```

### 3. UpdateDebounced - Debounced Updates

```csharp
// Search input with 300ms debounce
<input @oninput="@(e => UpdateDebounced(
    s => s.SetSearchQuery(e.Value?.ToString() ?? ""),
    300))" />
```

### 4. UpdateThrottled - Throttled Updates

```csharp
// Mouse tracking throttled to 100ms intervals
<div @onmousemove="@(e => UpdateThrottled(
    s => s.SetPosition(e.ClientX, e.ClientY),
    100))">
    Track mouse here
</div>
```

### 5. LazyLoad - Cached Data Loading

```csharp
// Automatic caching with deduplication
async Task LoadUserDetails(int userId)
{
    var user = await LazyLoad(
        $"user-{userId}",
        () => UserService.GetUserAsync(userId),
        cacheFor: TimeSpan.FromMinutes(5));

    await UpdateAsync(s => s.SetSelectedUser(user));
}
```

---

## Optimistic Updates

Update UI immediately, rollback on server error:

### Basic Usage

```csharp
// Instant UI update with automatic rollback on failure
await store.UpdateOptimistic(
    s => s.RemoveItem(itemId),                    // Optimistic: remove immediately
    async s => await api.DeleteItemAsync(itemId), // Server: actual delete
    (s, error) => s.RestoreItem(itemId)           // Error: rollback
);
```

### With Server Response

```csharp
// Use server response to update state
await store.UpdateOptimistic<AppState, ServerItem>(
    s => s.AddPendingItem(item),                  // Show pending state
    async s => await api.CreateItemAsync(item),   // Server creates with ID
    (s, result) => s.ConfirmItem(result),         // Update with server data
    (s, error) => s.RemovePendingItem(item)       // Remove on failure
);
```

### Two-Phase with Confirmation

```csharp
await store.UpdateOptimisticWithConfirm(
    s => s.SetPending(true),
    async s => await api.Process(),
    (s, result) => s.Confirm(result)
);
```

---

## Undo/Redo History

Full history stack for editor-like experiences:

### Setup

```csharp
// Program.cs
builder.Services.AddStoreWithHistory(
    new EditorState(),
    opts => opts
        .WithMaxSize(100)                              // Max entries
        .WithMaxMemoryMB(50)                           // Memory limit
        .ExcludeActions("CURSOR_MOVE", "SELECTION")    // Don't track these
        .GroupActions(TimeSpan.FromMilliseconds(300)), // Group rapid edits
    (store, sp) => store.WithDefaults(sp, "Editor")
);
```

### Usage

```razor
@inherits StoreComponent<EditorState>
@inject IStoreHistory<EditorState> History

<button @onclick="@(() => History.UndoAsync())" disabled="@(!History.CanUndo)">
    Undo
</button>
<button @onclick="@(() => History.RedoAsync())" disabled="@(!History.CanRedo)">
    Redo
</button>
<span>@History.CurrentIndex / @History.Count</span>

@code {
    // Jump to specific point
    async Task GoTo(int index) => await History.GoToAsync(index);
}
```

---

## Query System

TanStack Query-inspired data fetching with caching:

### Setup

```csharp
builder.Services.AddQueryClient();
```

### Queries

```csharp
@inject IQueryClient QueryClient

@code {
    private IQuery<User> userQuery = null!;

    protected override void OnInitialized()
    {
        userQuery = QueryClient.CreateQuery<User>(
            "user-123",                                    // Cache key
            async ct => await api.GetUserAsync(123, ct),   // Fetch function
            opts => opts
                .WithStaleTime(TimeSpan.FromMinutes(5))    // Fresh for 5 min
                .WithCacheTime(TimeSpan.FromHours(1))      // Cache for 1 hour
                .WithRetry(3)                              // Retry 3 times
        );
    }
}

@if (userQuery.IsLoading) { <Spinner /> }
@if (userQuery.IsError) { <Error Message="@userQuery.Error" /> }
@if (userQuery.IsSuccess) { <UserCard User="@userQuery.Data" /> }
```

### Mutations

```csharp
@code {
    private IMutation<UpdateUserRequest, User> mutation = null!;

    protected override void OnInitialized()
    {
        mutation = QueryClient.CreateMutation<UpdateUserRequest, User>(
            async (req, ct) => await api.UpdateUserAsync(req, ct),
            opts => opts.OnSuccess((_, _) =>
                QueryClient.InvalidateQueries("user-*"))  // Invalidate cache
        );
    }

    async Task Save()
    {
        await mutation.MutateAsync(new UpdateUserRequest { Name = "John" });
    }
}
```

---

## Cross-Tab Sync

Sync state across browser tabs in real-time:

### Setup

```csharp
builder.Services.AddStore(
    new CartState(),
    (store, sp) => store
        .WithDefaults(sp, "Cart")
        .WithTabSync(sp, opts => opts
            .Channel("shopping-cart")
            .EnableMessageSigning                       // HMAC security
            .DeriveKeyFromOrigin                        // Same-origin key derivation
            .RequireValidSignature = true
            .MaxMessageAgeSeconds = 30                  // Replay attack prevention
            .ExcludeActions("HOVER", "FOCUS"))          // Don't sync these
);
```

### How It Works

```
Tab 1: User adds item to cart
    ↓
Store updates → TabSyncMiddleware broadcasts
    ↓
Tab 2: Receives update → Store syncs → UI updates
    ↓
Both tabs show same cart!
```

No additional code needed in components. Sync happens automatically.

### Security Options

| Option | Description |
|--------|-------------|
| `EnableMessageSigning` | Enable HMAC-SHA256 message signing |
| `DeriveKeyFromOrigin` | Auto-derive key from window.location.origin |
| `SigningKey` | Explicit shared signing key |
| `RequireValidSignature` | Reject unsigned messages (default: true) |
| `MaxMessageAgeSeconds` | Prevent replay attacks (default: 30) |
| `MaxMessageSizeBytes` | Prevent DoS attacks (default: 1MB) |
| `MaxJsonDepth` | Prevent stack overflow (default: 32) |
| `FailFastOnInsecureConfiguration` | Throw on misconfiguration |

---

## Server Sync (SignalR)

Real-time collaboration with presence and cursors:

### Setup

```csharp
builder.Services.AddStore(
    new DocumentState(),
    (store, sp) => store
        .WithDefaults(sp, "Document")
        .WithServerSync(sp, opts => opts
            .HubUrl("/hubs/documents")
            .DocumentId(documentId)
            .EnablePresence()                            // Who's online
            .EnableCursorTracking()                      // Live cursors
            .ConflictResolution(ConflictResolution.LastWriteWins)
            .OnUserJoined(user => Console.WriteLine($"{user} joined"))
            .OnCursorUpdated((userId, pos) => RenderCursor(userId, pos)))
);
```

### Usage

```csharp
@inject IServerSync<DocumentState> ServerSync

@code {
    protected override async Task OnInitializedAsync()
    {
        // Set your presence
        await ServerSync.UpdatePresenceAsync(new PresenceData
        {
            DisplayName = currentUser.Name,
            Color = "#ff0000"
        });
    }

    // Track cursor position
    async Task OnMouseMove(MouseEventArgs e)
    {
        await ServerSync.UpdateCursorAsync(new CursorPosition
        {
            X = e.ClientX,
            Y = e.ClientY
        });
    }
}
```

### Conflict Resolution

| Mode | Behavior |
|------|----------|
| `ClientWins` | Local changes always win |
| `ServerWins` | Server changes always win |
| `LastWriteWins` | Most recent timestamp wins |
| `Custom` | Your custom resolver |

---

## Immer-Style Updates

Clean syntax for complex nested updates:

### The Problem

```csharp
// Verbose nested updates
await store.UpdateAsync(s => s with {
    User = s.User with {
        Profile = s.User.Profile with {
            Address = s.User.Profile.Address with { City = "NYC" }
        }
    },
    Items = s.Items.Add(newItem)
});
```

### The Solution

```csharp
// Clean, readable updates
await store.ProduceAsync(draft => draft
    .Set(s => s.User.Profile.Address.City, "NYC")
    .Append(s => s.Items, newItem));
```

### Available Operations

```csharp
await store.ProduceAsync(draft => draft
    // Properties
    .Set(s => s.Name, "John")                    // Set value
    .Update(s => s.Count, c => c + 1)            // Transform
    .SetNull<string?>(s => s.Optional)           // Set to null

    // Numbers
    .Increment(s => s.Count)                     // count++
    .Decrement(s => s.Count)                     // count--
    .Increment(s => s.Count, 5)                  // count += 5

    // Booleans
    .Toggle(s => s.IsActive)                     // !isActive

    // Strings
    .Concat(s => s.Name, " Jr.")                 // Append

    // Lists (ImmutableList)
    .Append(s => s.Items, item)                  // Add to end
    .Prepend(s => s.Items, item)                 // Add to start
    .Remove(s => s.Items, item)                  // Remove item
    .SetAt(s => s.Items, 0, item)                // Replace at index
    .RemoveAt(s => s.Items, 0)                   // Remove at index

    // Dictionaries (ImmutableDictionary)
    .DictSet(s => s.Map, "key", value)           // Add/update
    .DictRemove(s => s.Map, "key")               // Remove
);
```

---

## Redux-Style Actions

Type-safe action dispatching for Redux-familiar teams:

### Define Actions

```csharp
public record Increment : IAction<CounterState>;
public record IncrementBy(int Amount) : IAction<CounterState>;
public record Reset : IAction<CounterState>;
```

### Dispatch

```csharp
// Simple dispatch
await store.DispatchAsync<CounterState, Increment>(
    new Increment(),
    (state, _) => state with { Count = state.Count + 1 }
);

// With payload
await store.DispatchAsync(
    new IncrementBy(5),
    (state, action) => state with { Count = state.Count + action.Amount }
);

// Pattern matching
await store.DispatchAsync(action, (state, a) => a switch
{
    Increment => state with { Count = state.Count + 1 },
    IncrementBy i => state with { Count = state.Count + i.Amount },
    Reset => state with { Count = 0 },
    _ => state
});
```

---

## Plugin System

Extensible hooks for cross-cutting concerns:

### Create a Plugin

```csharp
public class AnalyticsPlugin : StorePluginBase<AppState>
{
    private readonly IAnalytics _analytics;

    public AnalyticsPlugin(IAnalytics analytics) => _analytics = analytics;

    public override Task OnAfterUpdateAsync(AppState prev, AppState next, string action)
    {
        _analytics.Track(action, new { prev.Count, next.Count });
        return Task.CompletedTask;
    }
}
```

### Register Plugins

```csharp
// Individual plugin
builder.Services.AddStore(
    new AppState(),
    (store, sp) => store
        .WithPlugin<AppState, AnalyticsPlugin>(sp)
        .WithPlugin<AppState, ValidationPlugin>(sp)
);

// Auto-discover from assembly
builder.Services.AddStore(
    new AppState(),
    (store, sp) => store.WithPlugins(typeof(Program).Assembly, sp)
);
```

### Plugin Hooks

```csharp
public class MyPlugin : StorePluginBase<AppState>
{
    public override Task OnStoreCreatedAsync() { /* Store initialized */ }
    public override Task OnBeforeUpdateAsync(AppState state, string action) { /* Pre-update */ }
    public override Task OnAfterUpdateAsync(AppState prev, AppState next, string action) { /* Post-update */ }
    public override Task OnStoreDisposingAsync() { /* Cleanup */ }
    public override IMiddleware<AppState>? GetMiddleware() => null; // Custom middleware
}
```

---

## Security

### Security Profiles

| Profile | DevTools | Validation | Message Signing | Use Case |
|---------|----------|------------|-----------------|----------|
| `Development` | Enabled (DEBUG) | Optional | Optional | Local development |
| `Production` | Disabled | Warnings | Required | Deployed apps |
| `Strict` | Disabled | Required | Required | High-security apps |
| `Custom` | Manual | Manual | Manual | Fine-grained control |

### AddSecureStore Configuration

```csharp
builder.Services.AddSecureStore(
    new AppState(),
    "App",
    opts =>
    {
        opts.Profile = SecurityProfile.Production;    // Security profile
        opts.PersistenceKey = "app-state";            // LocalStorage key
        opts.EnableTabSync = true;                    // Cross-tab sync
        opts.EnableHistory = true;                    // Undo/redo
        opts.MaxHistoryEntries = 50;                  // History limit
        opts.MaxHistoryMemoryMB = 10;                 // Memory limit
        opts.UseScoped = true;                        // Scoped registration
        opts.RequireValidator = true;                 // Require state validator
        opts.ThrowOnSecurityWarnings = true;          // Fail-fast on warnings
        opts.FilterSensitiveData = true;              // Filter [SensitiveData]
    });
```

### State Validation

```csharp
// Register validator
builder.Services.AddStateValidator<AppState>(state =>
{
    var errors = new List<string>();

    if (state.UserId < 0)
        errors.Add("UserId cannot be negative");

    if (state.Items?.Count > 1000)
        errors.Add("Items exceeds maximum size");

    return errors;
});

// Or use a validator class
public class AppStateValidator : IStateValidator<AppState>
{
    public StateValidationResult Validate(AppState state)
    {
        var errors = new List<string>();
        // Validation logic...
        return errors.Count > 0
            ? StateValidationResult.Failure(errors)
            : StateValidationResult.Success();
    }
}

builder.Services.AddStateValidator<AppState, AppStateValidator>();
```

### Sensitive Data Filtering

```csharp
public record UserState(
    string Username,
    [property: SensitiveData] string Password,
    [property: SensitiveData] string AuthToken,
    [property: SensitiveData(Reason = "PII")] string SocialSecurityNumber
);

// In DevTools: { Username: "John", Password: "[REDACTED]", ... }
```

### Never Persist Secrets

Use `TransformOnSave` to exclude sensitive fields from localStorage:

```csharp
.WithPersistence(sp, new PersistenceOptions<UserState>
{
    Key = "user-state",
    TransformOnSave = state => state with
    {
        Password = null,
        AuthToken = null,
        ApiKey = null
    }
})
```

### Security Gotchas

| Mistake | Solution |
|---------|----------|
| DevTools in production | Use `#if DEBUG` or `AddSecureStore` |
| Secrets in localStorage | Use `TransformOnSave` to exclude |
| Missing state validation | Register `IStateValidator<T>` |
| TabSync without signing | Enable `EnableMessageSigning` |
| No history memory limit | Set `WithMaxMemoryMB()` |
| Client-side trust | Always validate on server |

---

## Selectors & Performance

### The Problem

`StoreComponent<T>` re-renders on **any** state change. For large apps, use selectors:

### SelectorStoreComponent

```csharp
// Only re-renders when Count changes
@inherits SelectorStoreComponent<AppState, int>

<h1>@State</h1>

@code {
    protected override int SelectState(AppState state) => state.Count;
}
```

### Selector Patterns

```csharp
// Single value
protected override int SelectState(AppState s) => s.Count;

// Multiple values (tuple)
protected override (string, bool) SelectState(AppState s) =>
    (s.UserName, s.IsLoading);

// Computed value
protected override int SelectState(TodoState s) =>
    s.Todos.Count(t => t.Completed);

// Filtered list
protected override ImmutableList<Todo> SelectState(TodoState s) =>
    s.Todos.Where(t => !t.Completed).ToImmutableList();
```

### Performance Impact

| Metric | StoreComponent | SelectorStoreComponent |
|--------|----------------|------------------------|
| Re-renders | Every change | Only selected changes |
| Typical reduction | - | 90%+ fewer renders |

---

## Persistence & DevTools

### LocalStorage Persistence

```csharp
builder.Services.AddScopedStore(
    new AppState(),
    (store, sp) => store
        .WithDefaults(sp, "App")
        .WithPersistence(sp, "app-state"));  // Auto-save & restore
```

### Redux DevTools

Included with `WithDefaults()` in DEBUG builds. Features:
- Time-travel debugging
- State inspection
- Action replay
- Import/export

Install: [Redux DevTools Extension](https://github.com/reduxjs/redux-devtools)

### Diagnostics (DEBUG only)

```csharp
#if DEBUG
builder.Services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
builder.Services.AddStore(state, (store, sp) => store.WithDiagnostics(sp));
#endif

// Query in components
@inject IDiagnosticsService Diagnostics

var actions = Diagnostics.GetRecentActions<AppState>(10);
var metrics = Diagnostics.GetPerformanceMetrics<AppState>();
```

---

## Middleware

### Custom Middleware

```csharp
public class LoggingMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    public Task OnBeforeUpdateAsync(TState state, string? action)
    {
        Console.WriteLine($"Before: {action}");
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(TState prev, TState next, string? action)
    {
        Console.WriteLine($"After: {action}");
        return Task.CompletedTask;
    }
}

// Register
.WithMiddleware(new LoggingMiddleware<AppState>())
```

### Functional Middleware

```csharp
.WithMiddleware(FunctionalMiddleware.Create<AppState>(
    onBefore: (state, action) => Console.WriteLine($"Before: {action}"),
    onAfter: (prev, next, action) => Console.WriteLine($"After: {action}")
))
```

### Built-in Middleware

| Middleware | Purpose |
|------------|---------|
| DevToolsMiddleware | Redux DevTools (DEBUG only) |
| PersistenceMiddleware | LocalStorage |
| LoggingMiddleware | Console logging |
| HistoryMiddleware | Undo/redo |
| TabSyncMiddleware | Cross-tab sync |
| ServerSyncMiddleware | SignalR sync |
| PluginMiddleware | Plugin lifecycle |
| DiagnosticsMiddleware | Performance (DEBUG) |

---

## Blazor Render Modes

Works with all modes - registration method determines feature availability:

| Feature | WebAssembly | Server (Singleton) | Server (Scoped) | Auto |
|---------|-------------|-------------------|-----------------|------|
| Core Store | ✅ | ✅ | ✅ | ✅ |
| Async Helpers | ✅ | ✅ | ✅ | ✅ |
| DevTools | ✅ | ❌ | ✅ | ✅ |
| Persistence | ✅ | ❌ | ✅ | ✅ |
| TabSync | ✅ | ❌ | ✅ | ✅ |
| History | ✅ | ✅ | ✅ | ✅ |
| Query | ✅ | ✅ | ✅ | ✅ |
| Plugins | ✅ | ✅ | ✅ | ✅ |

### Blazor Server with JS Features

Use scoped stores for DevTools, persistence, and TabSync:

```csharp
// Scoped = per-user + full JS features
builder.Services.AddScopedStoreWithUtilities(
    new UserState(),
    (store, sp) => store.WithDefaults(sp, "User"));
```

---

## API Reference

### StoreComponent\<T\>

```csharp
protected TState State { get; }

// Updates
protected Task UpdateAsync(Func<TState, TState> updater, string? action = null);

// Async helpers (requires AddStoreWithUtilities)
protected Task UpdateDebounced(Func<TState, TState> updater, int delayMs);
protected Task UpdateThrottled(Func<TState, TState> updater, int intervalMs);
protected Task ExecuteAsync<T>(Func<Task<T>> action, ...);
protected Task<T> LazyLoad<T>(string key, Func<Task<T>> loader, TimeSpan? cacheFor);
```

### Registration

```csharp
// Secure (recommended)
builder.Services.AddSecureStore(state, "Name", opts => ...);

// With utilities
builder.Services.AddStoreWithUtilities(state, configure);
builder.Services.AddScopedStoreWithUtilities(state, configure);

// Basic
builder.Services.AddStore(state, configure);
builder.Services.AddScopedStore(state, configure);
builder.Services.AddTransientStore(stateFactory, configure);

// Special
builder.Services.AddQueryClient();
builder.Services.AddStoreWithHistory(state, historyOpts, configure);
builder.Services.AddStoreHistory<TState>(history);

// Security
builder.Services.AddStateValidator<TState, TValidator>();
builder.Services.AddStateValidator<TState>(validateFunc);
builder.Services.AddStateValidatorsFromAssembly(assembly);
builder.Services.AddSecurityAuditLogger(opts => ...);
```

### StoreBuilder

```csharp
store
    // Core
    .WithDefaults(sp, "Name")              // DevTools + Logging (DEBUG)
    .WithLogging()                         // Logging only
    .WithMiddleware(middleware)            // Custom middleware
    .WithStateValidator(validator)         // State validation
    .WithSecurityProfile(sp, profile)      // Security profile
    .WithEnvironmentDefaults(sp)           // Auto-detect profile

    // Features
    .WithPersistence(sp, "key")            // LocalStorage
    .WithHistory(opts => ...)              // Undo/redo
    .WithTabSync(sp, opts => ...)          // Cross-tab
    .WithServerSync(sp, opts => ...)       // SignalR
    .WithPlugin<TState, TPlugin>(sp)       // Plugin
    .WithPlugins(assembly, sp)             // Auto-discover plugins
    .WithDiagnostics(sp)                   // DEBUG only
```

---

## Breaking Changes in v2.0.0

### Middleware Interface

The `IMiddleware<TState>` interface now receives both previous and new state:

```csharp
// Before (v1.x)
Task OnAfterUpdateAsync(TState newState, string? action);

// After (v2.0)
Task OnAfterUpdateAsync(TState previousState, TState newState, string? action);
```

**Migration:** Update your middleware implementations to accept the additional `previousState` parameter.

### Optimistic Updates

Optimistic updates now use dedicated extension methods:

```csharp
// Before (v1.x) - Manual rollback pattern
var original = store.GetState();
await store.UpdateAsync(s => s.RemoveItem(id));
try { await api.DeleteAsync(id); }
catch { await store.UpdateAsync(_ => original); throw; }

// After (v2.0) - Built-in support
await store.UpdateOptimistic(
    s => s.RemoveItem(id),
    async _ => await api.DeleteAsync(id),
    (s, error) => s.RestoreItem(id)
);
```

### Plugin System

Plugin hooks now receive both previous and new state:

```csharp
// Before (v1.x)
public override Task OnAfterUpdateAsync(AppState newState, string action);

// After (v2.0)
public override Task OnAfterUpdateAsync(AppState previousState, AppState newState, string action);
```

### New Features (Non-Breaking)

- **Query System**: TanStack Query-inspired data fetching with `IQueryClient`
- **Immer-Style Updates**: Clean syntax with `ProduceAsync()` and draft operations
- **Undo/Redo History**: Full history stack with `IStoreHistory<T>`
- **Cross-Tab Sync**: Real-time sync with `WithTabSync()`
- **Server Sync**: SignalR collaboration with `WithServerSync()`
- **Security Profiles**: `AddSecureStore()` with automatic configuration
- **State Validation**: `IStateValidator<T>` for external state
- **Sensitive Data**: `[SensitiveData]` attribute for filtering

---

## Common Gotchas

1. **Always use `with`**: `state with { X = 1 }` not `state.X = 1`
2. **Use ImmutableList**: `Todos.Add(item)` returns new list
3. **State methods are pure**: No logging, no API calls
4. **Use UpdateAsync**: Synchronous `Update()` is obsolete
5. **Register utilities**: Call `AddStoreWithUtilities()` for async helpers
6. **Blazor Server**: Use `AddScopedStore` for DevTools/Persistence/TabSync
7. **Security**: Use `AddSecureStore` for production deployments
8. **Validation**: Implement `IStateValidator<T>` for persistence/sync
9. **History limits**: Set `WithMaxMemoryMB()` for large state objects

---

## Documentation

- [Full Documentation](https://mashrulhaque.github.io/EasyAppDev.Blazor.Store/)

---

## License

MIT © EasyAppDev

---

<div align="center">

**[GitHub](https://github.com/mashrulhaque/EasyAppDev.Blazor.Store)** • **[Issues](https://github.com/mashrulhaque/EasyAppDev.Blazor.Store/issues)** • **[Discussions](https://github.com/mashrulhaque/EasyAppDev.Blazor.Store/discussions)**

</div>
