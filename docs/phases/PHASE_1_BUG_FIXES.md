# Phase 1: Bug Fixes & Polish

> Version: 1.1.0 | Status: ✅ Complete | Risk: Low

## Overview

Phase 1 focuses on fixing known bugs and polishing existing code without introducing breaking changes. This phase establishes a solid foundation for future enhancements.

**Goal:** Ship a bulletproof 1.1.x release that users can trust completely.

---

## Issues to Address

### 1.1 AsyncData<T> Should Be a Record

**File:** `src/EasyAppDev.Blazor.Store/AsyncActions/AsyncData.cs`

**Problem:**
`AsyncData<T>` is defined as a `class` but should be a `record` to maintain immutability consistency with the rest of the library.

```csharp
// Current (problematic)
public class AsyncData<T>
{
    public T? Data { get; init; }
    public bool IsLoading { get; init; }
    // ...
}

// Should be
public record AsyncData<T>
{
    public T? Data { get; init; }
    public bool IsLoading { get; init; }
    // ...
}
```

**Why This Matters:**
- Users might accidentally hold references and mutate
- Breaks pattern consistency ("state is records")
- Missing value equality semantics

**Implementation:**
1. Change `class` to `record`
2. Ensure static factory methods still work
3. Update tests to verify immutability
4. Add equality tests

**Tests to Add:**
```csharp
[Fact]
public void AsyncData_ShouldHaveValueEquality()
{
    var a = AsyncData<int>.Success(42);
    var b = AsyncData<int>.Success(42);
    a.Should().Be(b);
}

[Fact]
public void AsyncData_WithExpression_ShouldWork()
{
    var loading = AsyncData<int>.Loading();
    var success = loading with { HasData = true, Data = 42 };
    success.HasData.Should().BeTrue();
}
```

---

### 1.2 MemoizedSelector Thread Safety

**File:** `src/EasyAppDev.Blazor.Store/Selectors/MemoizedSelector.cs`

**Problem:**
The `Select` method has race conditions when called from multiple threads:

```csharp
// Current (race condition)
public TResult Select(TState state)
{
    if (_hasCache && EqualityComparer<TState>.Default.Equals(_lastState, state))
    {
        return _cachedResult!;  // Thread A reads here
    }

    var result = _selector(state);
    _lastState = state;         // Thread B writes here
    _cachedResult = result;     // Interleaved access
    _hasCache = true;

    return result;
}
```

**Why This Matters:**
- Blazor Server: Multiple users = multiple threads
- Corrupted cache = wrong data shown to users
- Hard to reproduce = hard to debug

**Implementation:**
```csharp
public TResult Select(TState state)
{
    ArgumentNullException.ThrowIfNull(state);

    lock (_lock)
    {
        if (_hasCache && EqualityComparer<TState>.Default.Equals(_lastState, state))
        {
            return _cachedResult!;
        }

        var result = _selector(state);

        if (!_hasCache || !_comparer.Equals(_cachedResult, result))
        {
            _lastState = state;
            _cachedResult = result;
            _hasCache = true;
        }

        return result;
    }
}
```

**Alternative (Lock-Free):**
```csharp
private volatile CacheEntry? _cache;

private sealed record CacheEntry(TState State, TResult Result);

public TResult Select(TState state)
{
    var cache = _cache;
    if (cache != null && EqualityComparer<TState>.Default.Equals(cache.State, state))
    {
        return cache.Result;
    }

    var result = _selector(state);
    _cache = new CacheEntry(state, result);
    return result;
}
```

**Tests to Add:**
```csharp
[Fact]
public async Task MemoizedSelector_ShouldBeThreadSafe()
{
    var selector = new MemoizedSelector<int, string>(x => x.ToString());
    var tasks = Enumerable.Range(0, 100)
        .Select(i => Task.Run(() => selector.Select(i % 10)));

    var results = await Task.WhenAll(tasks);
    // Should not throw, should return consistent results
}
```

---

### 1.3 Swallowed Exceptions in StoreBuilder

**Files:**
- `src/EasyAppDev.Blazor.Store/Core/StoreBuilder.cs:282-285`
- `src/EasyAppDev.Blazor.Store/Core/StoreBuilder.cs:329-332`

**Problem:**
Exceptions are silently swallowed, making debugging impossible:

```csharp
catch (Exception ex)
{
    _ = ex;  // Gone forever
}
```

**Why This Matters:**
- Users report "persistence doesn't work" with no way to diagnose
- Violates principle of least surprise
- Silent failures are worse than loud failures

**Implementation:**
```csharp
catch (Exception ex)
{
    // Log at Debug level - user can enable if needed
    System.Diagnostics.Debug.WriteLine(
        $"[EasyAppDev.Store] Failed to load persisted state for key '{key}': {ex.Message}");

    // If logger is available, use it
    _logger?.LogDebug(ex, "Failed to hydrate state from persistence key: {Key}", key);
}
```

**Better Approach - Add Optional Callback:**
```csharp
public StoreBuilder<TState> WithPersistence(
    IPersistenceProvider provider,
    string key,
    JsonSerializerOptions? jsonOptions = null,
    int debounceMs = 0,
    Action<Exception>? onHydrationError = null)  // New parameter
{
    try
    {
        // hydration logic
    }
    catch (Exception ex)
    {
        onHydrationError?.Invoke(ex);
        _logger?.LogDebug(ex, "Failed to hydrate state");
    }
}
```

