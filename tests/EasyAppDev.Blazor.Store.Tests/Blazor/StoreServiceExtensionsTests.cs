using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Utilities;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class StoreServiceExtensionsTests
{
    [Fact]
    public void AddStore_RegistersSingletonStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialState = new TestState(0, "Initial");

        // Act
        services.AddStore(initialState);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
        store!.GetState().Should().Be(initialState);
    }

    [Fact]
    public void AddStore_RegistersSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialState = new TestState(0, "Initial");

        // Act
        services.AddStore(initialState);
        var provider = services.BuildServiceProvider();

        // Assert
        var store1 = provider.GetRequiredService<IStore<TestState>>();
        var store2 = provider.GetRequiredService<IStore<TestState>>();
        store1.Should().BeSameAs(store2); // Singleton
    }

    [Fact]
    public void AddStore_WithFactory_UsesFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton("TestValue");

        // Act
        services.AddStore<TestState>(sp =>
        {
            var value = sp.GetRequiredService<string>();
            return new TestState(0, value);
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetRequiredService<IStore<TestState>>();
        store.GetState().Message.Should().Be("TestValue");
    }

    [Fact]
    public void AddStore_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var comparer = new TestStateComparer();
        var configureInvoked = false;

        // Act
        services.AddStore(
            new TestState(0, "Test"),
            (builder, sp) =>
            {
                configureInvoked = true;
                return builder.WithComparer(comparer);
            });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetRequiredService<IStore<TestState>>();
        store.Should().NotBeNull();
        configureInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddScopedStore_RegistersScopedStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialState = new TestState(0, "Scoped");

        // Act
        services.AddScopedStore(initialState);
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var store1 = scope1.ServiceProvider.GetRequiredService<IStore<TestState>>();
        var store2 = scope2.ServiceProvider.GetRequiredService<IStore<TestState>>();

        store1.Should().NotBeSameAs(store2); // Different instances per scope
    }

    [Fact]
    public void AddScopedStore_WithFactory_UsesFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<string>(_ => "ScopedValue");

        // Act
        services.AddScopedStore<TestState>(sp =>
        {
            var value = sp.GetRequiredService<string>();
            return new TestState(0, value);
        });
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        store.GetState().Message.Should().Be("ScopedValue");
    }

    [Fact]
    public void AddScopedStore_SameInstanceWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScopedStore(new TestState(0, "Scoped"));
        var provider = services.BuildServiceProvider();

        // Act & Assert
        using var scope = provider.CreateScope();
        var store1 = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        var store2 = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();

        store1.Should().BeSameAs(store2); // Same instance within scope
    }

    [Fact]
    public void AddTransientStore_RegistersTransientStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTransientStore<TestState>(
            sp => new TestState(0, "Transient"));
        var provider = services.BuildServiceProvider();

        // Assert
        var store1 = provider.GetRequiredService<IStore<TestState>>();
        var store2 = provider.GetRequiredService<IStore<TestState>>();

        store1.Should().NotBeSameAs(store2); // Different instances each time
    }

    [Fact]
    public void AddTransientStore_WithFactory_UsesFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var counter = 0;

        // Act
        services.AddTransientStore<TestState>(sp =>
        {
            counter++;
            return new TestState(counter, "Transient");
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store1 = provider.GetRequiredService<IStore<TestState>>();
        var store2 = provider.GetRequiredService<IStore<TestState>>();

        store1.GetState().Counter.Should().Be(1);
        store2.GetState().Counter.Should().Be(2);
    }

    [Fact]
    public void AddTransientStore_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var comparer = new TestStateComparer();
        var configureInvoked = false;

        // Act
        services.AddTransientStore<TestState>(
            sp => new TestState(0, "Test"),
            (builder) =>
            {
                configureInvoked = true;
                builder.WithComparer(comparer);
            });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetRequiredService<IStore<TestState>>();
        store.Should().NotBeNull();
        configureInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddStore_ReturnsServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddStore(new TestState(0, "Test"));

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void MultipleStores_CanBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStore(new TestState(0, "Store1"));
        services.AddStore(new CounterState(10));
        var provider = services.BuildServiceProvider();

        // Assert
        var testStore = provider.GetRequiredService<IStore<TestState>>();
        var counterStore = provider.GetRequiredService<IStore<CounterState>>();

        testStore.GetState().Counter.Should().Be(0);
        counterStore.GetState().Value.Should().Be(10);
    }

    [Fact]
    public void AddStoreWithUtilities_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStoreWithUtilities(new TestState(0, "Test"));
        var provider = services.BuildServiceProvider();

        // Assert - Store registered
        var store = provider.GetService<IStore<TestState>>();
        store.Should().NotBeNull();
        store!.GetState().Should().Be(new TestState(0, "Test"));

        // Assert - Utility services registered
        provider.GetService<IDebounceManager>().Should().NotBeNull();
        provider.GetService<IThrottleManager>().Should().NotBeNull();
        provider.GetService<ILazyCache>().Should().NotBeNull();
        provider.GetService<IAsyncActionExecutor<TestState>>().Should().NotBeNull();
    }

    [Fact]
    public void AddStoreWithUtilities_WithConfiguration_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var comparer = new TestStateComparer();
        var configureInvoked = false;

        // Act
        services.AddStoreWithUtilities(
            new TestState(0, "Test"),
            (builder, sp) =>
            {
                configureInvoked = true;
                return builder.WithComparer(comparer);
            });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetRequiredService<IStore<TestState>>();
        store.Should().NotBeNull();
        configureInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddStoreWithUtilities_ReturnsServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddStoreWithUtilities(new TestState(0, "Test"));

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddStoreWithUtilities_MultipleStores_EachGetsOwnAsyncExecutor()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddStoreWithUtilities(new TestState(0, "Store1"));
        services.AddStoreWithUtilities(new CounterState(10));
        var provider = services.BuildServiceProvider();

        // Assert - Both executors registered
        var testExecutor = provider.GetService<IAsyncActionExecutor<TestState>>();
        var counterExecutor = provider.GetService<IAsyncActionExecutor<CounterState>>();

        testExecutor.Should().NotBeNull();
        counterExecutor.Should().NotBeNull();
        testExecutor.Should().NotBeSameAs(counterExecutor);
    }

    [Fact]
    public void AddScopedStoreWithUtilities_RegistersScopedStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedStoreWithUtilities(new TestState(0, "Scoped"));
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var store1 = scope1.ServiceProvider.GetRequiredService<IStore<TestState>>();
        var store2 = scope2.ServiceProvider.GetRequiredService<IStore<TestState>>();

        store1.Should().NotBeSameAs(store2); // Different instances per scope
    }

    [Fact]
    public void AddScopedStoreWithUtilities_WithFactory_UsesFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<string>(_ => "ScopedValue");

        // Act
        services.AddScopedStoreWithUtilities<TestState>(
            sp =>
            {
                var value = sp.GetRequiredService<string>();
                return new TestState(0, value);
            });
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        store.GetState().Message.Should().Be("ScopedValue");

        // Assert utilities also registered
        scope.ServiceProvider.GetService<IDebounceManager>().Should().NotBeNull();
    }

    [Fact]
    public void AddScopedStoreWithUtilities_RegistersAllUtilities()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedStoreWithUtilities(new TestState(0, "Test"));
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<IDebounceManager>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IThrottleManager>().Should().NotBeNull();
        scope.ServiceProvider.GetService<ILazyCache>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IAsyncActionExecutor<TestState>>().Should().NotBeNull();
    }

    [Fact]
    public void AddStoreUtilities_CanBeCalledMultipleTimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Call multiple times (e.g., when registering multiple stores)
        services.AddStoreUtilities();
        services.AddStoreUtilities();
        services.AddStoreUtilities();
        var provider = services.BuildServiceProvider();

        // Assert - Should not throw and services should be registered
        provider.GetService<IDebounceManager>().Should().NotBeNull();
        provider.GetService<IThrottleManager>().Should().NotBeNull();
        provider.GetService<ILazyCache>().Should().NotBeNull();
    }

    [Fact]
    public async Task AddScopedStore_WithConfigure_CalledWithDevTools_LoadsJavascriptModule()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialState = new TestState(0, "Scoped");
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        // Register IJSRuntime so configure can pick it up from the service provider
        services.AddScoped(_ => jsRuntimeMock.Object);

        // Act - configure the scoped store to use JS runtime and enable DevTools
        services.AddScopedStore<TestState>(
            initialState,
            (builder, sp) => builder.WithDevTools(sp)
        );

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();

        // Trigger an update to cause DevTools middleware to initialize and import the JS module
        await store.UpdateAsync(state => state with { Counter = 1 }, "INCREMENT");

        // Assert - JS module import should have been invoked
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()),
            Times.Once);
    }

    private class TestStateComparer : IEqualityComparer<TestState>
    {
        public bool Equals(TestState? x, TestState? y) => x?.Counter == y?.Counter;
        public int GetHashCode(TestState obj) => obj.Counter.GetHashCode();
    }

    private record CounterState(int Value);
}
