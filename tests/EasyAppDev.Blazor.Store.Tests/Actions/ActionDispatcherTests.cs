using EasyAppDev.Blazor.Store.Actions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Actions;

// Test state
public record CounterState(int Count, string? LastAction);

// Test actions
public record Increment : IAction;
public record Decrement : IAction;
public record IncrementBy(int Amount) : IAction;
public record SetCount(int Value) : IAction;
public record Reset : IAction;

// Typed actions for compile-time safety
public record TypedIncrement : IAction<CounterState>;

// Test reducer implementation
public class CounterReducer : IReducer<CounterState, Increment>
{
    public CounterState Reduce(CounterState state, Increment action)
        => state with { Count = state.Count + 1, LastAction = "INCREMENT" };
}

// Pattern matching reducer
public class PatternReducer : IReducer<CounterState>
{
    public CounterState Reduce(CounterState state, IAction action) => action switch
    {
        Reset => new CounterState(0, "RESET"),
        _ => state
    };
}

public class ActionDispatcherTests : IDisposable
{
    private readonly IStore<CounterState> _store;

    public ActionDispatcherTests()
    {
        _store = StoreTestHelpers.CreateStore(new CounterState(0, null));
    }

    [Fact]
    public async Task DispatchAsync_WithRegisteredReducer_UpdatesState()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<Increment>((s, a) => s with { Count = s.Count + 1, LastAction = "INCREMENT" });

        // Act
        await dispatcher.DispatchAsync(new Increment());

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(1);
        state.LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task DispatchAsync_WithPayloadAction_UsesPayloadValue()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<IncrementBy>((s, a) => s with { Count = s.Count + a.Amount, LastAction = $"INCREMENT_BY_{a.Amount}" });

        // Act
        await dispatcher.DispatchAsync(new IncrementBy(5));

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(5);
        state.LastAction.Should().Be("INCREMENT_BY_5");
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleReducers_HandlesAllActions()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<Increment>((s, a) => s with { Count = s.Count + 1 })
            .Register<Decrement>((s, a) => s with { Count = s.Count - 1 })
            .Register<SetCount>((s, a) => s with { Count = a.Value });

