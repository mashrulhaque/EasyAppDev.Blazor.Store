using EasyAppDev.Blazor.Store.Utilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Utilities;

public class LazyCacheTests
{
    [Fact]
    public async Task GetOrLoadAsync_FirstCall_LoadsData()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        var result = await cache.GetOrLoadAsync("key1", Loader);

        // Assert
        result.Should().Be("test-data", "because the loader should return the data");
        loadCount.Should().Be(1, "because the loader should be called once");
    }

    [Fact]
    public async Task GetOrLoadAsync_CacheHit_ReturnsCachedDataWithoutReloading()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        var result1 = await cache.GetOrLoadAsync("key1", Loader);
        var result2 = await cache.GetOrLoadAsync("key1", Loader);

        // Assert
        result1.Should().Be("test-data");
        result2.Should().Be("test-data");
        loadCount.Should().Be(1, "because the loader should only be called once (cache hit on second call)");
    }

    [Fact]
    public async Task GetOrLoadAsync_ConcurrentCalls_DeduplicatesRequests()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.Delay(100); // Simulate slow API call
            return "test-data";
        }

        // Act - Fire 5 concurrent requests for same key
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => cache.GetOrLoadAsync("key1", Loader))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBe("test-data");
        loadCount.Should().Be(1, "because concurrent requests should be deduplicated");
    }

    [Fact]
    public async Task GetOrLoadAsync_DifferentKeys_LoadsIndependently()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader(string value)
        {
            loadCount++;
            await Task.CompletedTask;
            return value;
        }

        // Act
        var result1 = await cache.GetOrLoadAsync("key1", () => Loader("data1"));
        var result2 = await cache.GetOrLoadAsync("key2", () => Loader("data2"));

        // Assert
        result1.Should().Be("data1");
        result2.Should().Be("data2");
        loadCount.Should().Be(2, "because different keys should load independently");
    }

    [Fact]
    public async Task GetOrLoadAsync_WithExpiration_ReloadsAfterExpiry()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return $"data-{loadCount}";
        }

        // Act
        var result1 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: TimeSpan.FromMilliseconds(100));
        await Task.Delay(150); // Wait for expiration
        var result2 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: TimeSpan.FromMilliseconds(100));

        // Assert
        result1.Should().Be("data-1");
        result2.Should().Be("data-2");
        loadCount.Should().Be(2, "because cache should reload after expiration");
    }

    [Fact]
    public async Task GetOrLoadAsync_WithoutExpiration_CachesIndefinitely()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        var result1 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: null);
        await Task.Delay(200); // Wait some time
        var result2 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: null);

        // Assert
        result1.Should().Be("test-data");
        result2.Should().Be("test-data");
        loadCount.Should().Be(1, "because data should be cached indefinitely");
    }

    [Fact]
    public async Task GetOrLoadAsync_OnError_DoesNotCacheError()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            if (loadCount == 1)
                throw new InvalidOperationException("Simulated error");
            return "test-data";
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrLoadAsync("key1", Loader));

        // Second call should retry (error not cached)
        var result = await cache.GetOrLoadAsync("key1", Loader);

        result.Should().Be("test-data");
        loadCount.Should().Be(2, "because errors should not be cached");
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntryFromCache()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        await cache.GetOrLoadAsync("key1", Loader);
        await cache.RemoveAsync("key1");
        await cache.GetOrLoadAsync("key1", Loader);

        // Assert
        loadCount.Should().Be(2, "because the cache entry was removed");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader(int id)
        {
            loadCount++;
            await Task.CompletedTask;
            return $"data-{id}";
        }

        // Act
        await cache.GetOrLoadAsync("key1", () => Loader(1));
        await cache.GetOrLoadAsync("key2", () => Loader(2));
        await cache.GetOrLoadAsync("key3", () => Loader(3));

        await cache.ClearAsync();

        await cache.GetOrLoadAsync("key1", () => Loader(1));
        await cache.GetOrLoadAsync("key2", () => Loader(2));

        // Assert
        loadCount.Should().Be(5, "because all entries were cleared (3 initial + 2 reloads)");
    }

    [Fact]
    public async Task CleanupExpiredAsync_RemovesOnlyExpiredEntries()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader(int id)
        {
            loadCount++;
            await Task.CompletedTask;
            return $"data-{id}";
        }

        // Act
        await cache.GetOrLoadAsync("key1", () => Loader(1), cacheFor: TimeSpan.FromMilliseconds(50)); // Expires soon
        await cache.GetOrLoadAsync("key2", () => Loader(2), cacheFor: TimeSpan.FromMinutes(10)); // Expires later
        await cache.GetOrLoadAsync("key3", () => Loader(3), cacheFor: TimeSpan.FromMilliseconds(50)); // Expires soon

        await Task.Delay(100); // Wait for key1 and key3 to expire

        var removedCount = await cache.CleanupExpiredAsync();

        // Assert
        removedCount.Should().Be(2, "because key1 and key3 should have expired");
        cache.Count.Should().Be(1, "because only key2 should remain");
    }

    [Fact]
    public async Task Count_ReturnsCorrectNumberOfEntries()
    {
        // Arrange
        using var cache = new LazyCache();
        async Task<string> Loader(int id)
        {
            await Task.CompletedTask;
            return $"data-{id}";
        }

        // Act & Assert
        cache.Count.Should().Be(0, "because cache is initially empty");

        await cache.GetOrLoadAsync("key1", () => Loader(1));
        cache.Count.Should().Be(1);

        await cache.GetOrLoadAsync("key2", () => Loader(2));
        cache.Count.Should().Be(2);

        await cache.GetOrLoadAsync("key1", () => Loader(1)); // Cache hit, no new entry
        cache.Count.Should().Be(2);

        await cache.RemoveAsync("key1");
        cache.Count.Should().Be(1);

        await cache.ClearAsync();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_ClearsAllEntries()
    {
        // Arrange
        var cache = new LazyCache();
        async Task<string> Loader()
        {
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        await cache.GetOrLoadAsync("key1", Loader);
        cache.Count.Should().Be(1);

        cache.Dispose();

        // Assert - After disposal, accessing Count will throw ObjectDisposedException
        // This verifies that the cache has been properly disposed
        var act = () => { var _ = cache.Count; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetOrLoadAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new LazyCache();
        async Task<string> Loader()
        {
            await Task.CompletedTask;
            return "test-data";
        }

        // Act
        cache.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetOrLoadAsync("key1", Loader));
    }

    [Fact]
    public async Task GetOrLoadAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        using var cache = new LazyCache();
        async Task<string> Loader()
        {
            await Task.CompletedTask;
            return "test-data";
        }

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetOrLoadAsync(null!, Loader));
    }

    [Fact]
    public async Task GetOrLoadAsync_WithNullLoader_ThrowsArgumentNullException()
    {
        // Arrange
        using var cache = new LazyCache();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetOrLoadAsync<string>("key1", null!));
    }

    [Fact]
    public async Task RemoveAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        using var cache = new LazyCache();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.RemoveAsync(null!));
    }

    [Fact]
    public async Task GetOrLoadAsync_DifferentTypes_HandlesCorrectly()
    {
        // Arrange
        using var cache = new LazyCache();

        // Act
        var stringResult = await cache.GetOrLoadAsync("key1", async () =>
        {
            await Task.CompletedTask;
            return "test-string";
        });

        var intResult = await cache.GetOrLoadAsync("key2", async () =>
        {
            await Task.CompletedTask;
            return 42;
        });

        var objectResult = await cache.GetOrLoadAsync("key3", async () =>
        {
            await Task.CompletedTask;
            return new { Name = "Test", Value = 100 };
        });

        // Assert
        stringResult.Should().Be("test-string");
        intResult.Should().Be(42);
        objectResult.Name.Should().Be("Test");
        objectResult.Value.Should().Be(100);
    }

    [Fact]
    public async Task GetOrLoadAsync_WithSlowLoader_AllowsConcurrentRequests()
    {
        // Arrange
        using var cache = new LazyCache();
        var key1Started = false;
        var key2Started = false;

        async Task<string> Loader1()
        {
            key1Started = true;
            await Task.Delay(200);
            return "data1";
        }

        async Task<string> Loader2()
        {
            key2Started = true;
            await Task.Delay(200);
            return "data2";
        }

        // Act - Start both loads concurrently
        var task1 = cache.GetOrLoadAsync("key1", Loader1);
        await Task.Delay(50); // Small delay to ensure task1 starts
        var task2 = cache.GetOrLoadAsync("key2", Loader2);

        await Task.WhenAll(task1, task2);

        // Assert
        key1Started.Should().BeTrue("because key1 loader should start");
        key2Started.Should().BeTrue("because key2 loader should start concurrently");
    }

    [Fact]
    public async Task GetOrLoadAsync_MultipleCallsWithExpiration_RespectsExpirationTiming()
    {
        // Arrange
        using var cache = new LazyCache();
        var loadCount = 0;
        async Task<string> Loader()
        {
            loadCount++;
            await Task.CompletedTask;
            return $"data-{loadCount}";
        }

        // Act
        var result1 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: TimeSpan.FromMilliseconds(200));
        await Task.Delay(100); // Before expiration
        var result2 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: TimeSpan.FromMilliseconds(200));
        await Task.Delay(150); // After expiration
        var result3 = await cache.GetOrLoadAsync("key1", Loader, cacheFor: TimeSpan.FromMilliseconds(200));

        // Assert
        result1.Should().Be("data-1");
        result2.Should().Be("data-1", "because cache should still be valid");
        result3.Should().Be("data-2", "because cache should have expired");
        loadCount.Should().Be(2, "because loader should be called twice (initial + after expiration)");
    }
}
