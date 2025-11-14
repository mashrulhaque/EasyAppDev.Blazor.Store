using Bunit;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class FullIntegrationTests : TestContext
{
    [Fact]
    public void FullWorkflow_StoreRegistration_ComponentUsage()
    {
        // Arrange - Setup DI like a real app
        Services.AddStore(new TestState(0, "Hello"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act - Render component
        var cut = RenderComponent<IntegrationTestComponent>();

        // Assert initial state
        cut.Find("p").TextContent.Should().Be("Counter: 0");
        cut.Find("span").TextContent.Should().Be("Message: Hello");

        // Act - User interaction
        cut.Find("button").Click();

        // Assert state updated and UI re-rendered
        cut.Find("p").TextContent.Should().Be("Counter: 1");

        // Verify store actually updated
        var store = Services.GetRequiredService<IStore<TestState>>();
        store.GetState().Counter.Should().Be(1);
    }

    [Fact]
    public void MultipleComponents_ShareSameStore()
    {
        // Arrange
        Services.AddStore(new TestState(0, "Shared"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act
        var component1 = RenderComponent<IntegrationTestComponent>();
        var component2 = RenderComponent<IntegrationTestComponent>();

        // Assert - Both components show same initial state
        component1.Find("p").TextContent.Should().Be("Counter: 0");
        component2.Find("p").TextContent.Should().Be("Counter: 0");

        // Act - Update from component1
        component1.Find("button").Click();

        // Assert - Both components updated
        component1.Find("p").TextContent.Should().Be("Counter: 1");
        component2.Find("p").TextContent.Should().Be("Counter: 1");
    }

    [Fact]
    public void StoreWithConfiguration_WorksWithComponent()
    {
        // Arrange - Register store with custom comparer
        Services.AddStore(
            new TestState(0, "Test"),
            (builder, sp) => builder.WithComparer(new TestStateCounterComparer()));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act
        var cut = RenderComponent<IntegrationTestComponent>();
        var store = Services.GetRequiredService<IStore<TestState>>();

        // Change message (counter stays same)
        store.Update(s => s with { Message = "Changed" });

        // Assert - Component should not re-render due to custom comparer
        // (Custom comparer only checks Counter)
        cut.Find("span").TextContent.Should().Be("Message: Test");
    }

    [Fact]
    public void AsyncUpdates_WorkWithComponent()
    {
        // Arrange
        Services.AddStore(new TestState(0, "Async"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act
        var cut = RenderComponent<AsyncIntegrationTestComponent>();
        cut.Find("button").Click();

        // Assert - Wait for async update
        cut.WaitForAssertion(() =>
        {
            cut.Find("p").TextContent.Should().Be("Counter: 100");
        });

        var store = Services.GetRequiredService<IStore<TestState>>();
        store.GetState().Counter.Should().Be(100);
    }

    [Fact]
    public void ComponentDisposal_UnsubscribesFromStore()
    {
        // Arrange
        Services.AddStore(new TestState(0, "Test"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var cut = RenderComponent<IntegrationTestComponent>();
        var store = Services.GetRequiredService<IStore<TestState>>();

        // Act - Dispose component
        cut.Instance.Dispose();

        // Update store after component disposal
        store.Update(s => s with { Counter = 999 });

        // Assert - Component should not re-render (still shows old value)
        cut.Find("p").TextContent.Should().Be("Counter: 0");
    }

    [Fact]
    public void ScopedStore_DifferentInstancesPerScope()
    {
        // Arrange
        var rootServices = new ServiceCollection();
        rootServices.AddScopedStore(new TestState(0, "Scoped"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(rootServices);
        var provider = rootServices.BuildServiceProvider();

        // Act - Create two scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        // Configure test contexts with different scopes
        var context1 = new TestContext();
        context1.Services.AddFallbackServiceProvider(scope1.ServiceProvider);

        var context2 = new TestContext();
        context2.Services.AddFallbackServiceProvider(scope2.ServiceProvider);

        // Render components in different scopes
        var component1 = context1.RenderComponent<IntegrationTestComponent>();
        var component2 = context2.RenderComponent<IntegrationTestComponent>();

        // Act - Update in scope1
        component1.Find("button").Click();

        // Assert - Only component1 updated
        component1.Find("p").TextContent.Should().Be("Counter: 1");
        component2.Find("p").TextContent.Should().Be("Counter: 0");

        // Cleanup
        context1.Dispose();
        context2.Dispose();
    }

    private class IntegrationTestComponent : StoreComponent<TestState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");

            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Counter: {State.Counter}");
            builder.CloseElement();

            builder.OpenElement(3, "span");
            builder.AddContent(4, $"Message: {State.Message}");
            builder.CloseElement();

            builder.OpenElement(5, "button");
            builder.AddAttribute(6, "onclick", EventCallback.Factory.Create(this, Increment));
            builder.AddContent(7, "Increment");
            builder.CloseElement();

            builder.CloseElement();
        }

        private void Increment()
        {
            UpdateState(state => state with { Counter = state.Counter + 1 }, "Increment");
        }
    }

    private class AsyncIntegrationTestComponent : StoreComponent<TestState>
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
                await Task.Delay(50);
                return state with { Counter = state.Counter + 100 };
            }, "IncrementAsync");
        }
    }

    private class TestStateCounterComparer : IEqualityComparer<TestState>
    {
        public bool Equals(TestState? x, TestState? y) => x?.Counter == y?.Counter;
        public int GetHashCode(TestState obj) => obj.Counter.GetHashCode();
    }
}
