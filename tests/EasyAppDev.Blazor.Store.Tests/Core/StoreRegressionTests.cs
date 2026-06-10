using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

public record RegressionState(int Counter, string Message);

/// <summary>
/// Regression tests for core store bugs: after-update middleware rollback,
/// out-of-order notification skipping, disposed-subscription invocation,
/// dispose-under-in-flight-update, action-aware subscriptions, and hydration
/// of the library's own persisted (wrapped) format.
/// </summary>
public class StoreRegressionTests
{
    private sealed class FailingAfterUpdateMiddleware : IMiddleware<RegressionState>
    {
        public Task OnBeforeUpdateAsync(RegressionState currentState, string? action) => Task.CompletedTask;

        public Task OnAfterUpdateAsync(RegressionState previousState, RegressionState currentState, string? action) =>
            throw new InvalidOperationException("Update rejected by after-update middleware");
    }

    [Fact]
    public async Task AfterUpdateMiddlewareThrows_RollsBackState_AndDoesNotNotify()
    {
        // Arrange
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, "initial"))
            .WithMiddleware(new FailingAfterUpdateMiddleware())
            .ConfigureMiddleware(o =>
            {
                o.StopOnError = true;
                o.LogErrors = false;
            })
            .Build();

        var notified = false;
        store.Subscribe(_ => notified = true);

        // Act
        var act = async () => await store.UpdateAsync(s => s with { Counter = 1 });

        // Assert - exception propagates, state is rolled back, no subscriber is notified
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected by after-update middleware*");
        store.GetState().Counter.Should().Be(0, "because the rejected update must be rolled back");
        notified.Should().BeFalse("because a rejected update must not notify subscribers");
    }

    [Fact]
    public void NotifyAll_SkipsNotificationsWithOlderVersions()
    {
        // Arrange
        var manager = new SubscriptionManager<RegressionState>();
        var received = new List<int>();
        manager.Subscribe(s => received.Add(s.Counter), () => new RegressionState(0, ""));

        // Act - deliver version 2 first, then a stale version 1 (out-of-order delivery)
        manager.NotifyAll(new RegressionState(2, ""), version: 2, action: null);
        manager.NotifyAll(new RegressionState(1, ""), version: 1, action: null);

        // Assert - the stale notification must be skipped
        received.Should().Equal(2);
    }

    [Fact]
    public async Task DisposedSubscription_IsNotInvoked_EvenWhenInNotificationSnapshot()
    {
        // Arrange
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, "")).Build();

        IDisposable? secondSubscription = null;
        var secondInvoked = false;

        // First subscriber disposes the second during notification; the second is still
        // in the snapshot taken at the start of NotifyAll but must NOT be invoked.
        store.Subscribe(_ => secondSubscription?.Dispose());
        secondSubscription = store.Subscribe(_ => secondInvoked = true);

        // Act
        await store.UpdateAsync(s => s with { Counter = 1 });

        // Assert
        secondInvoked.Should().BeFalse("because disposed subscriptions must be skipped");
    }

    [Fact]
    public async Task Dispose_WhileUpdateInFlight_DoesNotThrowObjectDisposedException()
    {
        // Arrange
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, "")).Build();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var updateTask = store.UpdateAsync(async s =>
        {
            await gate.Task;
            return s with { Counter = 1 };
        });

        // Give the update time to acquire the lock
        await Task.Delay(50);

        // Act - dispose while the update is in flight, then let the update finish
        store.Dispose();
        gate.SetResult();

        // Assert - the in-flight update must complete without ObjectDisposedException
        var act = async () => await updateTask;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Subscribe_WithActionListener_ReceivesStateAndAction()
    {
        // Arrange
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, "")).Build();
        var received = new List<(int Counter, string? Action)>();

        store.Subscribe((state, action) => received.Add((state.Counter, action)));

        // Act
        await store.UpdateAsync(s => s with { Counter = 1 }, "INCREMENT");
        await store.UpdateAsync(s => s with { Counter = 2 });

        // Assert
        received.Should().Equal((1, "INCREMENT"), (2, null));
    }

    [Fact]
    public async Task WithHydratedStateAsync_ParsesWrappedPersistedFormat()
    {
        // Arrange - simulate the envelope written by options-based persistence
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var persisted = new RegressionState(42, "hydrated");
        var wrapper = new PersistedStateWrapper
        {
            State = JsonSerializer.Serialize(persisted, jsonOptions)
        };

        var provider = new InMemoryPersistenceProvider();
        provider.SetValue("wrapped-key", JsonSerializer.Serialize(wrapper));

        // Act
        var builder = await StoreBuilder<RegressionState>.Create(new RegressionState(0, "default"))
            .WithHydratedStateAsync(provider, "wrapped-key");
        var store = builder.Build();

        // Assert
        store.GetState().Should().Be(persisted);
    }

    [Fact]
    public async Task WithHydratedStateAsync_StillParsesLegacyRawFormat()
    {
        // Arrange - legacy format: the payload is the raw serialized state
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var persisted = new RegressionState(7, "legacy");

        var provider = new InMemoryPersistenceProvider();
        provider.SetValue("raw-key", JsonSerializer.Serialize(persisted, jsonOptions));

        // Act
        var builder = await StoreBuilder<RegressionState>.Create(new RegressionState(0, "default"))
            .WithHydratedStateAsync(provider, "raw-key");
        var store = builder.Build();

        // Assert
        store.GetState().Should().Be(persisted);
    }

    [Fact]
    public async Task UpdateOptimistic_NoOpOptimisticUpdate_PreservesServerException()
    {
        // Arrange - a comparer that treats all states as equal makes every update a no-op,
        // so the optimistic state is never committed. Rollback must then NOT report a
        // (spurious) concurrent modification; the original server exception must surface.
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, ""))
            .WithComparer(new AlwaysEqualComparer())
            .Build();

        // Act
        var act = async () => await store.UpdateOptimistic(
            optimistic: s => s with { Counter = 99 },
            action: () => Task.FromException(new TimeoutException("server timed out")));

        // Assert - the original server exception, not ConcurrentModificationException
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("server timed out");
    }

    [Fact]
    public async Task UpdateOptimisticWithConfirm_NoOpOptimisticUpdate_PreservesServerException()
    {
        // Arrange
        var store = StoreBuilder<RegressionState>.Create(new RegressionState(0, ""))
            .WithComparer(new AlwaysEqualComparer())
            .Build();

        // Act
        var act = async () => await store.UpdateOptimisticWithConfirm(
            optimistic: s => s with { Counter = 99 },
            action: () => Task.FromException<int>(new TimeoutException("server timed out")),
            confirm: (s, _) => s);

        // Assert - the original server exception, not ConcurrentModificationException
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("server timed out");
    }

    private sealed class AlwaysEqualComparer : IEqualityComparer<RegressionState>
    {
        public bool Equals(RegressionState? x, RegressionState? y) => true;
        public int GetHashCode(RegressionState obj) => 0;
    }
}
