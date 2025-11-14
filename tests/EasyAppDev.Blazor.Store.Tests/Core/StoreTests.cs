using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

public record TestState(int Counter, string Message);

public class StoreTests : IDisposable
{
    private readonly IStore<TestState> _store;

    public StoreTests()
    {
        _store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
    }

    [Fact]
    public void GetState_ReturnsInitialState()
    {
        // Act
        var state = _store.GetState();

        // Assert
        state.Counter.Should().Be(0);
        state.Message.Should().Be("Initial");
    }

    [Fact]
    public void Update_WithValidUpdater_UpdatesState()
    {
        // Act
        _store.Update(state => state with { Counter = 1 });

        // Assert
        _store.GetState().Counter.Should().Be(1);
    }

    [Fact]
    public void Update_WithNullUpdater_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _store.Update(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_WhenUpdaterReturnsNull_ThrowsInvalidOperationException()
    {
        // Act
        Action act = () => _store.Update(_ => null!);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*returned null*");
    }

    [Fact]
    public void Subscribe_InvokesCallbackOnStateChange()
    {
        // Arrange
        var callbackInvoked = false;
        TestState? capturedState = null;

        using var subscription = _store.Subscribe(state =>
        {
            callbackInvoked = true;
            capturedState = state;
        });

        // Act
        _store.Update(state => state with { Counter = 1 });

        // Assert
        callbackInvoked.Should().BeTrue();
        capturedState.Should().NotBeNull();
        capturedState!.Counter.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenDisposed_StopsReceivingUpdates()
    {
        // Arrange
        var callCount = 0;
        var subscription = _store.Subscribe(_ => callCount++);

        _store.Update(state => state with { Counter = 1 });
        callCount.Should().Be(1);

        // Act
        subscription.Dispose();
        _store.Update(state => state with { Counter = 2 });

        // Assert
        callCount.Should().Be(1); // No additional invocation
    }

    [Fact]
    public void Subscribe_WithSelector_OnlyInvokesWhenSelectedValueChanges()
    {
        // Arrange
        var callCount = 0;
        using var subscription = _store.Subscribe(
            state => state.Counter,
            counter => callCount++);

        // Act
        _store.Update(state => state with { Message = "Changed" }); // Counter unchanged
        callCount.Should().Be(0);

        _store.Update(state => state with { Counter = 1 }); // Counter changed
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesStateAsynchronously()
    {
        // Arrange
        var updater = (TestState state) => state with { Counter = 5 };

        // Act
        await _store.UpdateAsync(updater);

        // Assert
        _store.GetState().Counter.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_WithAsyncUpdater_UpdatesState()
    {
        // Arrange
        var asyncUpdater = async (TestState state) =>
        {
            await Task.Delay(10);
            return state with { Counter = 10 };
        };

        // Act
        await _store.UpdateAsync(asyncUpdater);

        // Assert
        _store.GetState().Counter.Should().Be(10);
    }

    [Fact]
    public void Dispose_ClearsSubscriptions()
    {
        // Arrange
        var callCount = 0;
        _store.Subscribe(_ => callCount++);

        // Act
        _store.Dispose();
        Action act = () => _store.Update(state => state with { Counter = 1 });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
        callCount.Should().Be(0);
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
