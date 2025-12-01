# Phase 4: Advanced Features

> Version: 2.1.0 | Status: Complete | Risk: Medium

## Overview

Phase 4 adds powerful features for complex applications. All additions are backward compatible - no breaking changes.

**Goal:** Enable sophisticated state management patterns without complexity.

---

## Features

### 4.1 Optimistic Updates with Rollback

**Problem:**
Users see delayed feedback while waiting for server responses:
```csharp
// Current: User clicks, waits, then sees result
async Task AddToCart(Product product)
{
    await Update(s => s with { IsLoading = true });
    try
    {
        await api.AddToCartAsync(product.Id);
        await Update(s => s.AddItem(product));
    }
    finally
    {
        await Update(s => s with { IsLoading = false });
    }
}
```

**Solution:** Optimistic updates with automatic rollback.

**API Design:**
```csharp
async Task AddToCart(Product product)
{
    await Store.UpdateOptimistic(
        // Optimistic update - applied immediately
        optimistic: s => s.AddItem(product),

        // Server action - runs in background
        action: async () => await api.AddToCartAsync(product.Id),

        // Rollback - applied if action fails
        rollback: s => s.RemoveItem(product.Id),

        // Optional: Transform on success (e.g., add server-generated ID)
        onSuccess: (s, result) => s.UpdateItemId(product.Id, result.ServerId),

        // Optional: Custom error handling
        onError: (s, ex) => s with {
            Error = $"Failed to add {product.Name}: {ex.Message}"
        }
    );
}
```

**Simpler Overload:**
```csharp
// For simple cases where optimistic = inverse of rollback
await Store.UpdateOptimistic(
    s => s.AddItem(product),                    // optimistic
    async () => await api.AddToCartAsync(product.Id)  // action
);
// Auto-generates rollback by tracking state diff
```

**Implementation:**
```csharp
public static class OptimisticUpdateExtensions
{
    public static async Task UpdateOptimistic<TState, TResult>(
        this IStore<TState> store,
        Func<TState, TState> optimistic,
        Func<Task<TResult>> action,
        Func<TState, TState>? rollback = null,
        Func<TState, TResult, TState>? onSuccess = null,
        Func<TState, Exception, TState>? onError = null,
        string? actionName = null)
        where TState : notnull
    {
        var previousState = store.GetState();

        // Apply optimistic update immediately
        await store.UpdateAsync(optimistic, actionName ?? "OPTIMISTIC_UPDATE");

        try
        {
            var result = await action();

            if (onSuccess != null)
            {
                await store.UpdateAsync(s => onSuccess(s, result), $"{actionName}_SUCCESS");
            }
        }
        catch (Exception ex)
        {
            // Rollback on failure
            if (rollback != null)
            {
                await store.UpdateAsync(rollback, $"{actionName}_ROLLBACK");
            }
            else
            {
                // Auto-rollback to previous state
                await store.UpdateAsync(_ => previousState, $"{actionName}_ROLLBACK");
            }

            if (onError != null)
            {
                await store.UpdateAsync(s => onError(s, ex), $"{actionName}_ERROR");
            }
            else
            {
                throw;
            }
        }
    }
}
```

---

### 4.2 Built-in Undo/Redo

**Problem:**
Implementing undo/redo requires manual history tracking.

**Solution:** First-class undo/redo support.

**API Design:**

**Store Configuration:**
```csharp
builder.Services.AddStore(
    DocumentState.Empty,
    (store, sp) => store
        .WithHistory(options => options
            .MaxSize(50)                      // Keep last 50 states
            .ExcludeActions("CURSOR_MOVE")    // Don't track cursor changes
            .GroupActions(TimeSpan.FromMilliseconds(500))  // Group rapid changes
        )
);
```

