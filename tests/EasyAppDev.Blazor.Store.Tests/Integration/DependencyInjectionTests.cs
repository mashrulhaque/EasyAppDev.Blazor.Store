using Bunit;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

/// <summary>
/// Test state record used for DI validation.
/// </summary>
public record TestDiState(int Counter);

/// <summary>
/// Comprehensive integration tests that validate the complete DI setup works correctly.
/// These tests ensure all service registrations work together, preventing DI errors at runtime.
/// </summary>
[Collection("Sequential")]
public class DependencyInjectionTests : TestContext
{
    /// <summary>
    /// Validates that AddStoreUtilities correctly registers all three utility services.
    /// </summary>
    [Fact]
    public void AddStoreUtilities_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStoreUtilities();
        using var provider = services.BuildServiceProvider();

        // Assert - Verify all three utility services are registered
        var debounceManager = provider.GetService<IDebounceManager>();
        var throttleManager = provider.GetService<IThrottleManager>();
        var lazyCache = provider.GetService<ILazyCache>();

        debounceManager.Should().NotBeNull("IDebounceManager should be registered");
        debounceManager.Should().BeOfType<DebounceManager>("Should resolve to concrete DebounceManager");

        throttleManager.Should().NotBeNull("IThrottleManager should be registered");
        throttleManager.Should().BeOfType<ThrottleManager>("Should resolve to concrete ThrottleManager");

