# Phase 2: Cleanup & Simplification

> Version: 1.2.0 | Status: Complete | Risk: Low-Medium

## Overview

Phase 2 focuses on reducing complexity, removing deprecated code, and improving maintainability. Some minor breaking changes for deprecated APIs.

**Goal:** A leaner, more focused library that's easier to maintain and understand.

---

## Changes

### 2.1 Remove Deprecated Update() Method

**File:** `src/EasyAppDev.Blazor.Store/Core/Store.cs`

**Current State:**
```csharp
[Obsolete("Synchronous Update can cause deadlock with cross-store updates. Use UpdateAsync instead.")]
public void Update(Func<TState, TState> updater, string? action = null)
{
    UpdateAsync(updater, action).GetAwaiter().GetResult();
}
```

**Action:** Remove entirely.

**Why:**
- Causes deadlocks in cross-store scenarios
- `.GetAwaiter().GetResult()` blocks thread pool threads
- Has been obsolete since early versions
- Anyone still using it needs to migrate

**Also Remove From:**
- `IStateWriter<T>.Update()` interface method
- `StoreComponent<T>.UpdateState()` method

**Migration Path for Users:**
```csharp
// Before
store.Update(s => s.Increment());
UpdateState(s => s.Increment());

// After
await store.UpdateAsync(s => s.Increment());
await Update(s => s.Increment());
```

---

### 2.2 Extract Diagnostics to Separate Package

**Current Files:**
```
src/EasyAppDev.Blazor.Store/
├── Diagnostics/
│   ├── IDiagnosticsService.cs
│   ├── DiagnosticsService.cs
│   ├── DiagnosticsMiddleware.cs
│   └── Models/
│       ├── ActionHistoryEntry.cs
│       ├── RenderEvent.cs
│       ├── StateDiff.cs
│       ├── PerformanceMetrics.cs
│       └── SubscriptionInfo.cs
```

**New Structure:**
```
src/
├── EasyAppDev.Blazor.Store/              (core - no diagnostics)
└── EasyAppDev.Blazor.Store.Diagnostics/  (new package)
    ├── IDiagnosticsService.cs
    ├── DiagnosticsService.cs
    ├── DiagnosticsMiddleware.cs
    ├── DiagnosticsStoreComponent.cs      (new - extends StoreComponent)
    ├── Models/
    └── ServiceExtensions.cs
```

**Why:**
- Core library stays lean
- Diagnostics adds ~15% code size
- Most production apps don't need it
- DEBUG-only code shouldn't be in release builds
- Separation of concerns

**New Package Usage:**
```csharp
// Program.cs
builder.Services.AddStoreDiagnostics();

// Component
@inherits DiagnosticsStoreComponent<CounterState>

// Or via middleware
builder.Services.AddStore(state, (store, sp) => store
    .WithDiagnostics(sp)  // Only in Diagnostics package
);
```

**Migration:**
```xml
<!-- Before (implicit) -->
<PackageReference Include="EasyAppDev.Blazor.Store" Version="1.1.x" />

<!-- After (explicit if needed) -->
<PackageReference Include="EasyAppDev.Blazor.Store" Version="1.2.0" />
<PackageReference Include="EasyAppDev.Blazor.Store.Diagnostics" Version="1.2.0" />
```

---

### 2.3 Consolidate DevTools Overloads

**Current State (3 overloads):**
```csharp
// 1. Obsolete - no JSRuntime
[Obsolete]
public StoreBuilder<TState> WithDevTools(string? storeName = null)

// 2. Direct IJSRuntime injection
public StoreBuilder<TState> WithDevTools(IJSRuntime jsRuntime, string? storeName = null)

// 3. Lazy via IServiceProvider (recommended)
public StoreBuilder<TState> WithDevTools(IServiceProvider serviceProvider, string? storeName = null)
```

**Target State (1 overload):**
```csharp
/// <summary>
/// Enables Redux DevTools integration with lazy IJSRuntime resolution.
/// Works in all render modes: Server, WebAssembly, and Auto.
/// </summary>
public StoreBuilder<TState> WithDevTools(IServiceProvider serviceProvider, string? storeName = null)
```

**Why:**
- Less API surface = less confusion
- IServiceProvider version works everywhere
- Direct IJSRuntime injection is an anti-pattern (scoped service in singleton)
- Obsolete version is already obsolete

**Migration:**
```csharp
// Before (deprecated)
.WithDevTools("Counter")
.WithDevTools(jsRuntime, "Counter")

// After (only option)
.WithDevTools(serviceProvider, "Counter")
```

