using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

public class StoreBuilderTests
{
    [Fact]
    public void Create_WithValidState_ReturnsBuilder()
    {
        // Act
        var builder = StoreBuilder<TestState>.Create(new TestState(0, "Test"));

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Build_CreatesStoreWithInitialState()
    {
        // Arrange
        var initialState = new TestState(5, "Initial");

        // Act
        var store = StoreBuilder<TestState>.Create(initialState).Build();

        // Assert
        store.GetState().Should().Be(initialState);
    }

    [Fact]
    public async Task WithComparer_SetsCustomComparer()
    {
        // Arrange
        var comparer = new TestStateComparer();
        var initialState = new TestState(0, "Test");

        // Act
        var store = StoreBuilder<TestState>
            .Create(initialState)
            .WithComparer(comparer)
            .Build();

        // Assert - comparer is used internally, verify no exception
        await store.UpdateAsync(state => state with { Counter = 1 });
        store.GetState().Counter.Should().Be(1);
    }

    private class TestStateComparer : IEqualityComparer<TestState>
    {
        public bool Equals(TestState? x, TestState? y)
        {
            if (x is null || y is null) return x == y;
            return x.Counter == y.Counter && x.Message == y.Message;
        }

        public int GetHashCode(TestState obj) =>
            HashCode.Combine(obj.Counter, obj.Message);
    }
}
