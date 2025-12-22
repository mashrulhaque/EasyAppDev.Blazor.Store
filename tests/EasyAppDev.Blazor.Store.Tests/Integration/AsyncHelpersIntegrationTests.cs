using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

/// <summary>
/// Integration tests verifying all async helpers work together correctly.
/// Tests the complete workflow of debounce, throttle, ExecuteAsync, and LazyLoad.
/// </summary>
public class AsyncHelpersIntegrationTests : TestContext
{
    [Fact]
    public async Task CompleteWorkflow_DebounceSearchThenExecuteAsync_WorksTogether()
    {
        // Arrange
        Services.AddStore(SearchAndLoadState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<SearchAndLoadState>(Services);
        var cut = RenderComponent<SearchAndLoadComponent>();

        // Act 1: Type rapidly - should debounce to only execute last value
        cut.Instance.HandleSearch("a");
        await Task.Delay(50);
        cut.Instance.HandleSearch("ab");
        await Task.Delay(50);
        cut.Instance.HandleSearch("abc");

        // Wait for debounce to complete (300ms debounce + 100ms buffer)
        await Task.Delay(450);

        // Assert 1: Only final search value should be set
        cut.Instance.State.SearchQuery.Should().Be("abc");

        // Act 2: Load results using ExecuteAsync
        await cut.InvokeAsync(async () => await cut.Instance.LoadResults());
        await Task.Delay(100);

        // Assert 2: Results loaded successfully
        cut.Instance.State.Results.HasData.Should().BeTrue();
        cut.Instance.State.Results.Data.Should().NotBeEmpty();
        cut.Instance.State.Results.Data.Should().Contain(r => r.Contains("Result"));
    }

    [Fact]
    public async Task CompleteWorkflow_ThrottleEventsAndLazyLoad_WorksTogether()
    {
        // Arrange
        Services.AddStore(ScrollAndLoadState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<ScrollAndLoadState>(Services);
        var cut = RenderComponent<ScrollAndLoadComponent>();

        // Act 1: Rapid events (should be throttled)
        for (int i = 0; i < 10; i++)
        {
            cut.Instance.HandleScroll(i * 10);
            await Task.Delay(10); // Fire every 10ms
        }

        await Task.Delay(250); // Wait for throttle window

        // Assert 1: Events were throttled (not all 10 executed)
        cut.Instance.State.ScrollPosition.Should().BeGreaterThan(0);

        // Act 2: LazyLoad items on-demand
        await cut.InvokeAsync(async () => await cut.Instance.LoadItem("item-1"));
        await cut.InvokeAsync(async () => await cut.Instance.LoadItem("item-2"));
        await cut.InvokeAsync(async () => await cut.Instance.LoadItem("item-1")); // Should use cache

        await Task.Delay(100);

        // Assert 2: Items loaded with caching
        cut.Instance.State.LoadedItems.Count.Should().Be(2, "because item-1 was requested twice but should be cached");
    }

    [Fact]
    public async Task AllFeaturesTogether_ComplexScenario_WorksCorrectly()
    {
        // Arrange
        Services.AddStore(ComplexAppState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<ComplexAppState>(Services);
        var cut = RenderComponent<ComplexAppComponent>();

        // Scenario: User performs multiple async operations with all helper types

        // 1. Debounced search input
        cut.Instance.Search("laptop");
        await Task.Delay(50);
        cut.Instance.Search("laptop pro");
        await Task.Delay(400); // Let debounce complete (300ms debounce + 100ms buffer for async operations)
        cut.Instance.State.SearchQuery.Should().Be("laptop pro");

        // 2. Load search results with ExecuteAsync
        await cut.InvokeAsync(async () => await cut.Instance.LoadResults());
        await Task.Delay(100);
        cut.Instance.State.SearchResults.HasData.Should().BeTrue();

        // 3. LazyLoad product details
        await cut.InvokeAsync(async () => await cut.Instance.LoadProductDetails("product-1"));
        await Task.Delay(100);
        cut.Instance.State.ProductCache.Should().ContainKey("product-1");

        // 4. Throttled scroll tracking
        for (int i = 0; i < 5; i++)
        {
            cut.Instance.TrackScroll(i * 100);
            await Task.Delay(20);
        }
        await Task.Delay(200);

        // Final assertion: All features worked without errors
        cut.Instance.State.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task AsyncData_TransitionsCorrectly_ThroughAllStates()
    {
        // Arrange
        Services.AddStore(AsyncTransitionState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<AsyncTransitionState>(Services);
        var cut = RenderComponent<AsyncTransitionComponent>();

        // Assert: Initial state is NotAsked
        cut.Instance.State.Data.IsNotAsked.Should().BeTrue();

        // Act: Start loading
        var loadTask = cut.InvokeAsync(async () => await cut.Instance.LoadData());
        await Task.Delay(50); // Give it time to set loading state

        // Assert: Transitioning to Loading
        cut.Instance.State.Data.IsLoading.Should().BeTrue();

        // Wait for completion
        await loadTask;
        await Task.Delay(100);

        // Assert: Transitioned to Success
        cut.Instance.State.Data.HasData.Should().BeTrue();
        cut.Instance.State.Data.Data.Should().Be("test data");
    }

    [Fact]
    public async Task AsyncData_ErrorHandling_WorksCorrectly()
    {
        // Arrange
        Services.AddStore(AsyncErrorState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<AsyncErrorState>(Services);
        var cut = RenderComponent<AsyncErrorComponent>();

        // Act: Execute operation that will fail
        await cut.InvokeAsync(async () => await cut.Instance.LoadDataWithError());
        await Task.Delay(100);

        // Assert: Error captured correctly
        cut.Instance.State.Data.HasError.Should().BeTrue();
        cut.Instance.State.Data.Error.Should().Contain("Simulated error");
    }

    [Fact]
    public async Task MultipleAsyncOperations_Concurrent_HandleCorrectly()
    {
        // Arrange
        Services.AddStore(ConcurrentState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<ConcurrentState>(Services);
        var cut = RenderComponent<ConcurrentComponent>();

        // Act: Fire multiple concurrent operations
        var tasks = new[]
        {
            cut.InvokeAsync(async () => await cut.Instance.Operation1()),
            cut.InvokeAsync(async () => await cut.Instance.Operation2()),
            cut.InvokeAsync(async () => await cut.Instance.Operation3())
        };

        await Task.WhenAll(tasks);
        await Task.Delay(200);

        // Assert: All completed successfully
        cut.Instance.State.CompletedCount.Should().Be(3);
        cut.Instance.State.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task LazyLoad_Caching_WorksCorrectly()
    {
        // Arrange
        Services.AddStore(CacheTestState.Initial);
        StoreTestHelpers.RegisterStoreUtilities<CacheTestState>(Services);
        var cut = RenderComponent<CacheTestComponent>();

        // Act: Load same item multiple times
        await cut.InvokeAsync(async () => await cut.Instance.LoadItem("cached-item"));
        var firstLoadTime = DateTime.Now;
        await Task.Delay(100);

        await cut.InvokeAsync(async () => await cut.Instance.LoadItem("cached-item"));
        await Task.Delay(50);

        // Assert: Item was loaded only once (second call used cache)
        cut.Instance.State.LoadCount.Should().Be(1, "because second call should use cache");
    }

    #region Test Components and States

    // SearchAndLoadComponent - Tests Debounce + ExecuteAsync
    private class SearchAndLoadComponent : StoreComponentWithUtilities<SearchAndLoadState>
    {
        public new SearchAndLoadState State => base.State;

        public void HandleSearch(string query) =>
            UpdateDebounced(s => s with { SearchQuery = query }, 300);

        public async Task LoadResults() =>
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50); // Simulate API call
                    return new List<string> { "Result 1", "Result 2", "Result 3" };
                },
                loading: s => s with { Results = s.Results.ToLoading() },
                success: (s, results) => s with { Results = AsyncData<List<string>>.Success(results) },
                error: (s, ex) => s with { Results = AsyncData<List<string>>.Failure(ex.Message) }
            );
    }

    private record SearchAndLoadState(
        string SearchQuery,
        AsyncData<List<string>> Results)
    {
        public static SearchAndLoadState Initial => new("", AsyncData<List<string>>.NotAsked());
    }

    // ScrollAndLoadComponent - Tests Throttle + LazyLoad
    private class ScrollAndLoadComponent : StoreComponentWithUtilities<ScrollAndLoadState>
    {
        public new ScrollAndLoadState State => base.State;

        public void HandleScroll(int position) =>
            UpdateThrottled(s => s with { ScrollPosition = position }, 100);

        public async Task LoadItem(string itemId)
        {
            var item = await LazyLoad(
                itemId,
                async () =>
                {
                    await Task.Delay(50);
                    return new LoadedItem(itemId, $"Data for {itemId}");
                },
                cacheFor: TimeSpan.FromMinutes(5));

            Update(s => s with { LoadedItems = s.LoadedItems.Add(itemId, item) });
        }
    }

    private record ScrollAndLoadState(
        int ScrollPosition,
        ImmutableDictionary<string, LoadedItem> LoadedItems)
    {
        public static ScrollAndLoadState Initial => new(0, ImmutableDictionary<string, LoadedItem>.Empty);
    }

    private record LoadedItem(string Id, string Data);

    // ComplexAppComponent - Tests all features together
    private class ComplexAppComponent : StoreComponentWithUtilities<ComplexAppState>
    {
        public new ComplexAppState State => base.State;

        public void Search(string query) =>
            UpdateDebounced(s => s with { SearchQuery = query }, 300);

        public async Task LoadResults() =>
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    return new List<Product> { new("product-1", "Laptop", 999m) };
                },
                loading: s => s with { SearchResults = s.SearchResults.ToLoading() },
                success: (s, results) => s with { SearchResults = AsyncData<List<Product>>.Success(results) }
            );

        public async Task LoadProductDetails(string productId)
        {
            var details = await LazyLoad(
                productId,
                async () =>
                {
                    await Task.Delay(50);
                    return new ProductDetails(productId, "Detailed info", 999m, "Description");
                },
                cacheFor: TimeSpan.FromMinutes(5));

            Update(s => s with { ProductCache = s.ProductCache.Add(productId, details) });
        }

        public void TrackScroll(int position) =>
            UpdateThrottled(s => s with { ScrollPosition = position }, 100);
    }

    private record ComplexAppState(
        string SearchQuery,
        AsyncData<List<Product>> SearchResults,
        ImmutableDictionary<string, ProductDetails> ProductCache,
        int ScrollPosition,
        bool HasErrors)
    {
        public static ComplexAppState Initial => new(
            "",
            AsyncData<List<Product>>.NotAsked(),
            ImmutableDictionary<string, ProductDetails>.Empty,
            0,
            false);
    }

    private record Product(string Id, string Name, decimal Price);
    private record ProductDetails(string Id, string Name, decimal Price, string Description);

    // AsyncTransitionComponent - Tests AsyncData state transitions
    private class AsyncTransitionComponent : StoreComponentWithUtilities<AsyncTransitionState>
    {
        public new AsyncTransitionState State => base.State;

        public async Task LoadData() =>
            await ExecuteAsync(
                async () =>
                {
                    // Longer delay to ensure loading state can be observed
                    await Task.Delay(200);
                    return "test data";
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, data) => s with { Data = AsyncData<string>.Success(data) }
            );
    }

    private record AsyncTransitionState(AsyncData<string> Data)
    {
        public static AsyncTransitionState Initial => new(AsyncData<string>.NotAsked());
    }

    // AsyncErrorComponent - Tests error handling
    private class AsyncErrorComponent : StoreComponentWithUtilities<AsyncErrorState>
    {
        public new AsyncErrorState State => base.State;

        public async Task LoadDataWithError() =>
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    throw new InvalidOperationException("Simulated error");
#pragma warning disable CS0162 // Unreachable code detected
                    return "data";
#pragma warning restore CS0162
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, data) => s with { Data = AsyncData<string>.Success(data) },
                error: (s, ex) => s with { Data = AsyncData<string>.Failure(ex.Message) }
            );
    }

    private record AsyncErrorState(AsyncData<string> Data)
    {
        public static AsyncErrorState Initial => new(AsyncData<string>.NotAsked());
    }

    // ConcurrentComponent - Tests concurrent operations
    private class ConcurrentComponent : StoreComponentWithUtilities<ConcurrentState>
    {
        public new ConcurrentState State => base.State;

        public async Task Operation1() =>
            await ExecuteAsync(
                async () => { await Task.Delay(50); },
                loading: s => s,
                success: s => s with { CompletedCount = s.CompletedCount + 1 }
            );

        public async Task Operation2() =>
            await ExecuteAsync(
                async () => { await Task.Delay(50); },
                loading: s => s,
                success: s => s with { CompletedCount = s.CompletedCount + 1 }
            );

        public async Task Operation3() =>
            await ExecuteAsync(
                async () => { await Task.Delay(50); },
                loading: s => s,
                success: s => s with { CompletedCount = s.CompletedCount + 1 }
            );
    }

    private record ConcurrentState(int CompletedCount, bool HasErrors)
    {
        public static ConcurrentState Initial => new(0, false);
    }

    // CacheTestComponent - Tests LazyLoad caching
    private class CacheTestComponent : StoreComponentWithUtilities<CacheTestState>
    {
        public new CacheTestState State => base.State;

        public async Task LoadItem(string itemId)
        {
            await LazyLoad(
                itemId,
                async () =>
                {
                    Update(s => s with { LoadCount = s.LoadCount + 1 });
                    await Task.Delay(50);
                    return "data";
                },
                cacheFor: TimeSpan.FromMinutes(5));
        }
    }

    private record CacheTestState(int LoadCount)
    {
        public static CacheTestState Initial => new(0);
    }

    #endregion
}
