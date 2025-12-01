# Testing Strategy

> How we ensure quality and prevent regressions

## Philosophy

1. **Tests are documentation** - Reading tests should explain behavior
2. **Fast feedback** - Tests should run quickly
3. **Isolation** - Tests should not affect each other
4. **Reliability** - No flaky tests allowed

---

## Test Pyramid

```
                    ┌─────────┐
                    │   E2E   │  5%
                   ─┴─────────┴─
                  │  Component  │  15%
                 ─┴─────────────┴─
                │    Integration   │  15%
               ─┴──────────────────┴─
              │        Unit          │  65%
             ─┴───────────────────────┴─
```

### Unit Tests (65%)
- State transformation methods
- Utility functions
- Selectors
- Pure logic

### Integration Tests (15%)
- Store + Middleware
- Store + Subscriptions
- DI registration

### Component Tests (15%)
- Blazor components with bUnit
- Component lifecycle
- User interactions

### E2E Tests (5%)
- Critical user flows
- Cross-browser (if applicable)

---

## Frameworks & Tools

| Tool | Purpose |
|------|---------|
| xUnit | Test runner |
| FluentAssertions | Assertion library |
| bUnit | Blazor component testing |
| Moq | Mocking (sparingly) |
| Verify | Snapshot testing |

---

## Test Structure

### File Organization

```
tests/EasyAppDev.Blazor.Store.Tests/
├── Core/
│   ├── StoreTests.cs
│   ├── StoreBuilderTests.cs
│   └── SubscriptionManagerTests.cs
├── Blazor/
│   ├── StoreComponentTests.cs
│   └── SelectorComponentTests.cs
├── Middleware/
│   ├── MiddlewarePipelineTests.cs
│   └── LoggingMiddlewareTests.cs
├── Integration/
│   ├── StoreMiddlewareIntegrationTests.cs
│   └── DependencyInjectionTests.cs
├── Persistence/
│   └── PersistenceMiddlewareTests.cs
├── AsyncActions/
│   ├── AsyncDataTests.cs
│   └── AsyncActionExecutorTests.cs
├── Selectors/
│   └── MemoizedSelectorTests.cs
├── Utilities/
│   ├── DebounceManagerTests.cs
│   ├── ThrottleManagerTests.cs
│   └── LazyCacheTests.cs
├── TestUtilities/
│   ├── StoreTestHelpers.cs
│   └── TestStates.cs
└── Performance/
    └── StorePerformanceTests.cs
```

### Naming Convention

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
// or
public async Task MethodName_Scenario_ExpectedBehavior()
```

Examples:
```csharp
public void Increment_WithInitialCountZero_ReturnsCountOne()
public void UpdateAsync_WithNullUpdater_ThrowsArgumentNullException()
public async Task Subscribe_WhenStateChanges_InvokesCallback()
public void GetState_WhenDisposed_ThrowsObjectDisposedException()
```

---

## Writing Tests

### Unit Test Pattern (AAA)

```csharp
[Fact]
public void Increment_ShouldIncreaseCountByOne()
{
    // Arrange
    var state = new CounterState(5);

    // Act
    var newState = state.Increment();

    // Assert
    newState.Count.Should().Be(6);
}
```

### Always Test Immutability

```csharp
[Fact]
public void Increment_ShouldNotMutateOriginalState()
{
    // Arrange
    var original = new CounterState(5);

    // Act
    var newState = original.Increment();

    // Assert
    original.Count.Should().Be(5);  // Original unchanged
    newState.Count.Should().Be(6);
    ReferenceEquals(original, newState).Should().BeFalse();
}
```

### Test Edge Cases

```csharp
[Fact]
public void UpdateAsync_WithNullUpdater_ThrowsArgumentNullException()
{
    // Arrange
    var store = StoreTestHelpers.CreateStore(new CounterState(0));

    // Act
    Func<Task> act = () => store.UpdateAsync(null!);

    // Assert
    act.Should().ThrowAsync<ArgumentNullException>()
        .WithParameterName("updater");
}

[Fact]
public void GetState_WhenDisposed_ThrowsObjectDisposedException()
{
    // Arrange
    var store = StoreTestHelpers.CreateStore(new CounterState(0));
    store.Dispose();

    // Act
    Action act = () => store.GetState();

    // Assert
    act.Should().Throw<ObjectDisposedException>();
}
```

### Test Async Behavior

```csharp
[Fact]
public async Task UpdateAsync_ShouldCompleteSuccessfully()
{
    // Arrange
    var store = StoreTestHelpers.CreateStore(new CounterState(0));

    // Act
    await store.UpdateAsync(s => s.Increment());

    // Assert
    store.GetState().Count.Should().Be(1);
}

