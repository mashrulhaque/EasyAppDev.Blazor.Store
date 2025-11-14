# EasyAppDev.Blazor.Store Sample Application

This sample application demonstrates the features and capabilities of **EasyAppDev.Blazor.Store**, a Zustand-inspired state management library for Blazor applications.

## Overview

The sample showcases real-world patterns and best practices for using the state management library, including:
- Basic state management
- Async operations
- State persistence
- Performance optimization
- Middleware usage
- Form validation
- Data tables with sorting/filtering
- Modal/dialog management
- Multi-step wizards
- Custom middleware
- Memoized selectors

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A modern web browser (Chrome, Firefox, Edge, or Safari)
- Optional: [Redux DevTools Browser Extension](https://github.com/reduxjs/redux-devtools) for time-travel debugging

### Running the Sample

1. Navigate to the sample directory:
   ```bash
   cd samples/EasyAppDev.Blazor.Store.Sample
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser and navigate to the URL shown in the console (typically `https://localhost:5001` or `http://localhost:5000`)

### Building the Sample

```bash
dotnet build
```

## Demo Pages

### Basic Examples

#### 1. Counter (`/counter`)
**Demonstrates:** Basic state management, DevTools integration
- Simple increment/decrement operations
- Action tracking
- Redux DevTools time-travel debugging

#### 2. Todo List (`/todos`)
**Demonstrates:** Immutable collections, filtering
- Add, remove, and toggle todos
- Filter by status (All, Active, Completed)
- ImmutableList operations

#### 3. User Profile (`/profile`)
**Demonstrates:** Async operations, loading states
- Async data loading
- Loading, success, and error states
- Error handling and retry logic

### Intermediate Examples

#### 4. Shopping Cart (`/cart`)
**Demonstrates:** State persistence with LocalStorage
- Add/remove items
- Apply discount codes
- Automatic save/restore
- Clear cart functionality

#### 5. Theme Settings (`/theme`)
**Demonstrates:** Selector optimization, granular subscriptions
- SelectorStoreComponent usage
- Prevents unnecessary re-renders
- Multiple independent selectors

#### 6. Debounce Demo (`/debounce-demo`)
**Demonstrates:** Debouncing and throttling
- Search with debounce
- Mouse tracking with throttle
- Performance optimization techniques

### Advanced Examples

#### 7. AsyncData Demo (`/async-data-demo`)
**Demonstrates:** AsyncData<T> wrapper pattern
- Type-safe async state management
- NotAsked, Loading, Success, Failure states
- Unified error handling

#### 8. ExecuteAsync Demo (`/execute-async-demo`)
**Demonstrates:** ExecuteAsync helper method
- Automatic loading/success/error handling
- Simplified async operations
- Built-in state transitions

#### 9. LazyLoad Demo (`/lazy-load-demo`)
**Demonstrates:** Lazy loading with caching
- Request deduplication
- Automatic cache management
- TTL-based cache expiration

#### 10. Form Validation (`/form-validation`)
**Demonstrates:** Form state management and validation
- Field-level validation
- Form-level validation
- Async validation (e.g., username availability)
- Error state management
- Form submission with loading states

#### 11. Data Table (`/data-table`)
**Demonstrates:** Complex data management
- Client-side sorting
- Multi-column filtering
- Pagination
- Row selection
- Bulk operations

#### 12. Modal/Dialog (`/modals`)
**Demonstrates:** Modal state management
- Opening/closing modals
- Data passing to modals
- Nested modals
- Confirmation dialogs
- Form modals with state

#### 13. Multi-Step Wizard (`/wizard`)
**Demonstrates:** Multi-step form management
- Step navigation state
- Data accumulation across steps
- Step validation
- Progress tracking
- Back/forward navigation

#### 14. Custom Middleware (`/middleware-demo`)
**Demonstrates:** Middleware extensibility
- Analytics tracking middleware
- Performance monitoring middleware
- State validation middleware
- Conditional middleware execution

#### 15. Derived State (`/derived-state`)
**Demonstrates:** Memoized selectors and computed state
- Creating memoized selectors
- Combining multiple selectors
- Performance optimization for expensive computations
- When to use selectors vs. computed properties

#### 16. Comprehensive Demo (`/comprehensive-demo`)
**Demonstrates:** All features together
- Combines multiple patterns
- Real-world complexity
- Best practices

### Diagnostics

#### Diagnostics Panel (`/diagnostics`)
**Demonstrates:** Debug tools and monitoring (DEBUG builds only)
- State update history
- Component render tracking
- Performance metrics
- Subscription management

## Key Concepts

### State as Records

States are defined as immutable C# records with transformation methods:

```csharp
public record CounterState(int Count, string? LastAction = null)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
    public CounterState Decrement() => this with { Count = Count - 1, LastAction = "DECREMENT" };
}
```

### StoreComponent Base Class

Components inherit from `StoreComponent<T>` for automatic subscription management:

```csharp
@page "/counter"
@inherits StoreComponent<CounterState>

<h1>Count: @State.Count</h1>
<button @onclick="@(() => Update(s => s.Increment()))">+</button>
```

### Store Registration

Stores are registered in `Program.cs` with fluent configuration:

```csharp
builder.Services.AddStoreUtilities();
builder.Services.AddAsyncActionExecutor<CounterState>();

builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithDefaults(sp, "Counter")
        .WithPersistence(sp, "counter-state")
);
```

## Project Structure

```
EasyAppDev.Blazor.Store.Sample/
├── Pages/                  # Demo pages
│   ├── Index.razor         # Home page with overview
│   ├── Counter.razor       # Basic counter example
│   ├── TodoList.razor      # Todo list with filtering
│   ├── UserProfile.razor   # Async operations
│   ├── ShoppingCart.razor  # State persistence
│   ├── ThemeSettings.razor # Selector optimization
│   ├── DebounceDemo.razor  # Debouncing/throttling
│   ├── AsyncDataDemo.razor # AsyncData<T> pattern
│   ├── ExecuteAsyncDemo.razor  # ExecuteAsync helper
│   ├── LazyLoadDemo.razor  # Lazy loading with cache
│   ├── FormValidation.razor    # Form validation
│   ├── DataTable.razor     # Data grid with sorting/filtering
│   ├── ModalDemo.razor     # Modal state management
│   ├── Wizard.razor        # Multi-step wizard
│   ├── CustomMiddleware.razor  # Middleware extensibility
│   ├── DerivedState.razor  # Memoized selectors
│   ├── ComprehensiveDemo.razor # All features
│   └── Diagnostics.razor   # Diagnostics panel
├── State/                  # State definitions
│   ├── CounterState.cs
│   ├── TodoState.cs
│   ├── UserProfileState.cs
│   ├── ShoppingCartState.cs
│   ├── ThemeState.cs
│   ├── DebounceState.cs
│   ├── AsyncDataDemoState.cs
│   ├── UserManagementState.cs
│   ├── ProductCatalogState.cs
│   ├── FormValidationState.cs
│   ├── DataTableState.cs
│   ├── ModalState.cs
│   ├── WizardState.cs
│   ├── CustomMiddlewareState.cs
│   ├── DerivedStateExample.cs
│   └── ComprehensiveDemoState.cs
├── Components/             # Reusable components
│   └── ...
├── Shared/                 # Shared layouts and components
│   ├── NavMenu.razor       # Navigation menu
│   └── MainLayout.razor    # Main layout
├── wwwroot/                # Static files
├── Program.cs              # Application entry point with store registration
└── README.md               # This file
```

## Learning Path

We recommend exploring the demos in this order:

1. **Counter** - Understand basic state management and DevTools
2. **Todo List** - Learn immutable collections and filtering
3. **User Profile** - Master async operations and loading states
4. **Shopping Cart** - Explore state persistence
5. **Theme Settings** - Learn performance optimization with selectors
6. **Debounce Demo** - Understand debouncing and throttling
7. **AsyncData Demo** - Master the AsyncData<T> pattern
8. **ExecuteAsync Demo** - Simplify async operations
9. **LazyLoad Demo** - Implement lazy loading with caching
10. **Form Validation** - Handle complex form state and validation
11. **Data Table** - Manage data grids with sorting and filtering
12. **Modal/Dialog** - Control modal state
13. **Multi-Step Wizard** - Build complex multi-step flows
14. **Custom Middleware** - Extend functionality with middleware
15. **Derived State** - Optimize with memoized selectors
16. **Comprehensive Demo** - See it all together
17. **Diagnostics** - Debug and monitor your stores

## Best Practices Demonstrated

### 1. Immutability
- Always use `with` expressions for state updates
- Use `ImmutableList<T>`, `ImmutableDictionary<K,V>` for collections
- Write pure state methods (no side effects)

### 2. State Methods
- Co-locate state transformation logic with state data
- Use descriptive verb names (`Increment`, `Toggle`, `Reset`)
- Keep methods pure and testable

### 3. Component Patterns
- **Inline updates** for simple operations: `@onclick="@(() => Update(s => s.Increment()))"`
- **Component methods** for validation/logic: `@onclick="IncrementIfValid"`
- **Ad-hoc inline** for flexibility: `@onclick="@(() => Update(s => s with { Count = newValue }))"`

### 4. Async Operations
- Use `ExecuteAsync` for common async patterns
- Use `AsyncData<T>` for type-safe async state
- Always handle loading, success, and error states

### 5. Performance
- Use `SelectorStoreComponent<T>` for granular re-rendering
- Implement memoized selectors for expensive computations
- Use debouncing for high-frequency updates
- Use throttling for continuous events

### 6. Testing
- Test state methods as pure functions
- Verify immutability in tests
- Test components with mocked stores

## Additional Resources

- [Main Documentation](../../docs/)
- [Architecture Guide](../../docs/ARCHITECTURE.md)
- [Coding Standards](../../docs/CODING_STANDARDS.md)
- [Testing Strategy](../../docs/TESTING_STRATEGY.md)
- [Library Source Code](../../src/EasyAppDev.Blazor.Store/)

## Redux DevTools

This sample is configured to work with the Redux DevTools browser extension:

1. Install the [Redux DevTools Extension](https://github.com/reduxjs/redux-devtools)
2. Run the sample application
3. Open Redux DevTools in your browser
4. Explore state changes, time-travel debugging, and action history

## Troubleshooting

### Port Already in Use

If you see an error about the port being in use, you can specify a different port:

```bash
dotnet run --urls="https://localhost:5002;http://localhost:5001"
```

### Redux DevTools Not Connecting

- Ensure the Redux DevTools extension is installed
- Check that the store is configured with `.WithDevTools("StoreName")`
- Verify you're running in a browser with the extension installed

### Build Errors

If you encounter build errors:

```bash
dotnet clean
dotnet restore
dotnet build
```

## Contributing

This sample application is part of the EasyAppDev.Blazor.Store project. Contributions are welcome!

## License

This sample application is part of the EasyAppDev.Blazor.Store library and shares the same license.
