using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.State;

/// <summary>
/// Tests for state methods pattern (Zustand-style).
/// </summary>
public class StateMethodsTests
{
    // Test state record with methods
    private record TestState(int Count, string? LastAction = null)
    {
        public TestState Increment() => this with
        {
            Count = Count + 1,
            LastAction = "INCREMENT"
        };

        public TestState Decrement() => this with
        {
            Count = Count - 1,
            LastAction = "DECREMENT"
        };

        public TestState IncrementBy(int amount) => this with
        {
            Count = Count + amount,
            LastAction = $"INCREMENT_BY_{amount}"
        };

        public TestState Reset() => this with
        {
            Count = 0,
            LastAction = "RESET"
        };
    }

    [Fact]
    public void Increment_ShouldIncrementCountBy1()
    {
        // Arrange
        var state = new TestState(5);

        // Act
        var newState = state.Increment();

        // Assert
        newState.Count.Should().Be(6);
        newState.LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public void Increment_ShouldNotMutateOriginalState()
    {
        // Arrange
        var state = new TestState(5);

        // Act
        var newState = state.Increment();

        // Assert
        state.Count.Should().Be(5); // Original unchanged
        newState.Count.Should().Be(6);
        ReferenceEquals(state, newState).Should().BeFalse();
    }

    [Fact]
    public void Decrement_ShouldDecrementCountBy1()
    {
        // Arrange
        var state = new TestState(5);

        // Act
        var newState = state.Decrement();

        // Assert
        newState.Count.Should().Be(4);
        newState.LastAction.Should().Be("DECREMENT");
    }

    [Theory]
    [InlineData(5, 15)]
    [InlineData(10, 20)]
    [InlineData(-3, 7)]
    public void IncrementBy_ShouldIncrementBySpecifiedAmount(int amount, int expected)
    {
        // Arrange
        var state = new TestState(10);

        // Act
        var newState = state.IncrementBy(amount);

        // Assert
        newState.Count.Should().Be(expected);
        newState.LastAction.Should().Be($"INCREMENT_BY_{amount}");
    }

    [Fact]
    public void Reset_ShouldResetCountToZero()
    {
        // Arrange
        var state = new TestState(42, "SOME_ACTION");

        // Act
        var newState = state.Reset();

        // Assert
        newState.Count.Should().Be(0);
        newState.LastAction.Should().Be("RESET");
    }

    [Fact]
    public void StateMethods_CanBeChained()
    {
        // Arrange
        var state = new TestState(0);

        // Act
        var finalState = state
            .Increment()
            .Increment()
            .IncrementBy(5)
            .Decrement();

        // Assert
        finalState.Count.Should().Be(6); // 0 + 1 + 1 + 5 - 1
    }
}
