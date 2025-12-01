# Coding Standards

> How we write code in this project

## General Principles

1. **Readability over cleverness** - Code is read more than written
2. **Consistency over preference** - Follow existing patterns
3. **Explicit over implicit** - Make intent clear

---

## Language & Framework

- **C# 12** with latest features
- **.NET 8.0** target framework
- **Nullable reference types** enabled
- **Warnings as errors** enabled

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
</PropertyGroup>
```

---

## Naming Conventions

### Types

| Kind | Convention | Example |
|------|------------|---------|
| Class | PascalCase | `StoreBuilder` |
| Interface | IPascalCase | `IStore<T>` |
| Record | PascalCase | `CounterState` |
| Enum | PascalCase | `ErrorLocation` |
| Generic Type | T + Name | `TState`, `TResult` |

### Members

| Kind | Convention | Example |
|------|------------|---------|
| Public method | PascalCase | `UpdateAsync` |
| Private method | PascalCase | `NotifySubscribers` |
| Property | PascalCase | `CurrentState` |
| Field (private) | _camelCase | `_subscriptionManager` |
| Parameter | camelCase | `initialState` |
| Local variable | camelCase | `newState` |
| Constant | PascalCase | `MaxRetries` |

### Files

| Kind | Convention | Example |
|------|------------|---------|
| Class file | ClassName.cs | `Store.cs` |
| Interface file | InterfaceName.cs | `IStore.cs` |
| Test file | ClassNameTests.cs | `StoreTests.cs` |

---

## Code Style

### Braces

Always use braces, even for single-line blocks:

```csharp
// Good
if (condition)
{
    DoSomething();
}

// Bad
if (condition)
    DoSomething();
```

### Line Length

Maximum 120 characters per line.

### Indentation

4 spaces, no tabs.

### Blank Lines

- One blank line between methods
- One blank line between logical sections
- No trailing blank lines

### Using Statements

Use file-scoped namespaces and global usings:

```csharp
// GlobalUsings.cs
global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;

// Store.cs
namespace EasyAppDev.Blazor.Store.Core;

public class Store<TState> { }
```

---

## Documentation

### XML Documentation

Required for all public APIs:

```csharp
/// <summary>
/// Updates the state asynchronously using the provided updater function.
/// </summary>
/// <param name="updater">Function that transforms current state to new state.</param>
/// <param name="action">Optional action name for debugging and DevTools.</param>
/// <returns>A task representing the async operation.</returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="updater"/> is null.
/// </exception>
/// <exception cref="ObjectDisposedException">
/// Thrown when the store has been disposed.
/// </exception>
/// <example>
/// <code>
/// await store.UpdateAsync(s => s with { Count = s.Count + 1 }, "INCREMENT");
/// </code>
/// </example>
public Task UpdateAsync(Func<TState, TState> updater, string? action = null)
```

### Comments

- Use sparingly - code should be self-documenting
- Explain "why", not "what"
- Keep comments up to date

```csharp
// Good: Explains why
// Notify subscribers AFTER releasing lock to prevent reentrancy deadlocks
if (shouldNotify)
{
    NotifySubscribers();
}

// Bad: Explains what (obvious from code)
// Check if should notify
if (shouldNotify)
{
    // Call notify subscribers method
    NotifySubscribers();
}
```

---

## Patterns

### Null Handling

Use null-conditional and null-coalescing operators:

```csharp
// Good
_logger?.LogInformation("State updated");
var name = user?.Name ?? "Unknown";

// Bad
if (_logger != null)
{
    _logger.LogInformation("State updated");
}
```

Use `ArgumentNullException.ThrowIfNull`:

```csharp
// Good
public void DoSomething(string value)
{
    ArgumentNullException.ThrowIfNull(value);
}

// Bad
public void DoSomething(string value)
{
    if (value == null)
        throw new ArgumentNullException(nameof(value));
}
```

### Async/Await

Always use `ConfigureAwait(false)` in library code:

```csharp
// Good
await _lock.WaitAsync().ConfigureAwait(false);
var result = await DoSomethingAsync().ConfigureAwait(false);

// Bad (in library code)
await _lock.WaitAsync();
var result = await DoSomethingAsync();
```

### Disposal

Implement `IDisposable` properly:

```csharp
public class Store<TState> : IStore<TState>, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _subscriptionManager.Clear();
        _subscriptionManager.Dispose();
        _lock.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Store<TState>));
    }
}
```

### Exception Handling

Be specific about exceptions:

```csharp
// Good
catch (JsonException ex)
{
    _logger?.LogWarning(ex, "Failed to deserialize state from key: {Key}", key);
    return null;
}

// Bad
catch (Exception ex)
{
    // Swallows everything
    _ = ex;
}
```

---

## State Design

### Records

Always use records for state:

```csharp
// Good
public record CounterState(int Count, string? LastAction = null)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
}