[Fact]
public async Task UpdateAsync_WithSlowMiddleware_ShouldWait()
{
    // Arrange
    var middleware = new SlowMiddleware(delay: TimeSpan.FromMilliseconds(100));
    var store = StoreTestHelpers.CreateStore(new CounterState(0), middleware);

    // Act
    var sw = Stopwatch.StartNew();
    await store.UpdateAsync(s => s.Increment());
    sw.Stop();

    // Assert
    sw.ElapsedMilliseconds.Should().BeGreaterThan(90);
}
```

### Test Thread Safety

```csharp
[Fact]
public async Task UpdateAsync_ConcurrentUpdates_ShouldBeThreadSafe()
{
    // Arrange
    var store = StoreTestHelpers.CreateStore(new CounterState(0));
    var tasks = new List<Task>();

    // Act
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(store.UpdateAsync(s => s.Increment()));
    }
    await Task.WhenAll(tasks);

    // Assert
    store.GetState().Count.Should().Be(100);
}

[Fact]
public async Task Subscribe_ConcurrentSubscriptions_ShouldBeThreadSafe()
{
    // Arrange
    var store = StoreTestHelpers.CreateStore(new CounterState(0));
    var callbackCount = 0;
    var subscriptions = new List<IDisposable>();

    // Act - Subscribe from multiple threads
    var subscribeTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
    {
        var sub = store.Subscribe(_ => Interlocked.Increment(ref callbackCount));
        lock (subscriptions) subscriptions.Add(sub);
    }));
    await Task.WhenAll(subscribeTasks);

    // Update once
    await store.UpdateAsync(s => s.Increment());

    // Assert - All 50 subscribers should be notified
    callbackCount.Should().Be(50);

    // Cleanup
    subscriptions.ForEach(s => s.Dispose());
}
```

---

## Component Testing with bUnit

### Basic Component Test

```csharp
public class CounterComponentTests : TestContext
{
    [Fact]
    public void Counter_InitialRender_ShowsZero()
    {
        // Arrange
        Services.AddStore(new CounterState(0));
        Services.AddStoreUtilities();

        // Act
        var cut = RenderComponent<Counter>();

        // Assert
        cut.Find("p").TextContent.Should().Contain("0");
    }

    [Fact]
    public async Task Counter_ClickIncrement_ShowsOne()
    {
        // Arrange
        Services.AddStore(new CounterState(0));
        Services.AddStoreUtilities();
        var cut = RenderComponent<Counter>();

        // Act
        await cut.Find("button.increment").ClickAsync();

        // Assert
        cut.Find("p").TextContent.Should().Contain("1");
    }
}
```

### Testing State Updates

```csharp
[Fact]
public async Task Component_WhenStoreUpdates_ReRenders()
{
    // Arrange
    var store = new Store<CounterState>(
        new CounterState(0),
        new SubscriptionManager<CounterState>());
    Services.AddSingleton<IStore<CounterState>>(store);
    Services.AddStoreUtilities();

    var cut = RenderComponent<Counter>();
    cut.Find("p").TextContent.Should().Contain("0");

    // Act - Update store directly
    await store.UpdateAsync(s => s with { Count = 42 });

    // Assert - Component should re-render
    cut.WaitForAssertion(() =>
        cut.Find("p").TextContent.Should().Contain("42"));
}
```

---

## Integration Tests

### Store + Middleware

```csharp
[Fact]
public async Task Store_WithLoggingMiddleware_LogsActions()
{
    // Arrange
    var logs = new List<string>();
    var store = StoreBuilder<CounterState>
        .Create(new CounterState(0))
        .WithLogging(msg => logs.Add(msg))
        .Build();

    // Act
    await store.UpdateAsync(s => s.Increment(), "INCREMENT");

    // Assert
    logs.Should().ContainSingle(l => l.Contains("INCREMENT"));
}
```

### DI Integration

```csharp
[Fact]
public void AddStore_RegistersStoreAsSingleton()
{
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddStore(new CounterState(0));
    var provider = services.BuildServiceProvider();

    // Assert
    var store1 = provider.GetRequiredService<IStore<CounterState>>();
    var store2 = provider.GetRequiredService<IStore<CounterState>>();
    ReferenceEquals(store1, store2).Should().BeTrue();
}