---

### 1.4 Replace Console.WriteLine with ILogger

**Files:**
- `src/EasyAppDev.Blazor.Store/Persistence/LocalStorageProvider.cs`
- `src/EasyAppDev.Blazor.Store/Persistence/SessionStorageProvider.cs`

**Problem:**
```csharp
catch (Exception ex)
{
    Console.WriteLine($"Error loading from localStorage: {ex.Message}");
}
```

**Why This Matters:**
- Not visible in production logging systems
- No structured logging
- Can't be filtered/disabled
- Unprofessional

**Implementation:**
```csharp
public class LocalStorageProvider : IPersistenceProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LocalStorageProvider>? _logger;

    public LocalStorageProvider(
        IJSRuntime jsRuntime,
        ILogger<LocalStorageProvider>? logger = null)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<string?> LoadAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load from localStorage key: {Key}", key);
            return null;
        }
    }
}
```

**Update DI Registration:**
```csharp
public static IServiceProvider AddLocalStorageProvider(this IServiceCollection services)
{
    services.AddScoped<IPersistenceProvider>(sp =>
    {
        var js = sp.GetRequiredService<IJSRuntime>();
        var logger = sp.GetService<ILogger<LocalStorageProvider>>();
        return new LocalStorageProvider(js, logger);
    });
}
```

---

### 1.5 Complete XML Documentation

**Problem:**
Some public APIs lack XML documentation, causing IDE warnings and poor IntelliSense.

**Files to Review:**
- All files in `src/EasyAppDev.Blazor.Store/`
- Focus on public methods, properties, and constructors

**Standard Template:**
```csharp
/// <summary>
/// [What it does - one sentence]
/// </summary>
/// <typeparam name="T">[Description of type parameter]</typeparam>
/// <param name="paramName">[Description of parameter]</param>
/// <returns>[What is returned]</returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="paramName"/> is null.
/// </exception>
/// <example>
/// <code>
/// var result = Method(arg);
/// </code>
/// </example>
/// <remarks>
/// [Additional context, thread-safety notes, etc.]
/// </remarks>
```

**Priority Targets:**
1. `IStore<T>` and all interface methods
2. `StoreComponent<T>` protected methods
3. `AsyncData<T>` factory methods
4. `StoreBuilder<T>` fluent methods

---

## Implementation Checklist

### 1.1 AsyncData<T> Record Conversion
- [x] Change class to record
- [x] Verify static factory methods work
- [x] Add value equality tests
- [x] Add with-expression tests
- [x] Update any documentation

### 1.2 MemoizedSelector Thread Safety
- [x] Add locking mechanism (implemented lock-free volatile pattern)
- [x] Add concurrent access tests
- [x] Consider lock-free alternative (chosen approach)
- [x] Benchmark performance impact
- [x] Document thread-safety guarantees

### 1.3 Exception Handling
- [x] Add logging to StoreBuilder hydration
- [ ] Add optional error callback parameter (deferred - Debug.WriteLine sufficient for now)
- [x] Update documentation
- [ ] Add test for error callback (deferred)

### 1.4 ILogger Integration
- [x] Update LocalStorageProvider
- [x] Update SessionStorageProvider
- [ ] Update DI registration helpers (optional - constructor injection works)
- [x] Add logger parameters to constructors

### 1.5 XML Documentation
- [x] Audit all public APIs
- [x] Add missing documentation
- [x] Enable documentation warnings in csproj (already enabled)
- [x] Generate documentation file (already enabled)

---

## Testing Requirements

### New Tests
```
tests/
└── EasyAppDev.Blazor.Store.Tests/
    ├── AsyncActions/
    │   ├── AsyncDataImmutabilityTests.cs      (new)
    │   └── AsyncDataEqualityTests.cs          (new)
    ├── Selectors/
    │   └── MemoizedSelectorThreadSafetyTests.cs (new)
    └── Persistence/
        └── ProviderLoggingTests.cs            (new)
```

### Test Coverage Goals
- AsyncData: 100% coverage
- MemoizedSelector: 100% coverage including threading
- Persistence providers: Error path coverage

---

## Migration Guide

**From 1.0.x to 1.1.x:**

No breaking changes. Direct upgrade.

```xml
<!-- Before -->
<PackageReference Include="EasyAppDev.Blazor.Store" Version="1.0.8" />

<!-- After -->
<PackageReference Include="EasyAppDev.Blazor.Store" Version="1.1.0" />
```

**Behavioral Changes:**
- `AsyncData<T>` now has value equality (may affect existing equality checks)
- Persistence errors now log warnings instead of silently failing

---

## Release Checklist

- [ ] All issues implemented
- [ ] All tests passing
- [ ] Test coverage maintained or improved
- [ ] Documentation updated
- [ ] CHANGELOG updated
- [ ] Version bumped to 1.1.0
- [ ] NuGet package built and tested locally
- [ ] Release notes drafted

---

## Success Criteria

1. Zero known bugs in core functionality
2. Thread-safety verified under load
3. All public APIs documented
4. Logging integrated throughout
5. No silent failures

---

[← Back to Roadmap](../ROADMAP.md) | [Next: Phase 2 →](PHASE_2_CLEANUP.md)