// Bad
public class CounterState
{
    public int Count { get; set; }  // Mutable!
}
```

### Collections

Always use immutable collections:

```csharp
// Good
public record TodoState(ImmutableList<Todo> Items);

// Bad
public record TodoState(List<Todo> Items);  // Mutable!
```

### State Methods

State methods must be pure:

```csharp
// Good: Pure function
public CounterState Increment() => this with { Count = Count + 1 };

// Bad: Side effect
public CounterState Increment()
{
    Console.WriteLine("Incrementing");  // Side effect!
    return this with { Count = Count + 1 };
}

// Bad: External dependency
public CounterState Increment(ILogger logger)  // Don't pass services
{
    logger.LogInformation("Incrementing");
    return this with { Count = Count + 1 };
}
```

---

## Testing

### Test Structure

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var state = new CounterState(5);

    // Act
    var newState = state.Increment();

    // Assert
    newState.Count.Should().Be(6);
    state.Count.Should().Be(5);  // Original unchanged
}
```

### Naming

- Test class: `{ClassUnderTest}Tests`
- Test method: `{MethodName}_{Scenario}_{ExpectedBehavior}`

```csharp
public class StoreTests
{
    [Fact]
    public void UpdateAsync_WithValidUpdater_UpdatesState() { }

    [Fact]
    public void UpdateAsync_WithNullUpdater_ThrowsArgumentNullException() { }

    [Fact]
    public async Task UpdateAsync_WhenDisposed_ThrowsObjectDisposedException() { }
}
```

### Assertions

Use FluentAssertions:

```csharp
// Good
result.Should().Be(expected);
list.Should().HaveCount(3);
action.Should().ThrowAsync<ArgumentNullException>();

// Bad
Assert.Equal(expected, result);
Assert.Equal(3, list.Count);
```

### Test Coverage

- All public APIs must be tested
- Edge cases must be covered
- Thread safety must be verified

---

## Performance

### Avoid Allocations

```csharp
// Good: Reuse array
private static readonly string[] EmptyStrings = Array.Empty<string>();

// Bad: Allocate every time
return new string[0];
```

### Use Spans for String Operations

```csharp
// Good
ReadOnlySpan<char> span = text.AsSpan();

// Bad (for hot paths)
string substring = text.Substring(0, 10);
```

### Lazy Initialization

```csharp
// Good
private Lazy<ExpensiveObject> _expensive = new(() => new ExpensiveObject());

// Or
private ExpensiveObject? _expensive;
private ExpensiveObject Expensive => _expensive ??= new ExpensiveObject();
```

---

## Security

### Input Validation

Validate all external inputs:

```csharp
public async Task<string?> LoadAsync(string key)
{
    ArgumentNullException.ThrowIfNull(key);

    if (string.IsNullOrWhiteSpace(key))
        throw new ArgumentException("Key cannot be empty", nameof(key));

    // Continue...
}
```

### Sensitive Data

Never log sensitive data:

```csharp
// Good
_logger?.LogInformation("User {UserId} logged in", user.Id);

// Bad
_logger?.LogInformation("User logged in with password {Password}", user.Password);
```

---

## File Organization

### Project Structure

```
src/EasyAppDev.Blazor.Store/
├── Core/                    # Core store implementation
├── Blazor/                  # Blazor-specific components
├── Middleware/              # Middleware system
├── DevTools/                # DevTools integration
├── Persistence/             # Persistence providers
├── Selectors/               # Selector pattern
├── AsyncActions/            # Async helpers
├── Utilities/               # Utility classes
└── Extensions/              # Extension methods
```

### Class Organization

```csharp
public class Store<TState> : IStore<TState>, IDisposable
{
    // 1. Fields
    private TState _state;
    private readonly SemaphoreSlim _lock;

    // 2. Constructors
    public Store(TState initialState) { }

    // 3. Properties
    public TState CurrentState => _state;

    // 4. Public methods
    public Task UpdateAsync(...) { }

    // 5. Interface implementations
    TState IStateReader<TState>.GetState() => _state;

    // 6. Private methods
    private void NotifySubscribers() { }

    // 7. Disposal
    public void Dispose() { }
}
```

---

## Git Conventions

### Commit Messages

```
<type>: <subject>

<body>

<footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `refactor`: Code refactoring
- `test`: Tests
- `chore`: Maintenance

Examples:
```
feat: add optimistic update support

Adds UpdateOptimistic method to IStore with automatic rollback on failure.

Closes #123
```

### Branch Names

- `feature/add-optimistic-updates`
- `fix/memory-leak-in-subscription`
- `docs/update-architecture`

---

[Back to Roadmap](ROADMAP.md)