**Component Usage:**
```csharp
@inherits StoreComponent<DocumentState>
@inject IStoreHistory<DocumentState> History

<button @onclick="History.Undo" disabled="@(!History.CanUndo)">Undo</button>
<button @onclick="History.Redo" disabled="@(!History.CanRedo)">Redo</button>
<span>@History.CurrentIndex / @History.Count</span>

@code {
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Subscribe to history changes
        History.OnHistoryChanged += () => InvokeAsync(StateHasChanged);
    }
}
```

**History Interface:**
```csharp
public interface IStoreHistory<TState> where TState : notnull
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    int CurrentIndex { get; }
    int Count { get; }

    Task Undo();
    Task Redo();
    Task GoTo(int index);
    void Clear();

    IReadOnlyList<HistoryEntry<TState>> Entries { get; }

    event Action? OnHistoryChanged;
}

public record HistoryEntry<TState>(
    TState State,
    string? Action,
    DateTime Timestamp);
```

**Implementation:**
```csharp
public class StoreHistory<TState> : IStoreHistory<TState>, IMiddleware<TState>
    where TState : notnull
{
    private readonly List<HistoryEntry<TState>> _history = new();
    private readonly HistoryOptions _options;
    private int _currentIndex = -1;
    private bool _isUndoRedo;

    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        if (_isUndoRedo) return Task.CompletedTask;
        if (_options.ExcludedActions.Contains(action)) return Task.CompletedTask;

        // Truncate forward history if we're not at the end
        if (_currentIndex < _history.Count - 1)
        {
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
        }

        // Add new entry
        _history.Add(new HistoryEntry<TState>(currentState, action, DateTime.UtcNow));
        _currentIndex = _history.Count - 1;

        // Enforce max size
        while (_history.Count > _options.MaxSize)
        {
            _history.RemoveAt(0);
            _currentIndex--;
        }

        OnHistoryChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async Task Undo()
    {
        if (!CanUndo) return;

        _isUndoRedo = true;
        try
        {
            _currentIndex--;
            await _store.UpdateAsync(_ => _history[_currentIndex].State, "UNDO");
            OnHistoryChanged?.Invoke();
        }
        finally
        {
            _isUndoRedo = false;
        }
    }
}
```

---

### 4.3 Type-Safe Actions/Events

**Problem:**
String-based action names are error-prone:
```csharp
await Update(s => s.Increment(), "INCREMNT");  // Typo!
```

**Solution:** Strongly-typed action records.

**API Design:**

**Define Actions:**
```csharp
// Actions as records
public abstract record CounterAction;
public record Increment : CounterAction;
public record Decrement : CounterAction;
public record IncrementBy(int Amount) : CounterAction;
public record Reset : CounterAction;

// With static helpers
public static class CounterActions
{
    public static Increment Increment() => new();
    public static Decrement Decrement() => new();
    public static IncrementBy IncrementBy(int amount) => new(amount);
}
```

**Dispatch Actions:**
```csharp
// Type-safe dispatch
await Store.Dispatch(new Increment());
await Store.Dispatch(new IncrementBy(5));
await Store.Dispatch(CounterActions.IncrementBy(5));
```

**Handle Actions (Reducer Pattern):**
```csharp
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store
        .WithReducer<Increment>((state, action) => state.Increment())
        .WithReducer<Decrement>((state, action) => state.Decrement())
        .WithReducer<IncrementBy>((state, action) => state.IncrementBy(action.Amount))
        .WithReducer<Reset>((state, action) => new CounterState(0))
);
```

**Or Pattern Matching:**
```csharp
.WithReducer((state, action) => action switch
{
    Increment => state.Increment(),
    Decrement => state.Decrement(),
    IncrementBy a => state.IncrementBy(a.Amount),
    Reset => new CounterState(0),
    _ => state
});
```

**DevTools Integration:**
Actions automatically serialize to DevTools with type name and payload.

---

### 4.4 Cross-Tab State Sync

**Problem:**
State changes in one tab don't reflect in other tabs.

**Solution:** BroadcastChannel API integration.

