using Bunit;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class SelectorStoreComponentTests : TestContext
{
    private record TestState(int Counter, string Message, bool IsActive);

    [Fact]
    public async Task Component_ShouldRenderInitialState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(5, "Hello", true));
        Services.AddSingleton<IStore<TestState>>(store);

        // Act
        var cut = RenderComponent<TestSelectorComponent>();

        // Assert
        cut.Find("p").TextContent.Should().Be("Counter: 5");
    }

    [Fact]
    public async Task Component_OnlyReRendersWhenSelectedValueChanges()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial", false));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestSelectorComponent>();

        var renderCount = 0;
        cut.RenderCount.Should().Be(1); // Initial render

        // Act 1: Change Counter (selected property)
        await store.UpdateAsync(s => new TestState(1, "Initial", false));

        // Assert 1: Should re-render
        cut.WaitForAssertion(() => cut.Find("p").TextContent.Should().Be("Counter: 1"));
        cut.RenderCount.Should().Be(2);

        // Act 2: Change Message (NOT selected)
        await store.UpdateAsync(s => new TestState(1, "Changed", false));

        // Assert 2: Should NOT re-render
        Thread.Sleep(50); // Give it time to potentially re-render
        cut.RenderCount.Should().Be(2); // Still 2, no re-render

        // Act 3: Change IsActive (NOT selected)
        await store.UpdateAsync(s => new TestState(1, "Changed", true));

        // Assert 3: Should NOT re-render
        Thread.Sleep(50);
        cut.RenderCount.Should().Be(2); // Still 2, no re-render

        // Act 4: Change Counter again
        await store.UpdateAsync(s => new TestState(2, "Changed", true));

        // Assert 4: Should re-render
        cut.WaitForAssertion(() => cut.Find("p").TextContent.Should().Be("Counter: 2"));
        cut.RenderCount.Should().Be(3);
    }

    [Fact]
    public async Task Component_ShouldProvideFullStateAccess()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(5, "Hello", true));
        Services.AddSingleton<IStore<TestState>>(store);

        // Act
        var cut = RenderComponent<TestFullStateAccessComponent>();

        // Assert - Component should be able to access all properties
        cut.Find(".counter").TextContent.Should().Be("5");
        cut.Find(".message").TextContent.Should().Be("Hello");
        cut.Find(".active").TextContent.Should().Be("True");
    }

    [Fact]
    public async Task Component_WithMultiplePropertiesSelector_ShouldReRenderWhenAnySelectedPropertyChanges()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial", false));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestMultiplePropertiesComponent>();

        cut.RenderCount.Should().Be(1);

        // Act 1: Change Counter (selected)
        await store.UpdateAsync(s => new TestState(1, "Initial", false));

        // Assert 1: Should re-render
        cut.WaitForAssertion(() => cut.RenderCount.Should().Be(2));

        // Act 2: Change Message (selected)
        await store.UpdateAsync(s => new TestState(1, "Changed", false));

        // Assert 2: Should re-render
        cut.WaitForAssertion(() => cut.RenderCount.Should().Be(3));

        // Act 3: Change IsActive (NOT selected)
        await store.UpdateAsync(s => new TestState(1, "Changed", true));

        // Assert 3: Should NOT re-render
        Thread.Sleep(50);
        cut.RenderCount.Should().Be(3); // Still 3
    }

    [Fact]
    public async Task Component_ShouldUpdateSelectedProperty()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(5, "Hello", true));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestSelectorComponent>();

        // Act
        await store.UpdateAsync(s => new TestState(10, "Hello", true));

        // Assert - Selected property should be updated
        cut.WaitForAssertion(() =>
        {
            var instance = cut.Instance;
            var selectedProp = instance.GetType().GetProperty("Selected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var selectedValue = selectedProp?.GetValue(instance);
            selectedValue.Should().Be(10);
        });
    }

    [Fact]
    public async Task Component_ShouldSupportUpdateMethods()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial", false));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestWithUpdateComponent>();

        // Act
        cut.Find("button").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            store.GetState().Counter.Should().Be(1);
        });
    }

    [Fact]
    public async Task Component_ShouldDisposeSubscriptionOnDispose()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial", false));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestSelectorComponent>();

        // Act - Dispose the component
        cut.Instance.Dispose();

        // Update store
        await store.UpdateAsync(s => new TestState(1, "Changed", false));

        // Assert - Component should not receive updates after disposal
        Thread.Sleep(50);
        // If subscription wasn't disposed, this would fail with ObjectDisposedException
        // The fact that we reach here without exception means it worked
    }

    [Fact]
    public async Task Component_WithComputedSelector_ShouldReRenderOnlyWhenComputedValueChanges()
    {
        // Arrange - Start with even number
        var store = StoreTestHelpers.CreateStore(new TestState(4, "Test", true));
        Services.AddSingleton<IStore<TestState>>(store);
        var cut = RenderComponent<TestComputedSelectorComponent>();

        var initialRenderCount = cut.RenderCount;

        // Act 1: Change Counter from 4 to 6 (both even)
        await store.UpdateAsync(s => new TestState(6, "Test", true));

        // Assert 1: Should NOT re-render (both are even)
        Thread.Sleep(50);
        cut.RenderCount.Should().Be(initialRenderCount);

        // Act 2: Change Counter from 6 to 7 (even to odd)
        await store.UpdateAsync(s => new TestState(7, "Test", true));

        // Assert 2: Should re-render (parity changed)
        cut.WaitForAssertion(() => cut.RenderCount.Should().Be(initialRenderCount + 1));

        // Act 3: Change Counter from 7 to 9 (both odd)
        await store.UpdateAsync(s => new TestState(9, "Test", true));

        // Assert 3: Should NOT re-render (both are odd)
        Thread.Sleep(50);
        cut.RenderCount.Should().Be(initialRenderCount + 1);
    }

    [Fact]
    public void Component_ReRendersWhenParameterChanges_EvenIfSelectedValueUnchanged()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(5, "Hello", true));
        Services.AddSingleton<IStore<TestState>>(store);

        var cut = RenderComponent<TestParameterizedSelectorComponent>(ps => ps
            .Add(p => p.Label, "First"));

        cut.Find("p").TextContent.Should().Be("First: 5");

        // Act - change ONLY the parameter; the store-selected value (Counter) is unchanged
        cut.SetParametersAndRender(ps => ps.Add(p => p.Label, "Second"));

        // Assert - the component must re-render with the new parameter value
        cut.Find("p").TextContent.Should().Be("Second: 5");
    }

    [Fact]
    public void Component_ReRendersOnEventHandler_EvenIfSelectedValueUnchanged()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(5, "Hello", true));
        Services.AddSingleton<IStore<TestState>>(store);

        var cut = RenderComponent<TestLocalStateSelectorComponent>();
        cut.Find("span").TextContent.Should().Be("Clicks: 0");

        // Act - click only mutates component-local state, not the store
        cut.Find("button").Click();

        // Assert - event-handler re-render must not be suppressed
        cut.Find("span").TextContent.Should().Be("Clicks: 1");
    }

    // Test Components

    private class TestParameterizedSelectorComponent : SelectorStoreComponent<TestState>
    {
        [Parameter] public string Label { get; set; } = string.Empty;

        protected override object SelectState(TestState state) => state.Counter;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, $"{Label}: {State.Counter}");
            builder.CloseElement();
        }
    }

    private class TestLocalStateSelectorComponent : SelectorStoreComponent<TestState>
    {
        private int _clicks;

        protected override object SelectState(TestState state) => state.Counter;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, () => _clicks++));
            builder.AddContent(2, "Click");
            builder.CloseElement();

            builder.OpenElement(3, "span");
            builder.AddContent(4, $"Clicks: {_clicks}");
            builder.CloseElement();
        }
    }

    private class TestSelectorComponent : SelectorStoreComponent<TestState>
    {
        protected override object SelectState(TestState state) => state.Counter;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, $"Counter: {State.Counter}");
            builder.CloseElement();
        }
    }

    private class TestFullStateAccessComponent : SelectorStoreComponent<TestState>
    {
        protected override object SelectState(TestState state) => state.Counter;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");

            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "class", "counter");
            builder.AddContent(3, State.Counter.ToString());
            builder.CloseElement();

            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", "message");
            builder.AddContent(6, State.Message);
            builder.CloseElement();

            builder.OpenElement(7, "span");
            builder.AddAttribute(8, "class", "active");
            builder.AddContent(9, State.IsActive.ToString());
            builder.CloseElement();

            builder.CloseElement();
        }
    }

    private class TestMultiplePropertiesComponent : SelectorStoreComponent<TestState>
    {
        protected override object SelectState(TestState state) => new { state.Counter, state.Message };

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, $"{State.Counter}: {State.Message}");
            builder.CloseElement();
        }
    }

    private class TestWithUpdateComponent : SelectorStoreComponent<TestState>
    {
        protected override object SelectState(TestState state) => state.Counter;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, Increment));
            builder.AddContent(2, "Increment");
            builder.CloseElement();
        }

        private void Increment()
        {
            Update(s => new TestState(s.Counter + 1, s.Message, s.IsActive));
        }
    }

    private class TestComputedSelectorComponent : SelectorStoreComponent<TestState>
    {
        // Select whether the counter is even or odd
        protected override object SelectState(TestState state) => state.Counter % 2 == 0;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, $"Is Even: {(bool)Selected!}");
            builder.CloseElement();
        }
    }
}
