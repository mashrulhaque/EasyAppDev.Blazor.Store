using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

/// <summary>
/// Tests for scoped store registration with IServiceProvider access.
/// Validates the fix for AddScopedStore configure signature.
/// </summary>
public class ScopedStoreTests
{
    private record TestState(int Value, string? ServiceValue = null);

    [Fact]
    public void AddScopedStore_WithServiceProviderConfigure_CanAccessScopedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<string>(_ => "ScopedServiceValue");

        // Act - Using new signature with IServiceProvider
        services.AddScopedStore(
            new TestState(0),
            (builder, sp) =>
            {
                // Can access scoped services during configuration
                var scopedValue = sp.GetService<string>();
                scopedValue.Should().NotBeNull("because scoped services should be available");
                return builder;
            });

        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        store.Should().NotBeNull();
        store.GetState().Value.Should().Be(0);
    }

    [Fact]
    public async Task AddScopedStore_DifferentScopes_GetDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScopedStore(new TestState(0));
        var provider = services.BuildServiceProvider();

        // Act & Assert
        IStore<TestState> store1, store2;
        int store1Value;
        int store2Value;

        using (var scope1 = provider.CreateScope())
        {
            store1 = scope1.ServiceProvider.GetRequiredService<IStore<TestState>>();
            await store1.UpdateAsync(s => s with { Value = 10 });
            store1Value = store1.GetState().Value; // Read before disposal
        }

        using (var scope2 = provider.CreateScope())
        {
            store2 = scope2.ServiceProvider.GetRequiredService<IStore<TestState>>();
            store2Value = store2.GetState().Value; // Read before disposal
        }

        // Assert
        store1.Should().NotBeSameAs(store2, "because different scopes should have different instances");
        store1Value.Should().Be(10, "because store1 was updated");
        store2Value.Should().Be(0, "because scope2 has a fresh instance");
    }

    [Fact]
    public void AddScopedStore_SameScope_GetsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScopedStore(new TestState(0));
        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var store1 = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        var store2 = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();

        // Assert
        store1.Should().BeSameAs(store2, "because same scope should return same instance");
    }

    [Fact]
    public void AddScopedStore_WithFactoryAndServiceProvider_CanResolveDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestDependency>(_ => new TestDependency { Value = 42 });

        // Act - Factory with IServiceProvider access
        services.AddScopedStore(
            sp =>
            {
                var dependency = sp.GetRequiredService<TestDependency>();
                return new TestState(dependency.Value);
            },
            (builder, sp) =>
            {
                var stringValue = sp.GetService<string>();
                return builder;
            });

        services.AddScoped<string>(_ => "ConfigValue");
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        store.GetState().Value.Should().Be(42, "because factory used scoped service");
    }

    private class TestDependency
    {
        public int Value { get; set; }
    }

    [Fact]
    public void AddScopedStoreWithUtilities_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedStoreWithUtilities(
            new TestState(0),
            (builder, sp) => builder);

        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetService<IStore<TestState>>();
        var debounce = scope.ServiceProvider.GetService<EasyAppDev.Blazor.Store.Utilities.IDebounceManager>();
        var throttle = scope.ServiceProvider.GetService<EasyAppDev.Blazor.Store.Utilities.IThrottleManager>();
        var cache = scope.ServiceProvider.GetService<EasyAppDev.Blazor.Store.Utilities.ILazyCache>();
        var executor = scope.ServiceProvider.GetService<EasyAppDev.Blazor.Store.AsyncActions.IAsyncActionExecutor<TestState>>();

        store.Should().NotBeNull();
        debounce.Should().NotBeNull();
        throttle.Should().NotBeNull();
        cache.Should().NotBeNull();
        executor.Should().NotBeNull();
    }

    [Fact]
    [Obsolete("Testing obsolete overload for backward compatibility")]
    public void AddScopedStore_LegacySignature_StillWorks()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Using old signature (obsolete but should still compile)
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddScopedStore(
            new TestState(0),
            (Action<StoreBuilder<TestState>>)(builder => builder.WithComparer(EqualityComparer<TestState>.Default)));
#pragma warning restore CS0618

        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore<TestState>>();
        store.Should().NotBeNull();
    }
}
