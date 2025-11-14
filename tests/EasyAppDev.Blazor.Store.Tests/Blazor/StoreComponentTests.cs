using Bunit;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public record TestState(int Counter, string Message);

public class StoreComponentTests : TestContext
{
    [Fact]
    public void Component_InitializesWithStoreState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act
        var cut = RenderComponent<TestStoreComponent>();

        // Assert
        cut.Find("p").TextContent.Should().Be("Counter: 0");
    }

    [Fact]
    public void Component_ReRendersWhenStateChanges()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<TestStoreComponent>();

        // Act
        store.Update(state => state with { Counter = 5 });

        // Assert
        cut.Find("p").TextContent.Should().Be("Counter: 5");
    }

    [Fact]
    public void UpdateState_UpdatesStore()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<TestStoreComponent>();

        // Act
        cut.Find("button").Click();

        // Assert
        store.GetState().Counter.Should().Be(1);
        cut.Find("p").TextContent.Should().Be("Counter: 1");
    }

    [Fact]
    public void Component_DisposesSubscriptionOnDisposal()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<TestStoreComponent>();

        // Act
        cut.Instance.Dispose();
        store.Update(state => state with { Counter = 10 });

        // Assert - component should not re-render after disposal
        cut.Find("p").TextContent.Should().Be("Counter: 0");
    }

    [Fact]
    public void UpdateStateAsync_UpdatesStoreAsynchronously()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<TestAsyncStoreComponent>();

        // Act
        cut.Find("button").Click();

        // Wait for async operation to complete
        cut.WaitForAssertion(() =>
        {
            store.GetState().Counter.Should().Be(5);
            cut.Find("p").TextContent.Should().Be("Counter: 5");
        });
    }

    [Fact]
    public void SubscribeToSelector_OnlyReRendersWhenSelectedValueChanges()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
        Services.AddSingleton<IStore<TestState>>(store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<TestSelectorComponent>();
        var renderCount = cut.RenderCount;

        // Act - Change message only (not counter)
        store.Update(state => state with { Message = "Changed" });

        // Assert - should not trigger re-render since selector watches Counter
        cut.RenderCount.Should().Be(renderCount);

        // Act - Change counter
        store.Update(state => state with { Counter = 1 });

        // Assert - should trigger re-render
        cut.RenderCount.Should().BeGreaterThan(renderCount);
        cut.Find("p").TextContent.Should().Be("Counter: 1");
    }

    private class TestStoreComponent : StoreComponent<TestState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Counter: {State.Counter}");
            builder.CloseElement();
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, Increment));
            builder.AddContent(5, "Increment");
            builder.CloseElement();
            builder.CloseElement();
        }

        private void Increment()
        {
            UpdateState(state => state with { Counter = state.Counter + 1 });
        }
    }

    private class TestAsyncStoreComponent : StoreComponent<TestState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Counter: {State.Counter}");
            builder.CloseElement();
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, IncrementAsync));
            builder.AddContent(5, "Increment Async");
            builder.CloseElement();
            builder.CloseElement();
        }

        private async Task IncrementAsync()
        {
            await UpdateStateAsync(async state =>
            {
                await Task.Delay(10);
                return state with { Counter = state.Counter + 5 };
            });
        }
    }

    private class TestSelectorComponent : StoreComponent<TestState>
    {
        private IDisposable? _selectorSubscription;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Counter: {State.Counter}");
            builder.CloseElement();
            builder.CloseElement();
        }

        protected override void SubscribeToStore()
        {
            // Override to use selector instead of subscribing to all changes
            _selectorSubscription = SubscribeToSelector(
                state => state.Counter,
                counter => { /* Callback is optional */ });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectorSubscription?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
