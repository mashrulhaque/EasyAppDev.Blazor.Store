# Changelog

All notable changes to EasyAppDev.Blazor.Store will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-14

### 🎉 First Stable Release

This is the first stable release of EasyAppDev.Blazor.Store, featuring a complete architectural refactoring based on SOLID principles.

### 🚨 Breaking Changes

#### 1. StoreComponent Now Requires Utility Services

**Impact**: Components inheriting from `StoreComponent<T>` will fail at runtime if utility services are not registered.

**Before** (alpha versions):
```csharp
// Just register the store
builder.Services.AddStore(new CounterState(0));
```

**After** (v1.0.0):
```csharp
// Option 1: Convenience method (recommended)
builder.Services.AddStoreWithUtilities(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter"));

// Option 2: Manual registration
builder.Services.AddStoreUtilities();
builder.Services.AddAsyncActionExecutor<CounterState>();
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter"));
```

**Why**: `StoreComponent` now uses dependency injection for debounce, throttle, caching, and async execution functionality, improving testability and following SOLID principles.

#### 2. Direct Store Constructor Changed

**Impact**: Direct instantiation of `Store<T>` now requires `ISubscriptionManager` parameter.

**Before** (alpha versions):
```csharp
var store = new Store<CounterState>(new CounterState(0));
```

**After** (v1.0.0):
```csharp
// Use StoreBuilder pattern
var store = StoreBuilder<CounterState>
    .Create(new CounterState(0))
    .Build();
```

**Why**: Separation of concerns - subscription management is now a separate responsibility handled by `ISubscriptionManager`.

### ✨ New Features

#### SOLID Architecture Refactoring

- **Single Responsibility Principle (SRP)**
  - Extracted `ISubscriptionManager` from `Store<T>` (subscription lifecycle management)
  - Extracted `IAsyncActionExecutor<T>` from `StoreComponent<T>` (async action handling)
  - Separated utility concerns: `IDebounceManager`, `IThrottleManager`, `ILazyCache`

- **Interface Segregation Principle (ISP)**
  - Split `IStore<T>` into focused interfaces:
    - `IStateReader<T>` - Read-only state access
    - `IStateWriter<T>` - State mutation operations
    - `IStateObservable<T>` - Subscription/notification functionality
  - Components can depend only on what they need

- **Dependency Inversion Principle (DIP)**
  - All components depend on abstractions, not implementations
  - Improved testability with mockable interfaces
  - Dependency injection throughout the library

#### Dependency Injection Support

- **Service Registration Extensions**
  - `AddStoreUtilities()` - Registers debounce, throttle, and cache services
  - `AddAsyncActionExecutor<TState>()` - Registers async action executor for a state type
  - `AddStoreWithUtilities<TState>()` - Convenience method combining all registrations
  - `AddScopedStoreWithUtilities<TState>()` - Scoped store with utilities (Blazor Server)

- **Automatic Dependency Resolution**
  - `StoreComponent<T>` automatically injects required services
  - Scoped lifetime for utility services (per Blazor connection)
  - Proper disposal and cleanup

#### Structured Logging

- **Replaced Console.WriteLine with ILogger<T>**
  - Production-ready logging throughout the library
  - Configurable log levels
  - Integration with ASP.NET Core logging infrastructure
  - Better debugging and monitoring capabilities

#### Comprehensive XML Documentation

- **1,655+ lines of XML documentation** added across 8 core interfaces:
  - `IStateReader<T>` - 72 lines
  - `IStateWriter<T>` - 198 lines
  - `IStateObservable<T>` - 216 lines
  - `ISubscriptionManager<T>` - 239 lines
  - `IDebounceManager` - 156 lines
  - `IThrottleManager` - 196 lines
  - `ILazyCache` - 301 lines
  - `IAsyncActionExecutor<T>` - 277 lines

- **Enhanced IntelliSense Support**
  - Detailed parameter descriptions
  - Usage examples in XML `<example>` tags
  - Remarks for best practices
  - Exception documentation

#### Middleware Improvements

- **MiddlewarePipelineOptions**
  - Configurable error handling strategies
  - Retry logic with exponential backoff
  - Circuit breaker pattern support
  - Better control over middleware behavior

### 🔧 Improvements

#### Code Quality

- **30% Reduction in StoreComponent Code**
  - Before: 530 lines
  - After: 370 lines
  - Improvement: -160 lines (-30%)

- **25% Reduction in Store Code**
  - Before: 274 lines
  - After: 206 lines
  - Improvement: -68 lines (-25%)

#### Testability

- **Improved Unit Testing**
  - All dependencies mockable via interfaces
  - Easier to test components in isolation
  - Test helpers: `StoreTestHelpers` utility class
  - `RegisterStoreUtilities<T>()` for component tests