        // Act
        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new Decrement());
        await dispatcher.DispatchAsync(new SetCount(10));

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(10);
    }

    [Fact]
    public async Task DispatchAsync_WithClassReducer_InvokesReducer()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register(new CounterReducer());

        // Act
        await dispatcher.DispatchAsync(new Increment());

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(1);
        state.LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task DispatchAsync_WithPatternReducer_UsesPatternMatching()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<Increment>((s, a) => s with { Count = s.Count + 1 })
            .RegisterPattern(new PatternReducer());

        // Act
        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new Reset());

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(0);
        state.LastAction.Should().Be("RESET");
    }

    [Fact]
    public async Task DispatchAsync_WithFunctionalPatternReducer_UsesPatternMatching()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .RegisterPattern((state, action) => action switch
            {
                Increment => state with { Count = state.Count + 1 },
                Decrement => state with { Count = state.Count - 1 },
                IncrementBy a => state with { Count = state.Count + a.Amount },
                _ => state
            });

        // Act
        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new IncrementBy(4));
        await dispatcher.DispatchAsync(new Decrement());

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(4);
    }

    [Fact]
    public void CanHandle_WithRegisteredReducer_ReturnsTrue()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<Increment>((s, a) => s with { Count = s.Count + 1 });

        // Assert
        dispatcher.CanHandle<Increment>().Should().BeTrue();
        dispatcher.CanHandle<Reset>().Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WithPatternReducer_ReturnsTrueForAll()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .RegisterPattern((s, a) => s);

        // Assert - pattern reducers handle all actions
        dispatcher.CanHandle<Increment>().Should().BeTrue();
        dispatcher.CanHandle<Reset>().Should().BeTrue();
    }

    [Fact]
    public void Dispatch_FiresAndForgets()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<SetCount>((s, a) => s with { Count = a.Value });

        // Act - fire and forget
        dispatcher.Dispatch(new SetCount(42));

        // Wait a bit for the dispatch to complete
        Thread.Sleep(100);

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(42);
    }

    [Fact]
    public async Task CreateDispatcher_WithConfigureAction_RegistersReducers()
    {
        // Arrange & Act
        var dispatcher = _store.CreateDispatcher(d => d
            .Register<Increment>((s, a) => s with { Count = s.Count + 1 })
            .Register<Decrement>((s, a) => s with { Count = s.Count - 1 }));

        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new Increment());
        await dispatcher.DispatchAsync(new Decrement());

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WithStoreExtension_WorksWithInlineReducer()
    {
        // Act
        await _store.DispatchAsync(new IncrementBy(10), (s, a) => s with { Count = s.Count + a.Amount });

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(10);
    }

    [Fact]
    public async Task DispatchAsync_WithAsyncReducer_HandlesAsyncOperations()
    {
        // Arrange
        async Task<CounterState> AsyncReducer(CounterState state, SetCount action)
        {
            await Task.Delay(10); // Simulate async work
            return state with { Count = action.Value, LastAction = "ASYNC_SET" };
        }

        // Act
        await _store.DispatchAsync(new SetCount(99), AsyncReducer);

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(99);
        state.LastAction.Should().Be("ASYNC_SET");
    }

    [Fact]
    public async Task DispatchAsync_SetsActionNameFromActionType()
    {
        // Arrange
        string? capturedAction = null;
        var store = StoreBuilder<CounterState>.Create(new CounterState(0, null))
            .WithLogging(msg =>
            {
                if (msg.Contains("IncrementBy"))
                    capturedAction = "IncrementBy";
            })
            .Build();

        var dispatcher = store.CreateDispatcher()
            .Register<IncrementBy>((s, a) => s with { Count = s.Count + a.Amount });

        // Act
        await dispatcher.DispatchAsync(new IncrementBy(5));

        // Assert
        capturedAction.Should().Be("IncrementBy");

        store.Dispose();
    }

    [Fact]
    public async Task DispatchAsync_WithUnhandledAction_ThrowsInvalidOperationException()
    {
        // Arrange - per the documented IActionDispatcher contract, dispatching an
        // action with no registered typed or pattern reducer throws.
        // (Previously this was silently swallowed.)
        var dispatcher = _store.CreateDispatcher()
            .Register<Increment>((s, a) => s with { Count = s.Count + 1 });

        await dispatcher.DispatchAsync(new Increment());

        // Act
        var act = async () => await dispatcher.DispatchAsync(new Reset());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No reducer*Reset*");

        // State unchanged from previous
        _store.GetState().Count.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WithDerivedActionType_UsesBaseTypeReducer()
    {
        // Arrange - reducer registered for the base type must handle derived actions
        var dispatcher = _store.CreateDispatcher()
            .Register<BaseCountAction>((s, a) => s with { Count = s.Count + a.Amount, LastAction = "BASE" });

        // Act - dispatch a derived action instance
        await dispatcher.DispatchAsync(new DerivedCountAction(7));

        // Assert
        var state = _store.GetState();
        state.Count.Should().Be(7);
        state.LastAction.Should().Be("BASE");
    }

    [Fact]
    public void CanHandle_WithDerivedActionType_ReturnsTrueForBaseTypeReducer()
    {
        // Arrange
        var dispatcher = _store.CreateDispatcher()
            .Register<BaseCountAction>((s, a) => s with { Count = s.Count + a.Amount });

        // Assert
        dispatcher.CanHandle<DerivedCountAction>().Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAndDispatch_Concurrently_DoesNotThrow()
    {
        // Arrange - reads are now thread-safe (ConcurrentDictionary + copy-on-write)
        var dispatcher = _store.CreateDispatcher()
            .RegisterPattern((s, a) => a is Increment ? s with { Count = s.Count + 1 } : s);

        // Act - interleave registrations and dispatches from multiple threads
        var tasks = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => dispatcher.RegisterPattern((s, a) => s)));
            tasks.Add(dispatcher.DispatchAsync(new Increment()));
        }

        // Assert
        await Task.WhenAll(tasks);
        _store.GetState().Count.Should().Be(20);
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}

// Inheritance hierarchy for derived-action dispatch tests
public record BaseCountAction(int Amount) : IAction;
public record DerivedCountAction(int Amount) : BaseCountAction(Amount);
