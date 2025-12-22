using Bunit;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

public record CounterState(int Count, string LastAction);

public record TestState(int Counter, string Message);

/// <summary>
/// Comprehensive integration tests that verify complete workflows across
/// multiple components, middleware, and the full application stack.
/// </summary>
public class FullWorkflowTests : TestContext
{
    [Fact]
    public void FullWorkflow_StoreCreation_Middleware_ComponentIntegration()
    {
        // Arrange - Setup complete stack
        var logs = new List<string>();

        Services.AddStore(
            new CounterState(0, "INIT"),
            (builder, sp) => builder
                .WithLogging(log => logs.Add(log)));
        StoreTestHelpers.RegisterStoreUtilities<CounterState>(Services);

        // Act - Render component and interact
        var cut = RenderComponent<IntegrationTestComponent>();

        // Assert initial state
        cut.Find("p").TextContent.Should().Contain("Count: 0");

        // Act - User interaction
        cut.Find("button").Click();

        // Assert - State updated, middleware logged, component re-rendered
        cut.Find("p").TextContent.Should().Contain("Count: 1");
        logs.Should().Contain(log => log.Contains("INCREMENT"));

        // Verify store state
        var store = Services.GetRequiredService<IStore<CounterState>>();
        store.GetState().Count.Should().Be(1);
        store.GetState().LastAction.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task MultiStore_Workflow()
    {
        // Arrange - Multiple stores
        Services.AddStore(new CounterState(0, "INIT"));
        StoreTestHelpers.RegisterStoreUtilities<CounterState>(Services);
        Services.AddStore(new TestState(0, "Test"));
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);

        // Act
        var counterStore = Services.GetRequiredService<IStore<CounterState>>();
        var testStore = Services.GetRequiredService<IStore<TestState>>();

        await counterStore.UpdateAsync(state => state with { Count = 5 });
        await testStore.UpdateAsync(state => state with { Counter = 10 });

        // Assert - Both stores independent
        counterStore.GetState().Count.Should().Be(5);
        testStore.GetState().Counter.Should().Be(10);
    }

    [Fact]
    public async Task CompleteWorkflow_WithMultipleMiddleware()
    {
        // Arrange - Setup store with multiple middleware
        var logs = new List<string>();
        var beforeUpdateCount = 0;
        var afterUpdateCount = 0;

        Services.AddStore(
            new CounterState(0, "INIT"),
            (builder, sp) => builder
                .WithLogging(log => logs.Add(log))
                .WithMiddleware(new CustomTrackingMiddleware(
                    () => beforeUpdateCount++,
                    () => afterUpdateCount++)));

        var store = Services.GetRequiredService<IStore<CounterState>>();

        // Act - Pass action name explicitly so it appears in logs
        await store.UpdateAsync(state => state with { Count = 1, LastAction = "TEST" }, action: "TEST");

        // Assert - All middleware executed
        logs.Should().Contain(log => log.Contains("TEST"));
        beforeUpdateCount.Should().Be(1);
        afterUpdateCount.Should().Be(1);
        store.GetState().Count.Should().Be(1);
    }

    [Fact]
    public async Task AsyncWorkflow_CompleteChain()
    {
        // Arrange
        var asyncMiddleware = new AsyncLoggingMiddleware();
        Services.AddStore(
            new CounterState(0, "INIT"),
            (builder, sp) => builder.WithMiddleware(asyncMiddleware));

        var store = Services.GetRequiredService<IStore<CounterState>>();

        // Act - Update with action name
        await store.UpdateAsync(async state =>
        {
            await Task.Delay(50);
            return state with { Count = state.Count + 1, LastAction = "ASYNC" };
        }, action: "ASYNC");

        // Assert
        store.GetState().Count.Should().Be(1);
        asyncMiddleware.Logs.Should().Contain(log => log.Contains("ASYNC"));
    }

    [Fact]
    public void MultipleComponents_SameStore_Synchronized()
    {
        // Arrange
        Services.AddStore(new CounterState(0, "INIT"));
        StoreTestHelpers.RegisterStoreUtilities<CounterState>(Services);

        var component1 = RenderComponent<IntegrationTestComponent>();
        var component2 = RenderComponent<IntegrationTestComponent>();

        // Act - Update from component1
        component1.Find("button").Click();

        // Assert - Both components synchronized
        component1.Find("p").TextContent.Should().Contain("Count: 1");
        component2.Find("p").TextContent.Should().Contain("Count: 1");
    }

