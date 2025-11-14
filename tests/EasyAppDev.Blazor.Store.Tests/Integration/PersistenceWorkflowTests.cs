using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

/// <summary>
/// Integration tests for complete persistence workflows including save, load, and restore operations.
/// </summary>
public class PersistenceWorkflowTests
{
    [Fact]
    public async Task PersistenceWorkflow_SaveLoadRestore()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();

        // Create store with persistence
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithPersistence(provider, "test-state")
            .Build();

        // Act - Update state (should persist)
        await store.UpdateAsync(state => state with { Counter = 42, Message = "Updated" });

        // Wait for persistence
        await Task.Delay(50);

        // Create new middleware to load state
        var middleware = new PersistenceMiddleware<TestState>(provider, "test-state");
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Counter.Should().Be(42);
        loadedState.Message.Should().Be("Updated");

        // Cleanup
        if (store is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task PersistenceWorkflow_WithInMemoryProvider()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();

        // Create store with persistence
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithPersistence(provider, "test-key")
            .Build();

        // Act - Multiple updates
        await store.UpdateAsync(state => state with { Counter = 1 });
        await Task.Delay(10);
        await store.UpdateAsync(state => state with { Counter = 2 });
        await Task.Delay(10);
        await store.UpdateAsync(state => state with { Counter = 3, Message = "Final" });
        await Task.Delay(10);

        // Create new middleware to load state
        var loadMiddleware = new PersistenceMiddleware<TestState>(provider, "test-key");
        var loadedState = await loadMiddleware.LoadStateAsync();

        // Assert - Final state persisted
        loadedState.Should().NotBeNull();
        loadedState!.Counter.Should().Be(3);
        loadedState.Message.Should().Be("Final");

        // Cleanup
        if (store is IDisposable disposable1)
        {
            disposable1.Dispose();
        }
    }

    [Fact]
    public async Task PersistenceWorkflow_Debounce_SavesOnlyLatest()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();

        // Create store with debounced persistence (100ms)
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithPersistence(provider, "debounce-test", debounceMs: 100)
            .Build();

        // Act - Rapid updates
        await store.UpdateAsync(state => state with { Counter = 1 });
        await store.UpdateAsync(state => state with { Counter = 2 });
        await store.UpdateAsync(state => state with { Counter = 3 });

        // Wait for debounce period
        await Task.Delay(150);

        // Load persisted state
        var middleware = new PersistenceMiddleware<TestState>(provider, "debounce-test");
        var loadedState = await middleware.LoadStateAsync();

        // Assert - Only final state persisted
        loadedState.Should().NotBeNull();
        loadedState!.Counter.Should().Be(3);
        provider.Count.Should().Be(1); // Only one save operation

        // Cleanup
        if (store is IDisposable disposable2)
        {
            disposable2.Dispose();
        }
    }

    [Fact]
    public async Task PersistenceWorkflow_MultipleStores_IndependentPersistence()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();

        var store1 = StoreBuilder<TestState>
            .Create(new TestState(0, "Store1"))
            .WithPersistence(provider, "store-1")
            .Build();

        var store2 = StoreBuilder<TestState>
            .Create(new TestState(0, "Store2"))
            .WithPersistence(provider, "store-2")
            .Build();

        // Act - Update both stores
        await store1.UpdateAsync(state => state with { Counter = 10 });
        await store2.UpdateAsync(state => state with { Counter = 20 });
        await Task.Delay(50);

        // Load both states
        var middleware1 = new PersistenceMiddleware<TestState>(provider, "store-1");
        var middleware2 = new PersistenceMiddleware<TestState>(provider, "store-2");

        var state1 = await middleware1.LoadStateAsync();
        var state2 = await middleware2.LoadStateAsync();

        // Assert - Both stores persisted independently
        state1.Should().NotBeNull();
        state1!.Counter.Should().Be(10);
        state1.Message.Should().Be("Store1");

        state2.Should().NotBeNull();
        state2!.Counter.Should().Be(20);
        state2.Message.Should().Be("Store2");

        provider.Count.Should().Be(2); // Two separate keys

        // Cleanup
        if (store1 is IDisposable disposable3)
        {
            disposable3.Dispose();
        }
        if (store2 is IDisposable disposable4)
        {
            disposable4.Dispose();
        }
    }

    [Fact]
    public async Task PersistenceWorkflow_LoadNonExistentKey_ReturnsNull()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();
        var middleware = new PersistenceMiddleware<TestState>(provider, "non-existent");

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().BeNull();
    }

    [Fact]
    public async Task PersistenceWorkflow_RemoveKey_NoLongerPersisted()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithPersistence(provider, "remove-test")
            .Build();

        // Act - Save state
        await store.UpdateAsync(state => state with { Counter = 5 });
        await Task.Delay(50);

        // Verify saved
        (await provider.ContainsKeyAsync("remove-test")).Should().BeTrue();

        // Remove key
        await provider.RemoveAsync("remove-test");

        // Assert
        (await provider.ContainsKeyAsync("remove-test")).Should().BeFalse();
        var middleware = new PersistenceMiddleware<TestState>(provider, "remove-test");
        var loadedState = await middleware.LoadStateAsync();
        loadedState.Should().BeNull();

        // Cleanup
        if (store is IDisposable disposable5)
        {
            disposable5.Dispose();
        }
    }

    [Fact]
    public async Task PersistenceWorkflow_ComplexState_SerializesCorrectly()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();
        var complexState = new ComplexTestState(
            Id: 123,
            Name: "Test",
            Tags: new[] { "tag1", "tag2", "tag3" },
            Metadata: new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            },
            Timestamp: DateTime.UtcNow
        );

        var store = StoreBuilder<ComplexTestState>
            .Create(complexState)
            .WithPersistence(provider, "complex-test")
            .Build();

        // Act - Update and persist
        await store.UpdateAsync(state => state with
        {
            Name = "Updated",
            Tags = new[] { "new-tag" }
        });
        await Task.Delay(50);

        // Load state
        var middleware = new PersistenceMiddleware<ComplexTestState>(provider, "complex-test");
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Id.Should().Be(123);
        loadedState.Name.Should().Be("Updated");
        loadedState.Tags.Should().BeEquivalentTo(new[] { "new-tag" });

        // Cleanup
        if (store is IDisposable disposable6)
        {
            disposable6.Dispose();
        }
    }

    #region Test Helpers

    private record ComplexTestState(
        int Id,
        string Name,
        string[] Tags,
        Dictionary<string, object> Metadata,
        DateTime Timestamp);

    #endregion
}