[Fact]
public void AddScopedStore_RegistersStoreAsScoped()
{
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddScopedStore(new CounterState(0));
    var provider = services.BuildServiceProvider();

    // Assert
    IStore<CounterState> store1, store2;
    using (var scope1 = provider.CreateScope())
    {
        store1 = scope1.ServiceProvider.GetRequiredService<IStore<CounterState>>();
    }
    using (var scope2 = provider.CreateScope())
    {
        store2 = scope2.ServiceProvider.GetRequiredService<IStore<CounterState>>();
    }
    ReferenceEquals(store1, store2).Should().BeFalse();
}
```

---

## Test Helpers

### StoreTestHelpers

```csharp
public static class StoreTestHelpers
{
    public static IStore<TState> CreateStore<TState>(
        TState initialState,
        params IMiddleware<TState>[] middlewares)
        where TState : notnull
    {
        var subscriptionManager = new SubscriptionManager<TState>();
        return new Store<TState>(
            initialState,
            subscriptionManager,
            middlewares: middlewares);
    }

    public static IStore<TState> CreateStore<TState>(TState initialState)
        where TState : notnull
    {
        return CreateStore(initialState, Array.Empty<IMiddleware<TState>>());
    }
}
```

### Test State Records

```csharp
// tests/TestUtilities/TestStates.cs
public record CounterState(int Count, string? LastAction = null)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
    public CounterState Decrement() => this with { Count = Count - 1, LastAction = "DECREMENT" };
}

public record TodoState(ImmutableList<Todo> Items)
{
    public static TodoState Empty => new(ImmutableList<Todo>.Empty);

    public TodoState AddItem(Todo item) => this with { Items = Items.Add(item) };
    public TodoState RemoveItem(Guid id) => this with { Items = Items.RemoveAll(t => t.Id == id) };
}

public record Todo(Guid Id, string Title, bool IsComplete);
```

### Mock Middleware

```csharp
public class MockMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    public List<string?> BeforeActions { get; } = new();
    public List<string?> AfterActions { get; } = new();

    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        BeforeActions.Add(action);
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        AfterActions.Add(action);
        return Task.CompletedTask;
    }
}
```

---

## Performance Tests

```csharp
public class StorePerformanceTests
{
    [Fact]
    public async Task Update_10000Times_CompletesUnder1Second()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CounterState(0));

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            await store.UpdateAsync(s => s.Increment());
        }
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        store.GetState().Count.Should().Be(10000);
    }

    [Fact]
    public void MemoizedSelector_CacheHit_Is10xFaster()
    {
        // Arrange
        var state = new TodoState(Enumerable.Range(0, 1000)
            .Select(i => new Todo(Guid.NewGuid(), $"Todo {i}", false))
            .ToImmutableList());

        var selector = new MemoizedSelector<TodoState, int>(
            s => s.Items.Count(t => t.IsComplete));

        // Act - First call (cache miss)
        var sw1 = Stopwatch.StartNew();
        var result1 = selector.Select(state);
        sw1.Stop();

        // Act - Second call (cache hit)
        var sw2 = Stopwatch.StartNew();
        var result2 = selector.Select(state);
        sw2.Stop();

        // Assert
        sw2.ElapsedTicks.Should().BeLessThan(sw1.ElapsedTicks / 10);
    }
}
```

---

## Test Coverage

### Goals

| Area | Target |
|------|--------|
| Overall | > 80% |
| Core (Store, Builder) | > 95% |
| State Methods | 100% |
| Public APIs | 100% |

### Running Coverage

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# Or use the script
./scripts/test-coverage.sh
```

### Coverage Exclusions

```csharp
// Exclude from coverage
[ExcludeFromCodeCoverage]
public class DevToolsMiddleware  // Hard to test JS interop
```

---

## CI/CD Integration

### GitHub Actions

```yaml
- name: Test
  run: dotnet test --no-build --verbosity normal

- name: Test with Coverage
  run: dotnet test --collect:"XPlat Code Coverage"

- name: Upload Coverage
  uses: codecov/codecov-action@v3
```

### PR Requirements

- All tests must pass
- No decrease in coverage
- Performance tests within thresholds

---

## Best Practices

### Do

- Test one thing per test
- Use descriptive test names
- Test edge cases
- Test error conditions
- Test thread safety
- Keep tests fast
- Use test helpers for setup

### Don't

- Test implementation details
- Test private methods directly
- Use Thread.Sleep (use async)
- Share state between tests
- Write flaky tests
- Over-mock

---

[Back to Roadmap](ROADMAP.md)