**API Design:**
```csharp
builder.Services.AddStore(
    CartState.Empty,
    (store, sp) => store
        .WithTabSync(options => options
            .Channel("cart-state")           // BroadcastChannel name
            .SyncActions("ADD_ITEM", "REMOVE_ITEM")  // Only sync specific actions
            .ExcludeActions("UI_STATE")      // Don't sync UI-only state
            .OnSyncReceived((state, source) =>
                logger.LogDebug("Received state from tab {Source}", source))
        )
);
```

**How It Works:**
1. Store update occurs in Tab A
2. TabSyncMiddleware serializes state + action
3. BroadcastChannel sends to all tabs
4. Other tabs receive and apply update
5. Conflict resolution: Last-write-wins or custom

**Implementation:**
```csharp
public class TabSyncMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private readonly IJSRuntime _js;
    private readonly string _channelName;
    private IJSObjectReference? _channel;
    private DotNetObjectReference<TabSyncMiddleware<TState>>? _dotNetRef;

    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        _channel = await _js.InvokeAsync<IJSObjectReference>(
            "eval",
            $@"(() => {{
                const channel = new BroadcastChannel('{_channelName}');
                channel.onmessage = (e) => {{
                    DotNet.invokeMethodAsync('{AssemblyName}', 'OnMessageReceived', e.data);
                }};
                return channel;
            }})()"
        );
    }

    public async Task OnAfterUpdateAsync(TState prev, TState current, string? action)
    {
        if (_isSyncUpdate) return;  // Don't broadcast received updates

        var message = new SyncMessage
        {
            TabId = _tabId,
            Action = action,
            State = JsonSerializer.Serialize(current)
        };

        await _channel.InvokeVoidAsync("postMessage", message);
    }

    [JSInvokable]
    public async Task OnMessageReceived(SyncMessage message)
    {
        if (message.TabId == _tabId) return;  // Ignore own messages

        _isSyncUpdate = true;
        try
        {
            var state = JsonSerializer.Deserialize<TState>(message.State);
            await _store.UpdateAsync(_ => state, $"SYNC_{message.Action}");
        }
        finally
        {
            _isSyncUpdate = false;
        }
    }
}
```

---

### 4.5 Source Generators for Boilerplate

**Problem:**
Repetitive code for state records:
```csharp
public record CounterState(int Count, string? LastAction)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
    public CounterState Decrement() => this with { Count = Count - 1, LastAction = "DECREMENT" };
    public CounterState IncrementBy(int amount) => this with { Count = Count + amount, LastAction = "INCREMENT_BY" };
    public CounterState SetCount(int value) => this with { Count = value, LastAction = "SET_COUNT" };
}
```

**Solution:** Source generators create setter methods.

**API Design:**
```csharp
[Store]
public partial record CounterState(int Count, string? LastAction);

// Generated:
public partial record CounterState
{
    public CounterState SetCount(int value) => this with { Count = value };
    public CounterState UpdateCount(Func<int, int> updater) => this with { Count = updater(Count) };
    public CounterState SetLastAction(string? value) => this with { LastAction = value };
}
```

**With Actions:**
```csharp
[Store(GenerateActions = true)]
public partial record CounterState(int Count);

// Generated actions:
public static partial class CounterStateActions
{
    public record SetCount(int Value);
}

// Generated reducer registration:
public static partial class CounterStateExtensions
{
    public static StoreBuilder<CounterState> WithGeneratedReducers(this StoreBuilder<CounterState> builder)
        => builder
            .WithReducer<CounterStateActions.SetCount>((s, a) => s.SetCount(a.Value));
}
```

**Advanced Attributes:**
```csharp
[Store]
public partial record TodoState(
    [property: Immutable] ImmutableList<Todo> Items,  // Generates Add/Remove/Update methods
    [property: Computed] int CompletedCount,           // Skipped in setters
    [property: Transient] bool IsLoading               // Not persisted
);

// Generated:
public partial record TodoState
{
    public TodoState AddItems(Todo item) => this with { Items = Items.Add(item) };
    public TodoState RemoveItems(Todo item) => this with { Items = Items.Remove(item) };
    public TodoState UpdateItems(int index, Func<Todo, Todo> updater)
        => this with { Items = Items.SetItem(index, updater(Items[index])) };
}
```