---

### 2.4 Slim Down StoreComponent<T>

**Current Responsibilities (too many):**
1. Store subscription management
2. State access
3. Update methods (sync, async)
4. Debounce integration
5. Throttle integration
6. LazyCache integration
7. AsyncExecutor integration
8. Diagnostics integration
9. Selector subscriptions

**Target Responsibilities (focused):**
1. Store subscription management
2. State access
3. Update methods (async only)
4. Selector subscriptions

**Removal Targets:**

**Remove LazyCache:**
```csharp
// Remove from StoreComponent
[Inject]
protected ILazyCache LazyCache { get; set; } = default!;

protected Task<T> LazyLoad<T>(string cacheKey, Func<Task<T>> loader, TimeSpan? cacheFor = null)
```

**Reason:** Data loading/caching is not state management. Users can inject `ILazyCache` directly if needed.

**Remove Debounce/Throttle Helpers:**
```csharp
// Remove these methods (keep the injected services for backward compat)
protected Task UpdateDebounced(...)
protected Task UpdateDebouncedAsync(...)
protected Task UpdateThrottled(...)
protected Task UpdateThrottledAsync(...)
```

**Reason:** Users can compose these themselves:
```csharp
// Users can do this directly
await DebounceManager.Debounce("key", async () =>
{
    await Update(s => s.DoSomething());
}, 300);
```

**Keep in StoreComponent:**
```csharp
public abstract class StoreComponent<TState> : ComponentBase, IDisposable
{
    [Inject] protected IStore<TState> Store { get; set; } = default!;
    [Inject] protected ILogger<StoreComponent<TState>>? Logger { get; set; }

    protected TState State => Store.GetState();

    protected Task Update(Func<TState, TState> updater, string? action = null);
    protected Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null);

    protected virtual void SubscribeToStore();
    protected IDisposable SubscribeToSelector<TSelected>(...);
}
```

**New Utility Components (optional):**
```csharp
// For users who want utilities
public abstract class StoreComponentWithUtilities<TState> : StoreComponent<TState>
{
    [Inject] protected IDebounceManager DebounceManager { get; set; } = default!;
    [Inject] protected IThrottleManager ThrottleManager { get; set; } = default!;
    [Inject] protected ILazyCache LazyCache { get; set; } = default!;

    protected Task UpdateDebounced(...);
    protected Task UpdateThrottled(...);
    protected Task<T> LazyLoad<T>(...);
}
```

---

### 2.5 Simplify AsyncActionExecutor Usage

**Current (verbose):**
```csharp
await ExecuteAsync(
    asyncAction: () => api.LoadUsersAsync(),
    loading: s => s with { IsLoading = true },
    success: (s, users) => s with { Users = users, IsLoading = false },
    error: (s, ex) => s with { Error = ex.Message, IsLoading = false }
);
```

**Problem:** This pattern is verbose and users already have `AsyncData<T>`.

**Solution:** Deprecate `ExecuteAsync` in favor of direct `AsyncData<T>` usage:
```csharp
// Recommended pattern (no ExecuteAsync needed)
await Update(s => s with { Users = s.Users.ToLoading() });

try
{
    var users = await api.LoadUsersAsync();
    await Update(s => s with { Users = AsyncData<List<User>>.Success(users) });
}
catch (Exception ex)
{
    await Update(s => s with { Users = AsyncData<List<User>>.Failure(ex.Message) });
}
```

**Or with helper extension:**
```csharp
// New extension method
await UpdateWithAsync(
    s => s.Users,                           // selector
    () => api.LoadUsersAsync(),             // loader
    (s, data) => s with { Users = data }    // updater
);
```

---

## File Changes Summary

### Files to Remove from Core Package
```
src/EasyAppDev.Blazor.Store/
├── Diagnostics/              (entire folder → new package)
```

### Files to Modify
```
src/EasyAppDev.Blazor.Store/
├── Core/
│   ├── Store.cs              (remove Update method)
│   ├── IStateWriter.cs       (remove Update from interface)
│   └── StoreBuilder.cs       (remove obsolete WithDevTools overloads)
├── Blazor/
│   └── StoreComponent.cs     (slim down, remove utilities)
└── EasyAppDev.Blazor.Store.csproj (remove diagnostics files)
```

