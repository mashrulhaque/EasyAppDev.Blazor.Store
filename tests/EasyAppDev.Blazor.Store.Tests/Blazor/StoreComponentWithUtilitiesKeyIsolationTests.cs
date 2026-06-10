using Bunit;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

/// <summary>
/// Verifies that debounce/throttle keys are unique per component INSTANCE.
/// Previously keys were "{TypeName}_{action}", so two instances of the same component
/// type cancelled/replaced each other's pending operations on the circuit-scoped
/// managers, and disposing one instance cancelled the other's pending operations.
/// </summary>
public class StoreComponentWithUtilitiesKeyIsolationTests : TestContext
{
    private record TestState(int Counter, string Message);

    private readonly IStore<TestState> _store;

    public StoreComponentWithUtilitiesKeyIsolationTests()
    {
        _store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(_store);
        Services.AddStoreUtilities();
    }

    [Fact]
    public async Task UpdateDebounced_TwoInstancesOfSameComponentType_DoNotCancelEachOther()
    {
        // Arrange - two instances of the SAME component type sharing the same
        // circuit-scoped DebounceManager
        var cut1 = RenderComponent<DebouncedTestComponent>();
        var cut2 = RenderComponent<DebouncedTestComponent>();

        // Act - both instances schedule a debounced update with the SAME action name.
        // With type-only keys, the second call would cancel the first instance's
        // pending operation and only one increment would be applied.
        await cut1.Instance.IncrementDebounced(1);
        await cut2.Instance.IncrementDebounced(2);

        // Assert - both increments must be applied
        await WaitUntilAsync(() => _store.GetState().Counter == 3);
        _store.GetState().Counter.Should().Be(3);
    }

    [Fact]
    public async Task UpdateDebounced_DisposingOneInstance_DoesNotCancelOtherInstancesPendingOperation()
    {
        // Arrange
        var cut1 = RenderComponent<DebouncedTestComponent>();
        var cut2 = RenderComponent<DebouncedTestComponent>();

        // Act - instance 1 schedules a pending operation, then instance 2 is disposed.
        // With type-only keys, instance 2's disposal would cancel instance 1's pending op.
        await cut1.Instance.IncrementDebounced(5);
        cut2.Instance.Dispose();

        // Assert - instance 1's pending operation still fires
        await WaitUntilAsync(() => _store.GetState().Counter == 5);
        _store.GetState().Counter.Should().Be(5);
    }

    [Fact]
    public async Task UpdateThrottled_TwoInstancesOfSameComponentType_BothExecute()
    {
        // Arrange
        var cut1 = RenderComponent<ThrottledTestComponent>();
        var cut2 = RenderComponent<ThrottledTestComponent>();

        // Act - with type-only keys, the second instance's call would be throttled
        // away because the first instance's call started the shared interval.
        await cut1.Instance.IncrementThrottled(1);
        await cut2.Instance.IncrementThrottled(2);

        // Assert - both updates must be applied (each instance has its own interval)
        await WaitUntilAsync(() => _store.GetState().Counter == 3);
        _store.GetState().Counter.Should().Be(3);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var start = Environment.TickCount;
        while (!condition() && Environment.TickCount - start < timeoutMs)
        {
            await Task.Delay(10);
        }
    }

    private class DebouncedTestComponent : StoreComponentWithUtilities<TestState>
    {
        public Task IncrementDebounced(int amount)
            => UpdateDebounced(s => s with { Counter = s.Counter + amount }, 50, "INCREMENT");

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, State.Counter);
            builder.CloseElement();
        }
    }

    private class ThrottledTestComponent : StoreComponentWithUtilities<TestState>
    {
        public Task IncrementThrottled(int amount)
            => UpdateThrottled(s => s with { Counter = s.Counter + amount }, 50, "INCREMENT");

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, State.Counter);
            builder.CloseElement();
        }
    }
}
