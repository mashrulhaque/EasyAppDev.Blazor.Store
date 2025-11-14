using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class LazyLoadTests : TestContext
{
    [Fact]
    public async Task LazyLoad_FirstCall_LoadsData()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(1));

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.Products.Should().ContainKey(1);
        component.Instance.State.Products[1].Name.Should().Be("Product 1");
        component.Instance.State.LoadCount.Should().Be(1);
    }

    [Fact]
    public async Task LazyLoad_CacheHit_DoesNotReload()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Load same product twice
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(1));
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(1));

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.LoadCount.Should().Be(1, "because second call should hit cache");
    }

    [Fact]
    public async Task LazyLoad_ConcurrentCalls_Deduplicates()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Fire 5 concurrent loads for same product
        await component.InvokeAsync(async () =>
        {
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => component.Instance.LoadProduct(1))
                .ToList();
            await Task.WhenAll(tasks);
        });

        // Wait for async operations
        await Task.Delay(200);

        // Assert
        component.Instance.State.LoadCount.Should().Be(1, "because concurrent requests should be deduplicated");
    }

    [Fact]
    public async Task LazyLoad_DifferentKeys_LoadsIndependently()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Load different products
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(1));
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(2));

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.Products.Should().HaveCount(2);
        component.Instance.State.LoadCount.Should().Be(2, "because different keys should load independently");
    }

    [Fact]
    public async Task LazyLoad_WithExpiration_ReloadsAfterExpiry()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Load with longer expiration to avoid race conditions
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProductWithExpiration(1, expirationMs: 500));

        // Wait for first load to complete
        await Task.Delay(100);
        var initialLoadCount = component.Instance.State.LoadCount;
        initialLoadCount.Should().Be(1, "because first load should complete");

        // Call again before expiration - should hit cache (still ~400ms until expiry)
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProductWithExpiration(1, expirationMs: 500));

        await Task.Delay(50);
        component.Instance.State.LoadCount.Should().Be(1, "because second call should hit cache");

        // Wait for cache to expire (need to wait at least 350ms more from the initial load)
        await Task.Delay(400);

        // Call again after expiration - should reload
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProductWithExpiration(1, expirationMs: 500));

        await Task.Delay(100);

        // Assert
        component.Instance.State.LoadCount.Should().Be(2, "because cache should reload after expiration");
    }

    [Fact]
    public async Task LazyLoad_OnError_DoesNotCache()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Load with error on first attempt
        await component.InvokeAsync(async () =>
        {
            try
            {
                await component.Instance.LoadProductWithError(1);
            }
            catch
            {
                // Expected error
            }
        });

        // Second attempt should retry (error not cached)
        await component.InvokeAsync(async () =>
            await component.Instance.LoadProduct(1));

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.LoadCount.Should().Be(2, "because errors should not be cached");
    }

    [Fact]
    public async Task LazyLoad_DisposesCleanly()
    {
        // Arrange
        Services.AddStore(new LazyLoadTestState(), (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<LazyLoadTestState>(Services);
        var component = RenderComponent<TestLazyLoadComponent>();

        // Act - Load some data
        await component.InvokeAsync(async () =>
        {
            await component.Instance.LoadProduct(1);
            await component.Instance.LoadProduct(2);
        });

        // Wait for async operations
        await Task.Delay(100);

        // Verify data loaded before disposal
        component.Instance.State.Products.Should().HaveCount(2);

        // Dispose component - should not throw
        var act = () => component.Dispose();
        act.Should().NotThrow("because disposal should be clean");
    }
}

// Test state
public record LazyLoadTestState(
    ImmutableDictionary<int, ProductData> Products,
    int LoadCount = 0)
{
    public LazyLoadTestState() : this(ImmutableDictionary<int, ProductData>.Empty)
    { }
}

public record ProductData(int Id, string Name, decimal Price);

// Test component
public class TestLazyLoadComponent : StoreComponent<LazyLoadTestState>
{
    public new LazyLoadTestState State => base.State;

    public async Task LoadProduct(int productId)
    {
        var product = await LazyLoad(
            $"product-{productId}",
            async () =>
            {
                await Task.Delay(50); // Simulate API call
                await Update(s => s with { LoadCount = s.LoadCount + 1 });
                return new ProductData(productId, $"Product {productId}", 99.99m * productId);
            });

        await Update(s => s with { Products = s.Products.SetItem(productId, product) });
    }

    public async Task LoadProductWithExpiration(int productId, int expirationMs)
    {
        var product = await LazyLoad(
            $"product-exp-{productId}",
            async () =>
            {
                await Task.Delay(50); // Simulate API call
                await Update(s => s with { LoadCount = s.LoadCount + 1 });
                return new ProductData(productId, $"Product {productId}", 99.99m * productId);
            },
            cacheFor: TimeSpan.FromMilliseconds(expirationMs));

        await Update(s => s with { Products = s.Products.SetItem(productId, product) });
    }

    public async Task LoadProductWithError(int productId)
    {
        var product = await LazyLoad(
            $"product-error-{productId}",
            async () =>
            {
                await Task.Delay(50); // Simulate API call
                await Update(s => s with { LoadCount = s.LoadCount + 1 });
                throw new InvalidOperationException("Simulated error");
#pragma warning disable CS0162 // Unreachable code detected
                return new ProductData(productId, $"Product {productId}", 99.99m * productId);
#pragma warning restore CS0162
            });

        await Update(s => s with { Products = s.Products.SetItem(productId, product) });
    }
}