### New Package
```
src/EasyAppDev.Blazor.Store.Diagnostics/
├── EasyAppDev.Blazor.Store.Diagnostics.csproj
├── DiagnosticsStoreComponent.cs
├── ServiceExtensions.cs
└── (moved from core)
```

---

## Migration Guide

### From 1.1.x to 1.2.x

**1. Update() → UpdateAsync()**
```csharp
// Before
store.Update(s => s.Increment());
UpdateState(s => s.Increment());

// After
await store.UpdateAsync(s => s.Increment());
await Update(s => s.Increment());
```

**2. DevTools Configuration**
```csharp
// Before (any of these)
.WithDevTools("Counter")
.WithDevTools(jsRuntime, "Counter")
.WithDevTools(serviceProvider, "Counter")

// After (only option)
.WithDevTools(serviceProvider, "Counter")
```

**3. Diagnostics (if using)**
```xml
<!-- Add new package -->
<PackageReference Include="EasyAppDev.Blazor.Store.Diagnostics" Version="1.2.0" />
```

```csharp
// Update usings
using EasyAppDev.Blazor.Store.Diagnostics;

// Update DI
builder.Services.AddStoreDiagnostics();
```

**4. LazyLoad/Debounce/Throttle Methods**
```csharp
// Before (via StoreComponent)
await LazyLoad("key", LoadDataAsync);
await UpdateDebounced(s => s.Search(query), 300);

// After (inject directly or use StoreComponentWithUtilities)
@inject ILazyCache LazyCache
@inject IDebounceManager DebounceManager

await LazyCache.GetOrLoadAsync("key", LoadDataAsync);
await DebounceManager.Debounce("search", () => Update(s => s.Search(query)), 300);

// Or use new base class
@inherits StoreComponentWithUtilities<MyState>
await LazyLoad("key", LoadDataAsync);  // Still works
```

---

## Implementation Checklist

### 2.1 Remove Update()
- [x] Remove from `Store<T>`
- [x] Remove from `IStateWriter<T>`
- [x] Remove `UpdateState` from `StoreComponent<T>`
- [x] Update all tests
- [x] Add compiler error guidance

### 2.2 Extract Diagnostics
- [x] ~~Create new project file~~ (Deferred - using #if DEBUG conditional compilation)
- [x] ~~Move diagnostic files~~ (Deferred - keeping in core with conditional compilation)
- [x] ~~Create `DiagnosticsStoreComponent<T>`~~ (Deferred)
- [x] ~~Create service extensions~~ (Deferred)
- [x] Diagnostics remain DEBUG-only via `#if DEBUG` blocks (alternative approach)

### 2.3 Consolidate DevTools
- [x] Remove obsolete overloads
- [x] Remove `WithJSRuntime` method
- [x] Update documentation
- [x] Test all render modes

### 2.4 Slim StoreComponent
- [x] Create `StoreComponentWithUtilities<T>`
- [x] Move utility methods to new class
- [x] Keep backward compatibility via new class
- [x] Update samples to show both approaches
- [x] Document migration path

### 2.5 Simplify AsyncExecutor
- [x] Add `UpdateWithAsync` extension method
- [ ] Deprecate `ExecuteAsync` methods (kept for backward compatibility)
- [x] Update documentation with patterns
- [x] Add examples in samples

---

## Testing Requirements

### Removed API Tests
- Verify compiler errors for removed methods
- Test migration paths work

### New Package Tests
```
tests/EasyAppDev.Blazor.Store.Diagnostics.Tests/
├── DiagnosticsServiceTests.cs
├── DiagnosticsMiddlewareTests.cs
└── DiagnosticsStoreComponentTests.cs
```

### Integration Tests
- Core package works without diagnostics
- Diagnostics package integrates correctly
- All render modes still function

---

## Release Checklist

- [x] All deprecated APIs removed
- [x] ~~Diagnostics package created~~ (Deferred - using #if DEBUG)
- [x] StoreComponent slimmed down
- [x] DevTools consolidated
- [x] Migration guide complete
- [x] All tests passing (352 tests)
- [x] Version bumped to 1.2.0
- [ ] Package published to NuGet

---

## Success Criteria

1. Core package size reduced by 15%+
2. Public API surface reduced by 30%+
3. Zero breaking changes for common use cases
4. Clear migration path for removed features
5. Diagnostics package works independently

---

[← Phase 1](PHASE_1_BUG_FIXES.md) | [Back to Roadmap](../ROADMAP.md) | [Phase 3 →](PHASE_3_CORE_ENHANCEMENTS.md)