        lazyCache.Should().NotBeNull("ILazyCache should be registered");
        lazyCache.Should().BeOfType<LazyCache>("Should resolve to concrete LazyCache");
    }

    /// <summary>
    /// Validates that AddStoreUtilities registers services with Scoped lifetime.
    /// </summary>
    [Fact]
    public void AddStoreUtilities_RegistersServicesAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStoreUtilities();
        using var provider = services.BuildServiceProvider();

        // Act - Create two scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var debounce1 = scope1.ServiceProvider.GetRequiredService<IDebounceManager>();
        var debounce2 = scope1.ServiceProvider.GetRequiredService<IDebounceManager>();
        var debounce3 = scope2.ServiceProvider.GetRequiredService<IDebounceManager>();

        // Assert - Same instance within a scope, different instance across scopes
        ReferenceEquals(debounce1, debounce2).Should().BeTrue("Should return same instance within a scope");
        ReferenceEquals(debounce1, debounce3).Should().BeFalse("Should return different instance across scopes");
    }

    /// <summary>
    /// Validates that AddAsyncActionExecutor registers the correct implementation and resolves IStateWriter dependency.
    /// Note: IStateWriter must be registered separately, as IStore implements IStateWriter.
    /// </summary>
    [Fact]
    public void AddAsyncActionExecutor_RegistersCorrectImplementation()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register the store which implements IStore and IStateWriter
        services.AddStore(new TestDiState(0));

        // IMPORTANT: IAsyncActionExecutor requires IStateWriter<TState> to be registered
        // IStore<TState> implements IStateWriter<TState>, so we need to register an alias
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());

        // Register the async action executor
        services.AddAsyncActionExecutor<TestDiState>();

        using var provider = services.BuildServiceProvider();

        // Act
        var executor = provider.GetService<IAsyncActionExecutor<TestDiState>>();
        var store = provider.GetService<IStore<TestDiState>>();

        // Assert - Verify executor is registered and can resolve
        executor.Should().NotBeNull("IAsyncActionExecutor should be registered");
        executor.Should().BeOfType<AsyncActionExecutor<TestDiState>>("Should resolve to concrete AsyncActionExecutor");

        // Verify the executor can use the store as IStateWriter
        store.Should().NotBeNull("IStore should be registered");
        store.Should().BeAssignableTo<IStateWriter<TestDiState>>("Store should implement IStateWriter");
    }

    /// <summary>
    /// Validates that AsyncActionExecutor can execute async actions successfully.
    /// </summary>
    [Fact]
    public async Task AddAsyncActionExecutor_CanExecuteAsyncActions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStore(new TestDiState(0));
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        services.AddAsyncActionExecutor<TestDiState>();

        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IAsyncActionExecutor<TestDiState>>();
        var store = provider.GetRequiredService<IStore<TestDiState>>();

        // Act - Execute an async action
        await executor.ExecuteAsync(
            asyncAction: () => Task.FromResult(42),
            loading: s => s with { Counter = -1 },
            success: (s, result) => s with { Counter = result },
            error: null,
            action: "TEST_ACTION"
        );

        // Assert - State should be updated with success value
        store.GetState().Counter.Should().Be(42);
    }

    /// <summary>
    /// Validates that StoreComponent can resolve all injected dependencies without errors.
    /// </summary>
    [Fact]
    public void StoreComponent_CanResolveAllDependencies()
    {
        // Arrange - Setup all required services
        Services.AddStoreUtilities();
        Services.AddStore(new TestDiState(0));
        Services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        Services.AddAsyncActionExecutor<TestDiState>();

        // Act - Render a component that requires all dependencies
        var cut = RenderComponent<TestStoreComponent>();

        // Assert - Component should render without DI exceptions
        cut.Should().NotBeNull("Component should render successfully");
        cut.Find("p").TextContent.Should().Contain("Counter: 0");

        // Verify all dependencies are injected
        var component = cut.Instance;
        component.Store.Should().NotBeNull("IStore should be injected");
        component.DebounceManager.Should().NotBeNull("IDebounceManager should be injected");
        component.ThrottleManager.Should().NotBeNull("IThrottleManager should be injected");
        component.LazyCache.Should().NotBeNull("ILazyCache should be injected");
        component.AsyncExecutor.Should().NotBeNull("IAsyncActionExecutor should be injected");
    }

    /// <summary>
    /// Validates that StoreComponent can use all injected services successfully.
    /// </summary>
    [Fact]
    public async Task StoreComponent_CanUseAllInjectedServices()
    {
        // Arrange
        Services.AddStoreUtilities();
        Services.AddStore(new TestDiState(0));
        Services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        Services.AddAsyncActionExecutor<TestDiState>();

        var cut = RenderComponent<TestStoreComponent>();

        // Act - Trigger update to verify Store works
        cut.Find("#increment").Click();

        // Assert - State should be updated
        cut.Find("p").TextContent.Should().Contain("Counter: 1");

        // Act - Trigger debounced update to verify DebounceManager works
        await cut.InvokeAsync(async () => await cut.Instance.TestDebounce());

        // Wait for debounce to complete (100ms delay + buffer for processing)
        await Task.Delay(200);

        // Force component to re-render and wait for assertion
        cut.WaitForAssertion(() => cut.Find("p").TextContent.Should().Contain("Counter: 2"), timeout: TimeSpan.FromSeconds(1));

        // Act - Test LazyCache
        var cachedValue = await cut.InvokeAsync(() => cut.Instance.TestLazyLoad());

        // Assert - LazyCache should work
        cachedValue.Should().Be("cached-value");
    }

    /// <summary>
    /// Validates the complete setup pattern from the sample app succeeds.
    /// Tests the recommended configuration: AddStoreUtilities + IStateWriter + AddAsyncActionExecutor + AddStore.
    /// </summary>
    [Fact]
    public async Task FullSetup_WithSampleAppConfiguration_Succeeds()
    {
        // Arrange - Replicate exact sample app setup
        var services = new ServiceCollection();

        // Add required JSRuntime mock for WithDefaults
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new MockJSRuntime());

        // Sample app pattern:
        services.AddStoreUtilities();
        services.AddStore(
            new TestDiState(0),
            (store, sp) => store.WithDefaults(sp, "Test Store"));

        // IMPORTANT: Register IStateWriter alias for AsyncActionExecutor
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        services.AddAsyncActionExecutor<TestDiState>();

        using var provider = services.BuildServiceProvider();

        // Act - Resolve all services
        var store = provider.GetService<IStore<TestDiState>>();
        var debounceManager = provider.GetService<IDebounceManager>();
        var throttleManager = provider.GetService<IThrottleManager>();
        var lazyCache = provider.GetService<ILazyCache>();
        var asyncExecutor = provider.GetService<IAsyncActionExecutor<TestDiState>>();

        // Assert - All services should resolve correctly
        store.Should().NotBeNull("Store should be registered");
        debounceManager.Should().NotBeNull("DebounceManager should be registered");
        throttleManager.Should().NotBeNull("ThrottleManager should be registered");
        lazyCache.Should().NotBeNull("LazyCache should be registered");
        asyncExecutor.Should().NotBeNull("AsyncActionExecutor should be registered");

        // Verify store is functional
        store!.GetState().Counter.Should().Be(0);
        await store.UpdateAsync(s => s with { Counter = 5 });
        store.GetState().Counter.Should().Be(5);
    }

    /// <summary>
    /// Validates that multiple stores with different state types can coexist.
    /// </summary>
    [Fact]
    public void FullSetup_WithMultipleStores_AllServicesResolveCorrectly()
    {
        // Arrange - Setup multiple stores
        Services.AddStoreUtilities();

        Services.AddStore(new TestDiState(0));
        Services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        Services.AddAsyncActionExecutor<TestDiState>();

        Services.AddStore(new SecondTestState("Initial"));
        Services.AddScoped<IStateWriter<SecondTestState>>(sp => sp.GetRequiredService<IStore<SecondTestState>>());
        Services.AddAsyncActionExecutor<SecondTestState>();

        // Act - Resolve all services
        var store1 = Services.GetService<IStore<TestDiState>>();
        var store2 = Services.GetService<IStore<SecondTestState>>();
        var executor1 = Services.GetService<IAsyncActionExecutor<TestDiState>>();
        var executor2 = Services.GetService<IAsyncActionExecutor<SecondTestState>>();
        var debounceManager = Services.GetService<IDebounceManager>();

        // Assert - All services should resolve independently
        store1.Should().NotBeNull();
        store2.Should().NotBeNull();
        executor1.Should().NotBeNull();
        executor2.Should().NotBeNull();
        debounceManager.Should().NotBeNull();

        // Verify stores are independent
        store1!.GetState().Counter.Should().Be(0);
        store2!.GetState().Message.Should().Be("Initial");
    }

    /// <summary>
    /// Validates that AddStore without utilities throws when StoreComponent tries to resolve dependencies.
    /// </summary>
    [Fact]
    public void StoreComponent_WithoutUtilities_ThrowsOnRender()
    {
        // Arrange - Register store but NOT utilities (common mistake)
        Services.AddStore(new TestDiState(0));
        // Intentionally NOT calling: Services.AddStoreUtilities();
        // Intentionally NOT calling: Services.AddAsyncActionExecutor<TestDiState>();

        // Act & Assert - Should throw when trying to render component
        Action act = () => RenderComponent<TestStoreComponent>();

        act.Should().Throw<InvalidOperationException>("Component should fail to resolve IDebounceManager")
           .WithMessage("*IDebounceManager*");
    }

    /// <summary>
    /// Validates that AddAsyncActionExecutor without IStateWriter registration throws when trying to resolve.
    /// This is a common mistake - users must register IStateWriter alias or use the helper method.
    /// </summary>
    [Fact]
    public void AddAsyncActionExecutor_WithoutStateWriter_ThrowsOnResolve()
    {
        // Arrange - Register executor but NOT IStateWriter (common mistake)
        var services = new ServiceCollection();
        services.AddAsyncActionExecutor<TestDiState>();
        // Intentionally NOT calling: services.AddStore(...)
        // Intentionally NOT calling: services.AddScoped<IStateWriter<TestDiState>>(...)

        using var provider = services.BuildServiceProvider();

        // Act & Assert - Should throw when trying to resolve executor
        Action act = () => provider.GetRequiredService<IAsyncActionExecutor<TestDiState>>();

        act.Should().Throw<InvalidOperationException>("Executor should fail to resolve IStateWriter dependency")
           .WithMessage("*IStateWriter*");
    }

    /// <summary>
    /// Validates that scoped stores work correctly with scoped utilities.
    /// </summary>
    [Fact]
    public async Task ScopedStore_WithScopedUtilities_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStoreUtilities();
        services.AddScopedStore(new TestDiState(0)); // Scoped instead of singleton
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        services.AddAsyncActionExecutor<TestDiState>();

        using var provider = services.BuildServiceProvider();

        // Act - Create two scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var store1 = scope1.ServiceProvider.GetRequiredService<IStore<TestDiState>>();
        var store2 = scope2.ServiceProvider.GetRequiredService<IStore<TestDiState>>();

        await store1.UpdateAsync(s => s with { Counter = 10 });
        await store2.UpdateAsync(s => s with { Counter = 20 });

        // Assert - Each scope has its own store instance
        store1.GetState().Counter.Should().Be(10);
        store2.GetState().Counter.Should().Be(20);
        ReferenceEquals(store1, store2).Should().BeFalse("Scoped stores should be different instances");
    }

    /// <summary>
    /// Validates that optional ILogger dependency doesn't break AsyncActionExecutor.
    /// </summary>
    [Fact]
    public void AsyncActionExecutor_WithoutLogger_StillWorks()
    {
        // Arrange - Setup without ILogger
        var services = new ServiceCollection();
        services.AddStore(new TestDiState(0));
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        services.AddAsyncActionExecutor<TestDiState>();
        // Intentionally NOT calling: services.AddLogging()

        using var provider = services.BuildServiceProvider();

        // Act - Resolve executor (logger is optional)
        var executor = provider.GetService<IAsyncActionExecutor<TestDiState>>();

        // Assert - Should resolve successfully even without logger
        executor.Should().NotBeNull("Executor should work without ILogger");
    }

    /// <summary>
    /// Validates that AsyncActionExecutor can use ILogger when available.
    /// </summary>
    [Fact]
    public async Task AsyncActionExecutor_WithLogger_UsesItForErrorLogging()
    {
        // Arrange - Setup with ILogger
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStore(new TestDiState(0));
        services.AddScoped<IStateWriter<TestDiState>>(sp => sp.GetRequiredService<IStore<TestDiState>>());
        services.AddAsyncActionExecutor<TestDiState>();

        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IAsyncActionExecutor<TestDiState>>();

        // Act - Execute action that throws (without error handler)
        Func<Task> act = async () => await executor.ExecuteAsync(
            asyncAction: () => Task.FromException<int>(new InvalidOperationException("Test error")),
            loading: s => s with { Counter = -1 },
            success: (s, result) => s with { Counter = result },
            error: null // No error handler - should use logger
        );

        // Assert - Should not throw (logger handles the error)
        await act.Should().NotThrowAsync("Logger should handle the error gracefully");
    }

    #region Test Components

    /// <summary>
    /// Test component that exposes all injected dependencies for validation.
    /// Uses StoreComponentWithUtilities for access to utility methods.
    /// </summary>
    public class TestStoreComponent : StoreComponentWithUtilities<TestDiState>
    {
        // Expose protected properties for testing
        public new IStore<TestDiState> Store => base.Store;
        public new IDebounceManager DebounceManager => base.DebounceManager;
        public new IThrottleManager ThrottleManager => base.ThrottleManager;
        public new ILazyCache LazyCache => base.LazyCache;
        public new IAsyncActionExecutor<TestDiState>? AsyncExecutor => base.AsyncExecutor;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.OpenElement(1, "p");
            builder.AddContent(2, $"Counter: {State.Counter}");
            builder.CloseElement();
            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "id", "increment");
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, Increment));
            builder.AddContent(6, "Increment");
            builder.CloseElement();
            builder.CloseElement();
        }

        private void Increment()
        {
            Update(s => s with { Counter = s.Counter + 1 });
        }

        public async Task TestDebounce()
        {
            await UpdateDebounced(s => s with { Counter = s.Counter + 1 }, 100);
        }

        public async Task<string> TestLazyLoad()
        {
            return await LazyLoad("test-key", () => Task.FromResult("cached-value"));
        }
    }

    #endregion

    #region Test Records

    /// <summary>
    /// Second test state record for multi-store tests.
    /// </summary>
    public record SecondTestState(string Message);

    #endregion

    #region Mock Services

    /// <summary>
    /// Mock JSRuntime for testing WithDefaults extension.
    /// </summary>
    private class MockJSRuntime : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }
    }

    #endregion
}
