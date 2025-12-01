using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Middleware;

public record FunctionalMiddlewareTestState(int Count, string Action = "");

public class FunctionalMiddlewareTests
{
    [Fact]
    public async Task Use_WithMiddleware_ExecutesForBothPhases()
    {
        // Arrange
        var phases = new List<MiddlewarePhase>();

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(async (ctx, next) =>
            {
                phases.Add(ctx.Phase);
                await next();
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        phases.Should().Contain(MiddlewarePhase.Before);
        phases.Should().Contain(MiddlewarePhase.After);
        phases.Should().HaveCount(2);

        store.Dispose();
    }

    [Fact]
    public async Task Use_WithMiddleware_ReceivesCorrectContext()
    {
        // Arrange
        MiddlewareContext<FunctionalMiddlewareTestState>? capturedBeforeContext = null;
        MiddlewareContext<FunctionalMiddlewareTestState>? capturedAfterContext = null;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(async (ctx, next) =>
            {
                if (ctx.IsBefore) capturedBeforeContext = ctx;
                if (ctx.IsAfter) capturedAfterContext = ctx;
                await next();
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 5 }, "INCREMENT");

        // Assert
        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext!.CurrentState.Count.Should().Be(0);
        capturedBeforeContext.Action.Should().Be("INCREMENT");
        capturedBeforeContext.NewState.Should().BeNull();
        capturedBeforeContext.IsBefore.Should().BeTrue();
        capturedBeforeContext.IsAfter.Should().BeFalse();

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext!.CurrentState.Count.Should().Be(0); // previous state
        capturedAfterContext.NewState!.Count.Should().Be(5);
        capturedAfterContext.Action.Should().Be("INCREMENT");
        capturedAfterContext.IsAfter.Should().BeTrue();

        store.Dispose();
    }

    [Fact]
    public async Task UseWhen_WithMatchingPredicate_ExecutesMiddleware()
    {
        // Arrange
        var executed = false;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .UseWhen(
                ctx => ctx.Action?.StartsWith("INCREMENT") == true,
                async (ctx, next) =>
                {
                    executed = true;
                    await next();
                })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "INCREMENT");

        // Assert
        executed.Should().BeTrue();

        store.Dispose();
    }

    [Fact]
    public async Task UseWhen_WithNonMatchingPredicate_DoesNotExecuteMiddleware()
    {
        // Arrange
        var executed = false;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .UseWhen(
                ctx => ctx.Action?.StartsWith("DECREMENT") == true,
                async (ctx, next) =>
                {
                    executed = true;
                    await next();
                })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "INCREMENT");

        // Assert
        executed.Should().BeFalse();

        store.Dispose();
    }

    [Fact]
    public async Task UseForAction_MatchesExactActionName()
    {
        // Arrange
        var matched = false;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .UseForAction("SAVE_USER", async (ctx, next) =>
            {
                matched = true;
                await next();
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "SAVE_USER");
        var matchedFirst = matched;

        matched = false;
        await store.UpdateAsync(s => s with { Count = 2 }, "DELETE_USER");
        var matchedSecond = matched;

        // Assert
        matchedFirst.Should().BeTrue();
        matchedSecond.Should().BeFalse();

        store.Dispose();
    }

    [Fact]
    public async Task UseForActionPrefix_MatchesActionsWithPrefix()
    {
        // Arrange
        var matchedActions = new List<string?>();

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .UseForActionPrefix("FETCH_", async (ctx, next) =>
            {
                matchedActions.Add(ctx.Action);
                await next();
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "FETCH_USERS");
        await store.UpdateAsync(s => s with { Count = 2 }, "FETCH_PRODUCTS");
        await store.UpdateAsync(s => s with { Count = 3 }, "SAVE_USER"); // No match

        // Assert
        matchedActions.Should().HaveCount(4); // 2 actions * 2 phases (before + after)
        matchedActions.Should().Contain("FETCH_USERS");
        matchedActions.Should().Contain("FETCH_PRODUCTS");
        matchedActions.Should().NotContain("SAVE_USER");

        store.Dispose();
    }

    [Fact]
    public async Task Use_WithSeparateHandlers_CallsCorrectHandler()
    {
        // Arrange
        var beforeCalled = false;
        var afterCalled = false;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(
                beforeHandler: async (ctx, next) =>
                {
                    beforeCalled = true;
                    await next();
                },
                afterHandler: async (ctx, next) =>
                {
                    afterCalled = true;
                    await next();
                })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        beforeCalled.Should().BeTrue();
        afterCalled.Should().BeTrue();

        store.Dispose();
    }

    [Fact]
    public async Task Use_WithOnlyBeforeHandler_OnlyCallsBeforeHandler()
    {
        // Arrange
        var beforeCalled = false;
        var afterCalled = false;

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(
                beforeHandler: async (ctx, next) =>
                {
                    beforeCalled = true;
                    await next();
                },
                afterHandler: null)
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        beforeCalled.Should().BeTrue();
        afterCalled.Should().BeFalse();

        store.Dispose();
    }

    [Fact]
    public void Use_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Use_WithBothHandlersNull_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(beforeHandler: null, afterHandler: null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Use_WithServiceProvider_MakesServicesAvailable()
    {
        // Arrange
        IServiceProvider? capturedServices = null;
        var serviceProvider = new TestServiceProvider();

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(serviceProvider, async (ctx, next) =>
            {
                capturedServices = ctx.Services;
                await next();
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        capturedServices.Should().BeSameAs(serviceProvider);

        store.Dispose();
    }

    [Fact]
    public async Task MultipleMiddlewares_ExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<int>();

        var store = StoreBuilder<FunctionalMiddlewareTestState>
            .Create(new FunctionalMiddlewareTestState(0))
            .Use(async (ctx, next) =>
            {
                if (ctx.IsBefore) executionOrder.Add(1);
                await next();
                if (ctx.IsAfter) executionOrder.Add(1);
            })
            .Use(async (ctx, next) =>
            {
                if (ctx.IsBefore) executionOrder.Add(2);
                await next();
                if (ctx.IsAfter) executionOrder.Add(2);
            })
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        executionOrder.Should().BeEquivalentTo([1, 2, 1, 2]);

        store.Dispose();
    }

    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
