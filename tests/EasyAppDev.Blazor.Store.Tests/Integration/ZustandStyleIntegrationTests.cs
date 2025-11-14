using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

public class ZustandStyleIntegrationTests
{
    // Test state with methods for integration testing
    private record CounterTestState(int Count, string? LastAction = null)
    {
        public CounterTestState Increment() => this with
        {
            Count = Count + 1,
            LastAction = "INCREMENT"
        };

        public CounterTestState Decrement() => this with
        {
            Count = Count - 1,
            LastAction = "DECREMENT"
        };

        public CounterTestState IncrementBy(int amount) => this with
        {
            Count = Count + amount,
            LastAction = $"INCREMENT_BY_{amount}"
        };

        public CounterTestState Reset() => this with
        {
            Count = 0,
            LastAction = "RESET"
        };
    }

    [Fact]
    public async Task Update_WithStateMethod_ShouldUpdateStore()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(0))
            .Build();

        // Act
        await store.UpdateAsync(s => s.Increment(), "INCREMENT");
        var state = store.GetState();

        // Assert
        state.Count.Should().Be(1);
        state.LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task Update_WithMultipleStateMethods_ShouldExecuteSequentially()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(0))
            .Build();

        // Act
        await store.UpdateAsync(s => s.Increment(), "INCREMENT");
        await store.UpdateAsync(s => s.IncrementBy(5), "INCREMENT_BY_5");
        await store.UpdateAsync(s => s.Decrement(), "DECREMENT");

        var state = store.GetState();

        // Assert
        state.Count.Should().Be(5); // 0 + 1 + 5 - 1
        state.LastAction.Should().Be("DECREMENT");
    }

    [Fact]
    public async Task Update_ShouldNotifySubscribers()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(0))
            .Build();

        var notificationCount = 0;
        CounterTestState? lastState = null;

        using var subscription = store.Subscribe(state =>
        {
            notificationCount++;
            lastState = state;
        });

        // Act
        await store.UpdateAsync(s => s.Increment());

        // Assert
        notificationCount.Should().Be(1);
        lastState.Should().NotBeNull();
        lastState!.Count.Should().Be(1);
    }

    [Fact]
    public async Task Update_WithoutActionName_ShouldWorkCorrectly()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(0))
            .Build();

        // Act
        await store.UpdateAsync(s => s.Increment());
        var state = store.GetState();

        // Assert
        state.Count.Should().Be(1);
        state.LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task StateMethod_ShouldMaintainImmutability()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(10))
            .Build();

        var originalState = store.GetState();

        // Act
        await store.UpdateAsync(s => s.Increment());
        var newState = store.GetState();

        // Assert
        originalState.Count.Should().Be(10);
        newState.Count.Should().Be(11);
        ReferenceEquals(originalState, newState).Should().BeFalse();
    }

    [Fact]
    public async Task ChainedStateMethods_ShouldWorkCorrectly()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(0))
            .Build();

        // Act
        await store.UpdateAsync(s => s.Increment().Increment().IncrementBy(5).Decrement());
        var state = store.GetState();

        // Assert
        state.Count.Should().Be(6); // 0 + 1 + 1 + 5 - 1
    }

    [Fact]
    public async Task Reset_ShouldResetToZero()
    {
        // Arrange
        var store = StoreBuilder<CounterTestState>
            .Create(new CounterTestState(100))
            .Build();

        // Act
        await store.UpdateAsync(s => s.Reset());
        var state = store.GetState();

        // Assert
        state.Count.Should().Be(0);
        state.LastAction.Should().Be("RESET");
    }
}