**Implementation:**
```csharp
[Generator]
public class StoreGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var records = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "EasyAppDev.Blazor.Store.StoreAttribute",
                predicate: (node, _) => node is RecordDeclarationSyntax,
                transform: (ctx, _) => GetRecordInfo(ctx))
            .Where(r => r != null);

        context.RegisterSourceOutput(records, GenerateSource);
    }

    private void GenerateSource(SourceProductionContext ctx, RecordInfo record)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"public partial record {record.Name}");
        sb.AppendLine("{");

        foreach (var prop in record.Properties)
        {
            // Generate SetX method
            sb.AppendLine($"    public {record.Name} Set{prop.Name}({prop.Type} value)");
            sb.AppendLine($"        => this with {{ {prop.Name} = value }};");

            // Generate UpdateX method
            sb.AppendLine($"    public {record.Name} Update{prop.Name}(Func<{prop.Type}, {prop.Type}> updater)");
            sb.AppendLine($"        => this with {{ {prop.Name} = updater({prop.Name}) }};");
        }

        sb.AppendLine("}");

        ctx.AddSource($"{record.Name}.g.cs", sb.ToString());
    }
}
```

---

## Implementation Priority

| Feature | Complexity | Impact | Priority |
|---------|------------|--------|----------|
| Optimistic Updates | Medium | High | 1 |
| Source Generators | High | High | 2 |
| Undo/Redo | Medium | Medium | 3 |
| Type-Safe Actions | Low | Medium | 4 |
| Tab Sync | Medium | Low | 5 |

---

## Testing Requirements

### Optimistic Updates
```csharp
[Fact]
public async Task OptimisticUpdate_ShouldRollbackOnFailure()
{
    var store = CreateStore(new CartState(Items: ImmutableList<Item>.Empty));
    var item = new Item("test");

    await store.UpdateOptimistic(
        optimistic: s => s.AddItem(item),
        action: () => throw new Exception("Server error"),
        rollback: s => s.RemoveItem(item.Id)
    );

    store.GetState().Items.Should().BeEmpty();
}
```

### Undo/Redo
```csharp
[Fact]
public async Task Undo_ShouldRestorePreviousState()
{
    var store = CreateStoreWithHistory(new CounterState(0));
    var history = GetHistory(store);

    await store.UpdateAsync(s => s.Increment());
    await store.UpdateAsync(s => s.Increment());

    store.GetState().Count.Should().Be(2);

    await history.Undo();
    store.GetState().Count.Should().Be(1);

    await history.Undo();
    store.GetState().Count.Should().Be(0);
}
```

### Tab Sync
```csharp
[Fact]
public async Task TabSync_ShouldBroadcastUpdates()
{
    // Requires browser automation (Playwright)
    var page1 = await Browser.NewPageAsync();
    var page2 = await Browser.NewPageAsync();

    await page1.GotoAsync("/counter");
    await page2.GotoAsync("/counter");

    await page1.ClickAsync("#increment");

    // Both pages should show 1
    await Expect(page1.Locator("#count")).ToHaveTextAsync("1");
    await Expect(page2.Locator("#count")).ToHaveTextAsync("1");
}
```

---

## Success Criteria

1. Optimistic updates feel instant
2. Undo/redo works like native apps
3. Actions are type-safe and discoverable
4. Tabs stay synchronized
5. Source generators reduce boilerplate by 50%+

---

[← Phase 3](PHASE_3_CORE_ENHANCEMENTS.md) | [Back to Roadmap](../ROADMAP.md) | [Phase 5 →](PHASE_5_KILLER_FEATURES.md)
