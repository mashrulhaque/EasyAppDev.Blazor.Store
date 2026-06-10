# EasyAppDev.Blazor.Store - State Management for Blazor

**A Zustand-inspired state management library for Blazor WebAssembly, Blazor Server, and MAUI Hybrid.** Zero boilerplate. Built-in data fetching with caching. Cross-tab sync. Real-time collaboration via SignalR. Undo/redo history. Redux DevTools support. All type-safe with C# records.

[![NuGet version for EasyAppDev.Blazor.Store state management library](https://img.shields.io/nuget/v/EasyAppDev.Blazor.Store.svg)](https://www.nuget.org/packages/EasyAppDev.Blazor.Store/)
[![License: MIT - Free to use for Blazor state management](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<div align="center">
  <a href="https://mashrulhaque.github.io/EasyAppDev.Blazor.Store/">
    <img src="https://img.shields.io/badge/📚_Full_Documentation-4A90E2?style=for-the-badge" alt="Full documentation for EasyAppDev.Blazor.Store - Blazor state management library" />
  </a>
  <a href="http://blazorstore.easyappdev.com/">
    <img src="https://img.shields.io/badge/🚀_Live_Demo-28A745?style=for-the-badge" alt="Live demo of Blazor state management with cross-tab sync and undo-redo" />
  </a>
</div>

```csharp
// Define state as a C# record
public record CounterState(int Count)
{
    public CounterState Increment() => this with { Count = Count + 1 };
}
```

```razor
@inherits StoreComponent<CounterState>

<h1>@State.Count</h1>
<button @onclick="@(() => UpdateAsync(s => s.Increment()))">+</button>
```

That's it. No actions, no reducers, no dispatchers. State updates propagate to all subscribers automatically.

> **Upgrading from v1.x?** See [Breaking Changes in v2.0.0](#breaking-changes-in-v200) for migration guide.

---

## Why This Library?

| Problem | Solution |
|---------|----------|
| Boilerplate everywhere (actions, reducers, dispatchers) | State = C# record with methods |
| 10 components fetch same data = 10 API calls | Request deduplication - one fetch, shared result |
| Tab 1 updates cart, Tab 2 shows stale data | Cross-tab sync via BroadcastChannel |
| No undo for user mistakes | Built-in undo/redo with memory limits |
| Loading/error state spaghetti | `AsyncData<T>` replaces boolean flags |
| Optimistic UI rollback is painful | Built-in rollback on server error |

**.NET 8, 9, 10** · Blazor Server, WebAssembly, Auto, MAUI Hybrid

---

## Quick Start

### Installation

**.NET CLI**
```bash
dotnet add package EasyAppDev.Blazor.Store
```

**Package Manager Console**
```powershell
Install-Package EasyAppDev.Blazor.Store
```

**PackageReference** (add to your .csproj)
```xml
<PackageReference Include="EasyAppDev.Blazor.Store" Version="2.0.*" />
```

### Setup

```csharp
// Program.cs
builder.Services.AddStoreWithUtilities(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter"));
```

```razor
@page "/counter"
@inherits StoreComponent<CounterState>

<h1>@State.Count</h1>
<button @onclick="@(() => UpdateAsync(s => s.Increment()))">+</button>
<button @onclick="@(() => UpdateAsync(s => s.Decrement()))">-</button>
```

That's it. All components subscribed to `CounterState` update automatically.

---

## Table of Contents

**Getting Started**
- [Why This Library?](#why-this-library) | [Quick Start](#quick-start) | [Core Concepts](#core-concepts---immutable-state-with-c-records) | [Registration](#registration-options)

**Data Fetching**
- [Query System](#query-system) | [Async Helpers](#async-helpers) | [Optimistic Updates](#optimistic-updates)

**Sync & Collaboration**
- [URL Sync](#url-sync-experimental) | [Cross-Tab Sync](#cross-tab-sync) | [Server Sync](#server-sync-signalr) | [Persistence & DevTools](#state-persistence--redux-devtools-integration)

**History & Advanced**
- [Undo/Redo](#undoredo-history) | [Immer-Style Updates](#immer-style-updates) | [Redux-Style Actions](#redux-style-actions) | [Selectors](#selectors--performance-optimization)

**Reference**
- [Plugins](#plugin-system) | [Middleware](#middleware) | [Security](#security) | [Render Modes](#blazor-render-modes) | [API](#api-reference) | [v2.0 Changes](#breaking-changes-in-v200) | [Comparison](#comparison-with-other-blazor-state-management-libraries) | [FAQ](#frequently-asked-questions-faq)

---

## Core Concepts - Immutable State with C# Records

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

Scoped store for Blazor Server per-user isolation (required for persistence/DevTools/TabSync):

```csharp
builder.Services.AddScopedStoreWithUtilities(
    new UserSessionState(),
    (store, sp) => store
        .WithDefaults(sp, "Session")
        .WithPersistence(sp, "session-state"));  // Works with scoped stores
```

> **Note:** `WithPersistence(sp, key)` requires scoped store registration in Blazor Server. Using it with singleton stores (`AddStore`) will throw an `InvalidOperationException` with guidance to use scoped stores.

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

Built-in utilities for common async patterns. No more writing the same loading/error handling.

### ExecuteCachedAsync - Request Deduplication

**The problem:** 10 components load the same user. Result: 10 API calls, 20 state updates.

**The solution:** `ExecuteCachedAsync` deduplicates both the fetch AND the state updates:

```csharp
// 10 components call this concurrently → 1 API call, 2 state updates
async Task LoadProduct(int productId, CancellationToken ct = default)
{
    await ExecuteCachedAsync(
        $"product-{productId}",
        async () => await ProductService.GetAsync(productId),
        loading: s => s with { Product = s.Product.ToLoading() },
        success: (s, product) => s with { Product = AsyncData.Success(product) },
        error: (s, ex) => s with { Product = AsyncData.Failure(ex.Message) },
        cacheFor: TimeSpan.FromMinutes(5),
        cancellationToken: ct
    );
}
```

| Scenario | Method |
|----------|--------|
| Multiple components load same data | `ExecuteCachedAsync` |
| Single component, no deduplication | `ExecuteAsync` |
| Just need cached data (no state updates) | `LazyLoad` |

### AsyncData\<T\> - No More Boolean Flags

```csharp
// State - replaces IsLoading, HasError, ErrorMessage, Data properties
public record UserState(AsyncData<User> CurrentUser);

// Component
@if (State.CurrentUser.IsLoading) { <Spinner /> }
@if (State.CurrentUser.HasData) { <p>Welcome, @State.CurrentUser.Data.Name</p> }
@if (State.CurrentUser.HasError) { <p class="error">@State.CurrentUser.Error</p> }
```

### Background Refetch (Stale-While-Revalidate)

Show existing data while refreshing in the background:

```csharp
// Transition to loading while preserving existing data
await UpdateAsync(s => s with { User = s.User.ToLoadingPreserved() });

// In component - show loading overlay on existing content
@if (State.User.IsRefetching)
{
    <div class="overlay"><Spinner /></div>
}
@if (State.User.HasData)
{
    <UserCard User="@State.User.Data" />
}
```

| Property | Description |
|----------|-------------|
| `IsFetching` | True during both initial loads and background refetches |
| `IsRefetching` | True when data exists AND a refetch is in progress |
| `ToLoadingPreserved()` | Transitions to loading while keeping existing `Data` |

### ExecuteAsync - Structured Async Flow

```csharp
await ExecuteAsync(
    () => UserService.GetCurrentUserAsync(),
    loading: s => s with { CurrentUser = s.CurrentUser.ToLoading() },
    success: (s, user) => s with { CurrentUser = AsyncData<User>.Success(user) },
    error: (s, ex) => s with { CurrentUser = AsyncData<User>.Failure(ex.Message) }
);
```

### Debounce & Throttle

```csharp
// Search with 300ms debounce
<input @oninput="@(e => UpdateDebounced(s => s.SetSearchQuery(e.Value?.ToString()), 300))" />

// Mouse tracking throttled to 100ms
<div @onmousemove="@(e => UpdateThrottled(s => s.SetPosition(e.ClientX, e.ClientY), 100))">
```

### LazyLoad - Simple Caching

```csharp
var user = await LazyLoad($"user-{userId}", () => UserService.GetUserAsync(userId), cacheFor: TimeSpan.FromMinutes(5));
```

#### Cache Invalidation

Control cached entries when data changes (in components inheriting `StoreComponentWithUtilities<TState>`):

```csharp
// Remove specific cached entry
InvalidateCachedResult($"product-{productId}");

// Remove all entries with prefix (e.g., after bulk operation)
InvalidateCachedResultsByPrefix("product-");

// Clear all cached results (e.g., on user logout)
ClearCachedResults();
```

Or access the executor directly:
```csharp
AsyncExecutor?.InvalidateCache($"product-{productId}");
AsyncExecutor?.InvalidateCacheByPrefix("product-");
AsyncExecutor?.ClearCache();
```

> **Note:** Only the first caller's callbacks (loading, success, error) are executed. Concurrent callers receive the same result but their callbacks are NOT invoked. This is intentional for deduplication.

---

## Optimistic Updates

**Instant feedback.** Update UI immediately, rollback automatically if the server fails.

```csharp
// User clicks delete → item disappears instantly → server confirms (or rollback)
await store.UpdateOptimistic(
    s => s.RemoveItem(itemId),                     // Step 1: Update UI now
    async s => await api.DeleteItemAsync(itemId),  // Step 2: Server call
    (s, error) => s.RestoreItem(itemId)            // Step 3: Rollback if failed
);
```

### With Server Response

```csharp
// Create with pending state, confirm with server-generated ID
await store.UpdateOptimistic<AppState, ServerItem>(
    s => s.AddPendingItem(item),                   // Show "saving..."
    async s => await api.CreateItemAsync(item),    // Server returns ID
    (s, result) => s.ConfirmItem(result),          // Update with real data
    (s, error) => s.RemovePendingItem(item)        // Remove on failure
);
```

---

## URL Sync (Experimental)

**Shareable URLs.** Sync component parameters with store state bidirectionally. Changes to URL update state, state changes update URL.

> **⚠️ Experimental Feature** - This API may change in future versions. `[Experimental("EASB001")]` attribute is applied. Phase 3 with attribute-based auto-sync is now available.

### Attribute-Based Auto-Sync (Zero Boilerplate)

The simplest way to sync URLs - just add attributes:

```csharp
@page "/products"
@inherits UrlSyncStoreComponent<ProductsState>

[SupplyParameterFromQuery]
[AutoSyncWithQuery]
public int Page { get; set; } = 1;  // Auto-maps to state.Page or state.CurrentPage

[SupplyParameterFromQuery]
[AutoSyncWithQuery("q")]  // Custom query param name
public string? Search { get; set; }

[SupplyParameterFromQuery]
[AutoSyncWithQuery]
public bool OnSaleOnly { get; set; }
```

**Convention-based matching:**
- Exact match: `Page` → `state.Page`
- "Current" prefix: `Page` → `state.CurrentPage`
- "Value" suffix: `Name` → `state.NameValue`
- Case-insensitive: `page` → `state.Page`

### Manual Configuration (Advanced)

For more control, use manual configuration:

```csharp
@page "/products"
@inherits UrlSyncStoreComponent<ProductsState>

[SupplyParameterFromQuery] public int Page { get; set; } = 1;
[SupplyParameterFromQuery] public string? Search { get; set; }
[SupplyParameterFromQuery] public bool OnSaleOnly { get; set; }

protected override void ConfigureUrlSync(IUrlSyncBuilder<ProductsState> builder)
{
    builder
        .SyncQueryParam(() => Page, s => s.CurrentPage)
        .SyncQueryParam(() => Search, s => s.SearchQuery)
        .SyncQueryParam(() => OnSaleOnly, s => s.FilterOnSale)
        .WithDebounce(TimeSpan.FromMilliseconds(500))
        .WithNavigationMode(UrlSyncNavigationMode.Replace);
}
```

### Hybrid Approach

Combine both for flexibility:

```csharp
// Auto-sync simple properties
[SupplyParameterFromQuery, AutoSyncWithQuery]
public int Page { get; set; }

// Manual config for advanced options
protected override void ConfigureUrlSync(IUrlSyncBuilder<ProductsState> builder)
{
    builder
        .WithDebounce(TimeSpan.FromMilliseconds(750))
        .ExcludeActions("SERVER_SYNC");
}
```

**What happens:**
1. User visits `/products?page=2&q=laptop&onSaleOnly=true`
2. State updates: `state with { CurrentPage = 2, SearchQuery = "laptop", FilterOnSale = true }`
3. User clicks filter → state changes → URL updates
4. User shares URL → recipient sees same filtered view

### Supported Types

Only **value types** and **strings** are supported (prevents infinite loops):
- Primitives: `int`, `long`, `short`, `byte`, `float`, `double`, `decimal`, `bool`
- Special: `string`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`
- Enums: Any enum type
- Nullable: All above types with `?`

❌ **Not supported**: Lists, arrays, complex objects (causes infinite update loops)

### Options

```csharp
builder
    .SyncQueryParam(() => Page, s => s.CurrentPage, "p")   // Custom param name
    .WithDebounce(TimeSpan.FromMilliseconds(500))          // Debounce rapid changes
    .WithNavigationMode(UrlSyncNavigationMode.Replace)     // Replace vs Push history
    .ExcludeActions("SERVER_SYNC", "CURSOR_UPDATE")        // Don't sync these actions
    .OnConversionError((param, ex) => Logger.LogWarning("Invalid {Param}: {Error}", param, ex.Message))
    .OnError(ex => Logger.LogError(ex, "URL sync error"));
```

### Navigation Modes

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Replace** (default) | Replaces current history entry | High-frequency updates (sliders, filters) - prevents back button pollution |
| **Push** | Adds new history entry | Intentional navigation (wizard steps, tabs) |

### Incompatibilities

⚠️ **Cannot use with:**
- TabSync middleware (each tab needs independent URL)
- Multiple `UrlSyncStoreComponent` per store (only one component can manage URL)

### Security Notes

- URL parameters are **user-controlled input** - always validate with `IStateValidator<T>`
- Sensitive data should **never** be in URLs (use session storage or server state)
- Maximum URL length: ~2000 chars (library warns at 1800)

---

## Undo/Redo History

**Ctrl+Z for your app state.** Full history stack with memory limits and action grouping.

```csharp
builder.Services.AddStoreWithHistory(
    new EditorState(),
    opts => opts
        .WithMaxSize(100)                              // Keep last 100 states
        .WithMaxMemoryMB(50)                           // Cap memory usage
        .ExcludeActions("CURSOR_MOVE", "SELECTION")    // Don't track noise
        .GroupActions(TimeSpan.FromMilliseconds(300)), // Group rapid typing
    (store, sp) => store.WithDefaults(sp, "Editor")
);
```

```razor
@inject IStoreHistory<EditorState> History

<button @onclick="@(() => History.UndoAsync())" disabled="@(!History.CanUndo)">Undo</button>
<button @onclick="@(() => History.RedoAsync())" disabled="@(!History.CanRedo)">Redo</button>
<span>@History.CurrentIndex / @History.Count</span>
```

**Key features:**
- Memory limits prevent runaway growth
- Action grouping collapses rapid changes (typing "hello" = 1 undo step, not 5)
- Exclude transient actions (cursor moves, hover states)
- Jump to any point with `GoToAsync(index)`

---

## Query System

**TanStack Query for Blazor.** Declarative data fetching with automatic caching, background refresh, and smart invalidation.

```csharp
builder.Services.AddQueryClient();
```

### Why This Matters

Without a query system, every component manages its own loading states, error handling, and caching. With it:

| Problem | Query System Solution |
|---------|----------------------|
| Duplicate API calls | Automatic request deduplication |
| Stale data after mutations | `InvalidateQueries(k => k.StartsWith("user-"))` |
| Loading spinners everywhere | Centralized loading/error states |
| Manual retry logic | Built-in with exponential backoff |
| Cache invalidation headaches | Configurable stale/cache times |

### Queries

Inherit from `QueryComponent` — it injects `IQueryClient` for you (available as the `QueryClient` property), wires queries to re-render the component, and disposes them automatically.

```csharp
@inherits QueryComponent

@code {
    private Query<User> userQuery = null!;

    protected override void OnInitialized()
    {
        userQuery = UseQuery(new QueryOptions<User>
        {
            Key = "user-123",                                       // Cache key
            QueryFn = async ct => await api.GetUserAsync(123, ct),  // Fetch function
            StaleTime = TimeSpan.FromMinutes(5),                    // Fresh for 5 min
            CacheTime = TimeSpan.FromHours(1),                      // Cache for 1 hour
            Retry = 3                                               // Retry 3 times
        });
    }
}

@if (userQuery.IsLoading) { <Spinner /> }
@if (userQuery.IsError) { <Error Message="@userQuery.Error?.Message" /> }
@if (userQuery.IsSuccess) { <UserCard User="@userQuery.Data" /> }
```

For simple cases, skip the options object:

```csharp
userQuery = UseQuery("user-123", async ct => await api.GetUserAsync(123, ct));
```

### Mutations with Auto-Invalidation

```csharp
@inherits QueryComponent

@code {
    private Mutation<User, UpdateUserRequest> mutation = null!;

    protected override void OnInitialized()
    {
        mutation = UseMutation(new MutationOptions<User, UpdateUserRequest>
        {
            MutationFn = async (req, ct) => await api.UpdateUserAsync(req, ct),
            OnSuccess = (_, _) => QueryClient.InvalidateQueries(
                key => key.StartsWith("user-"))   // Refetch all user queries
        });
    }

    async Task Save()
    {
        await mutation.MutateAsync(new UpdateUserRequest { Name = "John" });
        // All "user-…" queries automatically refresh
    }
}
```

---

## Cross-Tab Sync

**User adds item to cart in Tab 1. Tab 2 updates instantly.** No polling, no manual refresh.

```csharp
builder.Services.AddStore(
    new CartState(),
    (store, sp) => store
        .WithDefaults(sp, "Cart")
        .WithTabSync(sp, opts => opts
            .Channel("shopping-cart")
            .EnableMessageSigning()                    // HMAC-SHA256 security
            .MaxMessageAgeSeconds(30)                  // Replay attack prevention
            .ExcludeActions("HOVER", "FOCUS"))         // Don't sync transient state
);
```

That's all the code needed. Components don't change. Sync is automatic.

```
Tab 1: User adds item → Store updates → Broadcast
                                            ↓
Tab 2:                                 Receives → Store syncs → UI updates
```

### Security

| Option | Purpose |
|--------|---------|
| `EnableMessageSigning()` | HMAC-SHA256 signatures |
| `MaxMessageAgeSeconds(30)` | Reject old messages (replay attacks) |
| `MaxMessageSizeBytes(1MB)` | Prevent DoS |
| `RequireValidSignature(true)` | Reject unsigned messages |

---

## Server Sync (SignalR)

**Build Google Docs-style collaboration.** Real-time state sync across users with presence indicators and live cursors.

```csharp
builder.Services.AddStore(
    new DocumentState(),
    (store, sp) => store
        .WithDefaults(sp, "Document")
        .WithServerSync(sp, opts =>
        {
            opts.HubUrl = "/hubs/documents";
            opts.DocumentId = documentId;
            opts.EnablePresence = true;                  // "3 users editing"
            opts.EnableCursorTracking = true;            // See other users' cursors
            opts.ConflictResolution = ConflictResolution.LastWriteWins;
            opts.UserDisplayName = currentUser.Name;
            opts.UserCursorColor = "#ff0000";
            opts.OnUserJoined = user => ShowToast($"{user.DisplayName} joined");
            opts.OnCursorUpdated = cursor => RenderCursor(cursor);
        })
);
```

The connection is established automatically when the store is built (`AutoConnect = true` by default). Presence identity is configured via `UserDisplayName`/`UserCursorColor`, and presence/cursor changes from other users arrive through the `OnUserJoined`, `OnUserLeft`, `OnPresenceChanged`, and `OnCursorUpdated` callbacks.

### Conflict Resolution

| Mode | When to Use |
|------|-------------|
| `LastWriteWins` | Default. Most recent change wins. |
| `ServerWins` | Server is authoritative. |
| `ClientWins` | Offline-first apps. |
| `Custom` | Complex merge logic. |

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
    public override Task OnStoreCreatedAsync(IStore<AppState> store) { /* Store initialized */ }
    public override Task OnBeforeUpdateAsync(AppState currentState, string? action) { /* Pre-update */ }
    public override Task OnAfterUpdateAsync(AppState prev, AppState next, string? action) { /* Post-update */ }
    public override Task OnStoreDisposingAsync() { /* Cleanup */ }
    public override IMiddleware<AppState>? GetMiddleware() => null; // Custom middleware
}
```

---

## Security

### Security Profiles

| Profile | DevTools | Validation | Message Signing | Use Case |
|---------|----------|------------|-----------------|----------|
| `Development` | Enabled (debugger-gated) | Optional | Optional | Local development |
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
| DevTools in production | Leave `DevToolsOptions.Enabled` unset (debugger-gated) or use `AddSecureStore` |
| Secrets in localStorage | Use `TransformOnSave` to exclude |
| Missing state validation | Register `IStateValidator<T>` |
| TabSync without signing | Enable `EnableMessageSigning` |
| No history memory limit | Set `WithMaxMemoryMB()` |
| Client-side trust | Always validate on server |

---

## Selectors & Performance Optimization

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

## State Persistence & Redux DevTools Integration

### LocalStorage Persistence

```csharp
builder.Services.AddScopedStore(
    new AppState(),
    (store, sp) => store
        .WithDefaults(sp, "App")
        .WithPersistence(sp, "app-state"));  // Auto-save & restore
```

### Redux DevTools

Included with `WithDefaults()` and `WithDevTools()`. Activation is gated at runtime: DevTools turn on when a debugger is attached, or when explicitly enabled via `DevToolsOptions<TState>.Enabled = true`. Never enable in production. Features:
- Time-travel debugging
- State inspection
- Action replay
- Import/export

Install: [Redux DevTools Extension](https://github.com/reduxjs/redux-devtools)

### Diagnostics

```csharp
builder.Services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
builder.Services.AddStore(state, (store, sp) => store.WithDiagnostics(sp));

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
| DevToolsMiddleware | Redux DevTools (debugger-attached or opt-in) |
| PersistenceMiddleware | LocalStorage |
| LoggingMiddleware | Console logging |
| HistoryMiddleware | Undo/redo |
| TabSyncMiddleware | Cross-tab sync |
| ServerSyncMiddleware | SignalR sync |
| PluginMiddleware | Plugin lifecycle |
| DiagnosticsMiddleware | Performance metrics |

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
protected Task<T> ExecuteCachedAsync<T>(string key, Func<Task<T>> action, ..., TimeSpan? cacheFor, CancellationToken ct = default);
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
    .WithDefaults(sp, "Name")              // DevTools (debugger-gated) + Logging
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
    .WithDiagnostics(sp)                   // Performance diagnostics
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

## Comparison with Other Blazor State Management Libraries

| Feature | This Library | Fluxor | Blazor-State |
|---------|-------------|--------|--------------|
| **Boilerplate** | Zero | High (actions, reducers, effects) | Medium (handlers) |
| **Learning Curve** | Minimal (Zustand-like) | Steep (Redux patterns) | Medium (MediatR) |
| **Query System** | Built-in (TanStack-inspired) | None | None |
| **Request Deduplication** | Built-in | None | None |
| **Cross-Tab Sync** | Built-in (BroadcastChannel) | Manual | None |
| **Server Sync + Presence** | Built-in (SignalR) | Manual | None |
| **Undo/Redo History** | Built-in | Manual | None |
| **Optimistic Updates** | Built-in with rollback | Manual | None |
| **Persistence** | Built-in (localStorage) | Manual | Manual |
| **DevTools** | Redux DevTools | Redux DevTools | None |
| **Immer-Style Updates** | Built-in | None | None |
| **.NET Version** | 8, 9, 10 | 8+ | 8+ |
| **MAUI Hybrid** | Supported | Limited | Limited |

### When to Choose What

**This library** - You want features out of the box. Query system, sync, undo/redo, optimistic updates. All without ceremony.

**Fluxor** - Your team knows Redux. You want strict unidirectional data flow with actions/reducers/effects.

**Blazor-State** - You prefer MediatR-style patterns with request/handler separation.

---

## Frequently Asked Questions (FAQ)

### How does this compare to Fluxor?

Fluxor follows traditional Redux patterns with actions, reducers, and effects. EasyAppDev.Blazor.Store takes a simpler approach inspired by Zustand - your state is just a C# record with methods. No boilerplate, no ceremony. Both support Redux DevTools.

### Can I use this with Blazor Server?

Yes. Use `AddScopedStore` for full feature support (DevTools, persistence, cross-tab sync) in Blazor Server. Singleton stores work but cannot use JavaScript-dependent features.

### How do I persist state to localStorage?

```csharp
// For Blazor WebAssembly
builder.Services.AddStoreWithUtilities(
    new AppState(),
    (store, sp) => store
        .WithDefaults(sp, "App")
        .WithPersistence(sp, "app-state"));  // Auto-saves to localStorage

// For Blazor Server - use scoped store
builder.Services.AddScopedStoreWithUtilities(
    new AppState(),
    (store, sp) => store
        .WithDefaults(sp, "App")
        .WithPersistence(sp, "app-state"));
```

### Does this work with .NET MAUI Blazor?

Yes. The core store functionality works in MAUI Blazor Hybrid apps. Browser-specific features (DevTools, localStorage, cross-tab sync) require a browser context.

### How do I handle async operations like API calls?

Use the built-in `ExecuteAsync` helper or async state methods:

```csharp
await ExecuteAsync(
    () => api.LoadUsersAsync(),
    loading: s => s with { IsLoading = true },
    success: (s, users) => s with { Users = users, IsLoading = false },
    error: (s, ex) => s with { Error = ex.Message, IsLoading = false });
```

### Is state shared across browser tabs?

Not by default. Enable cross-tab synchronization with `WithTabSync`:

```csharp
.WithTabSync(sp, opts => opts.Channel("my-app").EnableMessageSigning())
```

### How do I debug state changes?

Install the [Redux DevTools browser extension](https://github.com/reduxjs/redux-devtools). State changes are logged when using `WithDefaults()` or `WithDevTools()` while a debugger is attached (or when explicitly enabled via `DevToolsOptions<TState>.Enabled = true`).

### Can multiple components share the same state?

Yes. All components inheriting `StoreComponent<T>` for the same state type automatically share state and receive updates.

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

---

### Found this library helpful?

If EasyAppDev.Blazor.Store has made state management easier in your Blazor projects, consider giving it a ⭐ on GitHub. It helps others discover the library and motivates continued development.

[![GitHub stars for EasyAppDev.Blazor.Store - Blazor state management library](https://img.shields.io/github/stars/mashrulhaque/EasyAppDev.Blazor.Store?style=social)](https://github.com/mashrulhaque/EasyAppDev.Blazor.Store)

</div>
