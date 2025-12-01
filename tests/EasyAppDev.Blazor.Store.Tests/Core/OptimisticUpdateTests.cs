using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

public record CartItem(string Id, string Name, int Quantity);
public record CartState(IReadOnlyList<CartItem> Items, string? Error);

public class OptimisticUpdateTests : IDisposable
{
    private readonly IStore<CartState> _store;

    public OptimisticUpdateTests()
    {
        _store = StoreTestHelpers.CreateStore(new CartState(Array.Empty<CartItem>(), null));
    }

    [Fact]
    public async Task UpdateOptimistic_WithSuccessfulAction_AppliesOptimisticThenSuccess()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);
        var serverItem = new CartItem("server-1", "Test Item", 1);

        // Act
        await _store.UpdateOptimistic(
            optimistic: s => s with { Items = s.Items.Append(item).ToList() },
            action: async () =>
            {
                await Task.Delay(10); // Simulate server call
                return serverItem;
            },
            onSuccess: (s, result) => s with
            {
                Items = s.Items.Select(i => i.Id == item.Id ? result : i).ToList()
            },
            actionName: "ADD_ITEM"
        );

        // Assert
        var state = _store.GetState();
        state.Items.Should().HaveCount(1);
        state.Items[0].Id.Should().Be("server-1");
    }

    [Fact]
    public async Task UpdateOptimistic_WithFailedAction_RollsBackToPreOptimisticState()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);

        // Act
        try
        {
            await _store.UpdateOptimistic(
                optimistic: s => s with { Items = s.Items.Append(item).ToList() },
                action: async () =>
                {
                    await Task.Delay(10);
                    throw new Exception("Server error");
                },
                actionName: "ADD_ITEM"
            );
        }
        catch (Exception)
        {
            // Expected
        }

        // Assert - should have rolled back
        var state = _store.GetState();
        state.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateOptimistic_WithCustomRollback_UsesCustomRollback()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);

        // Pre-populate state
        await _store.UpdateAsync(s => s with { Items = new[] { item } });

        // Act
        try
        {
            await _store.UpdateOptimistic(
                optimistic: s => s with
                {
                    Items = s.Items.Select(i => i.Id == item.Id
                        ? i with { Quantity = i.Quantity + 1 }
                        : i).ToList()
                },
                action: async () =>
                {
                    await Task.Delay(10);
                    throw new Exception("Server error");
                },
                rollback: s => s with
                {
                    Items = s.Items.Select(i => i.Id == item.Id
                        ? i with { Quantity = 1 }
                        : i).ToList(),
                    Error = "Failed to update quantity"
                },
                actionName: "UPDATE_QUANTITY"
            );
        }
        catch
        {
            // Expected
        }

        // Assert - custom rollback should have been applied
        var state = _store.GetState();
        state.Items.Should().HaveCount(1);
        state.Items[0].Quantity.Should().Be(1);
        state.Error.Should().Be("Failed to update quantity");
    }

    [Fact]
    public async Task UpdateOptimistic_WithOnError_HandlesErrorWithoutThrowing()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);
        Exception? caughtException = null;

        // Act
        await _store.UpdateOptimistic(
            optimistic: s => s with { Items = s.Items.Append(item).ToList() },
            action: async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Server unavailable");
            },
            onError: (s, ex) =>
            {
                caughtException = ex;
                return s with { Error = ex.Message };
            },
            actionName: "ADD_ITEM"
        );

        // Assert - error handled, not thrown
        var state = _store.GetState();
        state.Items.Should().BeEmpty(); // Rolled back
        state.Error.Should().Be("Server unavailable");
        caughtException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateOptimistic_VoidAction_WorksWithSimpleActions()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);
        var serverCalled = false;

        // Act
        await _store.UpdateOptimistic(
            optimistic: s => s with { Items = s.Items.Append(item).ToList() },
            action: async () =>
            {
                await Task.Delay(10);
                serverCalled = true;
            },
            actionName: "ADD_ITEM"
        );

        // Assert
        var state = _store.GetState();
        state.Items.Should().HaveCount(1);
        serverCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOptimisticWithConfirm_ReturnsServerResult()
    {
        // Arrange
        var tempItem = new CartItem("temp-1", "New Item", 1);

        // Act
        var serverItem = await _store.UpdateOptimisticWithConfirm(
            optimistic: s => s with { Items = s.Items.Append(tempItem).ToList() },
            action: async () =>
            {
                await Task.Delay(10);
                return new CartItem("server-123", "New Item", 1);
            },
            confirm: (s, result) => s with
            {
                Items = s.Items.Select(i => i.Id == tempItem.Id ? result : i).ToList()
            },
            actionName: "CREATE_ITEM"
        );

        // Assert
        serverItem.Id.Should().Be("server-123");
        var state = _store.GetState();
        state.Items.Should().HaveCount(1);
        state.Items[0].Id.Should().Be("server-123");
    }

    [Fact]
    public async Task UpdateOptimisticWithConfirm_RollsBackOnFailure()
    {
        // Arrange
        var tempItem = new CartItem("temp-1", "New Item", 1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _store.UpdateOptimisticWithConfirm(
                optimistic: s => s with { Items = s.Items.Append(tempItem).ToList() },
                action: async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Create failed");
#pragma warning disable CS0162
                    return new CartItem("server-123", "New Item", 1);
#pragma warning restore CS0162
                },
                confirm: (s, result) => s with
                {
                    Items = s.Items.Select(i => i.Id == tempItem.Id ? result : i).ToList()
                }
            );
        });

        // State should be rolled back
        var state = _store.GetState();
        state.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateOptimistic_ImmediatelyAppliesOptimisticUpdate()
    {
        // Arrange
        var item = new CartItem("1", "Test Item", 1);
        CartState? capturedState = null;
        var optimisticApplied = new TaskCompletionSource<bool>();

        using var subscription = _store.Subscribe(s =>
        {
            if (s.Items.Any())
            {
                capturedState = s;
                optimisticApplied.TrySetResult(true);
            }
        });

        // Act - start optimistic update but don't await
        var updateTask = _store.UpdateOptimistic(
            optimistic: s => s with { Items = s.Items.Append(item).ToList() },
            action: async () =>
            {
                await Task.Delay(500); // Long server call
            }
        );

        // The optimistic update should be applied immediately
        await optimisticApplied.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.Items.Should().HaveCount(1);

        await updateTask;
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