    [Fact]
    public async Task StoreWithCustomComparer_SkipsUnnecessaryUpdates()
    {
        // Arrange
        Services.AddStore(
            new CounterState(0, "INIT"),
            (builder, sp) => builder.WithComparer(new CountComparer()));

        var store = Services.GetRequiredService<IStore<CounterState>>();
        var callCount = 0;
        store.Subscribe(_ => callCount++);

        // Act - Change only LastAction (Count unchanged)
        await store.UpdateAsync(state => state with { LastAction = "CHANGED" });

        // Assert - No notification due to custom comparer
        callCount.Should().Be(0);

        // Act - Change Count
        await store.UpdateAsync(state => state with { Count = 1 });

        // Assert - Notification triggered
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ErrorHandling_InMiddleware_DoesNotBreakChain()
    {
        // Arrange
        var successfulMiddleware = new CustomTrackingMiddleware(() => { }, () => { });
        var faultyMiddleware = new FaultyMiddleware();

        Services.AddStore(
            new CounterState(0, "INIT"),
            (builder, sp) => builder
                .WithMiddleware(successfulMiddleware)
                .WithMiddleware(faultyMiddleware));

        var store = Services.GetRequiredService<IStore<CounterState>>();

        // Act - Update with faulty middleware
        // The store implementation catches middleware exceptions and continues
        Func<Task> act = async () => await store.UpdateAsync(state => state with { Count = 1 });

        // Assert - Store catches middleware exceptions, state still updates
        await act.Should().NotThrowAsync();
        store.GetState().Count.Should().Be(1);
    }

    #region Test Components

    private class IntegrationTestComponent : StoreComponent<CounterState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Count: {State.Count}, Last Action: {State.LastAction}");
            builder.CloseElement();
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, Increment));
            builder.AddContent(5, "Increment");
            builder.CloseElement();
            builder.CloseElement();
        }

        private async Task Increment()
        {
            await Update(
                state => state with
                {
                    Count = state.Count + 1,
                    LastAction = "INCREMENT"
                },
                action: "INCREMENT");
        }
    }

    #endregion

    #region Test Middleware

    private class CustomTrackingMiddleware : IMiddleware<CounterState>
    {
        private readonly Action _onBefore;
        private readonly Action _onAfter;

        public CustomTrackingMiddleware(Action onBefore, Action onAfter)
        {
            _onBefore = onBefore;
            _onAfter = onAfter;
        }

        public Task OnBeforeUpdateAsync(CounterState currentState, string? action)
        {
            _onBefore();
            return Task.CompletedTask;
        }

        public Task OnAfterUpdateAsync(CounterState previousState, CounterState newState, string? action)
        {
            _onAfter();
            return Task.CompletedTask;
        }
    }

    private class AsyncLoggingMiddleware : IMiddleware<CounterState>
    {
        public List<string> Logs { get; } = new();

        public async Task OnBeforeUpdateAsync(CounterState currentState, string? action)
        {
            await Task.Delay(10);
            Logs.Add($"BEFORE: {action}");
        }

        public async Task OnAfterUpdateAsync(CounterState previousState, CounterState newState, string? action)
        {
            await Task.Delay(10);
            Logs.Add($"AFTER: {action}");
        }
    }

    private class FaultyMiddleware : IMiddleware<CounterState>
    {
        public Task OnBeforeUpdateAsync(CounterState currentState, string? action)
        {
            throw new InvalidOperationException("Middleware error");
        }

        public Task OnAfterUpdateAsync(CounterState previousState, CounterState newState, string? action)
        {
            return Task.CompletedTask;
        }
    }

    private class CountComparer : IEqualityComparer<CounterState>
    {
        public bool Equals(CounterState? x, CounterState? y) => x?.Count == y?.Count;
        public int GetHashCode(CounterState obj) => obj.Count.GetHashCode();
    }

    #endregion
}