- **Test Coverage**
  - 326 total tests
  - 320 passing (98.2%)
  - Comprehensive integration tests for DI setup

#### Performance

- **Optimized Store Updates**
  - Better async/await patterns
  - Reduced allocations
  - Efficient subscription management

- **Thread Safety**
  - Proper async locking mechanisms
  - No thread pool starvation
  - Safe concurrent access

### 📚 Documentation

#### New Documentation Files

- **MIGRATION.md** (464 lines)
  - Complete upgrade guide from alpha versions
  - Step-by-step migration instructions
  - Before/after code examples
  - Troubleshooting common DI errors
  - Breaking changes explained in detail

- **docs/ARCHITECTURE_V2.md** (1,379 lines)
  - Comprehensive architecture overview
  - 15+ ASCII diagrams
  - 50+ code examples
  - SOLID principles explanation
  - Dependency injection flow
  - Before/after comparisons
  - Interface documentation

- **CHANGELOG.md** (this file)
  - Version history
  - Breaking changes documentation
  - Migration guides
  - Feature descriptions

#### Updated Documentation

- **README.md**
  - Updated Quick Start with DI registration
  - Convenience method examples
  - Troubleshooting section for DI errors
  - Best practices

- **CLAUDE.md**
  - Updated with DI architecture section
  - New store registration patterns
  - Interface segregation documentation
  - Component usage examples

### 🐛 Bug Fixes

- Fixed `IStore<T>` missing `IDisposable` interface exposure
- Fixed performance test thread pool starvation (changed from `Parallel.ForEach` to async `Task.WhenAll`)
- Fixed flaky timing tests with proper async synchronization
- Fixed race conditions in debounce/throttle tests

### 🏗️ Internal Changes

#### Extracted Components

- **SubscriptionManager<T>** (`ISubscriptionManager<T>`)
  - Observer pattern implementation
  - Subscription lifecycle management
  - Thread-safe subscription tracking

- **AsyncActionExecutor<T>** (`IAsyncActionExecutor<T>`)
  - Async action lifecycle management
  - Error handling and retry logic
  - Integration with store updates

- **Utility Services**
  - `DebounceManager` (`IDebounceManager`)
  - `ThrottleManager` (`IThrottleManager`)
  - `LazyCache` (`ILazyCache`)

#### Refactored Components

- **Store<T>**
  - Delegates subscription management to `ISubscriptionManager<T>`
  - Cleaner state update logic
  - Better separation of concerns

- **StoreComponent<T>**
  - Uses dependency injection for all utilities
  - Extracted async action handling to `IAsyncActionExecutor<T>`
  - Simplified component lifecycle

### 📦 Dependencies

No changes to external dependencies:
- Microsoft.AspNetCore.Components.Web 8.0.0
- System.Collections.Immutable 8.0.0
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0
- Microsoft.Extensions.Logging.Abstractions 8.0.0

### 🔄 Migration Path

#### For Existing Users (from alpha versions)

1. **Update Package**
   ```bash
   dotnet add package EasyAppDev.Blazor.Store --version 1.0.0
   ```

2. **Add Service Registration**
   ```csharp
   // In Program.cs
   builder.Services.AddStoreUtilities();
   builder.Services.AddAsyncActionExecutor<YourState>();
   ```

3. **Update Store Instantiation** (if using direct constructor)
   ```csharp
   // Change from:
   var store = new Store<MyState>(initialState);

   // To:
   var store = StoreBuilder<MyState>.Create(initialState).Build();
   ```

4. **Test Your Application**
   - Run your application
   - Check for DI-related errors
   - See MIGRATION.md for detailed troubleshooting

### 🙏 Acknowledgments

This release represents a significant architectural improvement based on:
- SOLID principles from Robert C. Martin
- Dependency injection patterns from ASP.NET Core
- State management patterns from Zustand (JavaScript)
- Testing best practices from the .NET community

### 📝 Notes

- This is a **breaking change release** requiring migration steps
- The new architecture provides significant long-term benefits:
  - Better testability
  - Improved modularity
  - SOLID compliance
  - Production-ready logging
  - Enhanced maintainability
- All existing functionality is preserved (just with different registration)
- Performance improvements in concurrent scenarios
- More robust error handling

---

## [0.1.0-alpha] - 2024-XX-XX

Initial alpha release with core functionality:
- Store pattern with immutable state
- StoreComponent base class for Blazor
- Redux DevTools integration
- LocalStorage/SessionStorage persistence
- Middleware pipeline
- Selector pattern with memoization
- Async action support

---

[1.0.0]: https://github.com/YOUR_USERNAME/EasyAppDev.Blazor.Store/releases/tag/v1.0.0
[0.1.0-alpha]: https://github.com/YOUR_USERNAME/EasyAppDev.Blazor.Store/releases/tag/v0.1.0-alpha
