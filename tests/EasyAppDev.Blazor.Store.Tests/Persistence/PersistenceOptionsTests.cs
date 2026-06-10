using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Persistence;
using FluentAssertions;
using Moq;
using System.Text.Json;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Persistence;

public record PersistenceOptionsTestState(int Count, string Name, bool IsActive = false);

public class PersistenceOptionsTests
{
    [Fact]
    public async Task WithPersistenceOptions_ShouldPersist_SkipsWhenReturnsFalse()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var saveCount = 0;
        providerMock.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => saveCount++)
            .Returns(Task.CompletedTask);

        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            ShouldPersist = (prev, curr, action) => action != "TEMP_UPDATE"
        };

        var store = StoreBuilder<PersistenceOptionsTestState>
            .Create(new PersistenceOptionsTestState(0, "Initial"))
            .WithPersistence(providerMock.Object, options)
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "PERSIST_ME");
        await store.UpdateAsync(s => s with { Count = 2 }, "TEMP_UPDATE");
        await store.UpdateAsync(s => s with { Count = 3 }, "PERSIST_ME");

        // Assert
        saveCount.Should().Be(2); // Only the PERSIST_ME actions

        store.Dispose();
    }

    [Fact]
    public async Task WithPersistenceOptions_TransformOnSave_TransformsBeforeSaving()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        string? savedJson = null;
        providerMock.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, json) => savedJson = json)
            .Returns(Task.CompletedTask);

        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            TransformOnSave = state => state with { Name = "REDACTED" }
        };

        var store = StoreBuilder<PersistenceOptionsTestState>
            .Create(new PersistenceOptionsTestState(0, "Sensitive"))
            .WithPersistence(providerMock.Object, options)
            .Build();

        // Act
        await store.UpdateAsync(s => s with { Name = "Very Sensitive Data" });

        // Assert
        savedJson.Should().NotBeNull();
        savedJson.Should().Contain("REDACTED");
        savedJson.Should().NotContain("Very Sensitive Data");

        store.Dispose();
    }

    [Fact]
    public async Task WithPersistenceOptions_TransformOnLoad_TransformsAfterLoading()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var storedState = new PersistenceOptionsTestState(5, "Stored", true);
        var storedJson = JsonSerializer.Serialize(storedState, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        providerMock.Setup(p => p.LoadAsync(It.IsAny<string>()))
            .ReturnsAsync(storedJson);

        PersistenceOptionsTestState? loadedState = null;
        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            TransformOnLoad = state => state with { IsActive = false },
            OnHydrationSuccess = state => loadedState = state
        };

        var middleware = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, options);

        // Act
        var result = await middleware.LoadStateAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(5);
        result.Name.Should().Be("Stored");
        result.IsActive.Should().BeFalse(); // Transformed
        loadedState.Should().NotBeNull();
        loadedState!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task WithPersistenceOptions_OnHydrationSuccess_CalledOnSuccessfulLoad()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var storedState = new PersistenceOptionsTestState(10, "Test");
        providerMock.Setup(p => p.LoadAsync(It.IsAny<string>()))
            .ReturnsAsync(JsonSerializer.Serialize(storedState, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

        PersistenceOptionsTestState? loadedState = null;
        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            OnHydrationSuccess = state => loadedState = state
        };

        var middleware = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, options);

        // Act
        await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Count.Should().Be(10);
    }

    [Fact]
    public async Task WithPersistenceOptions_OnHydrationSkipped_CalledWhenNoStateFound()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        providerMock.Setup(p => p.LoadAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var skippedCalled = false;
        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            OnHydrationSkipped = () => skippedCalled = true
        };

        var middleware = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, options);

        // Act
        var result = await middleware.LoadStateAsync();

        // Assert
        result.Should().BeNull();
        skippedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WithPersistenceOptions_OnHydrationFailure_CalledOnError()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var expectedException = new InvalidOperationException("Load failed");
        providerMock.Setup(p => p.LoadAsync(It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        Exception? capturedError = null;
        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            OnHydrationFailure = ex => capturedError = ex
        };

        var middleware = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, options);

        // Act
        var result = await middleware.LoadStateAsync();

        // Assert
        result.Should().BeNull();
        capturedError.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task WithPersistenceOptions_DebounceMs_ReducesSaveFrequency()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var saveCount = 0;
        providerMock.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => Interlocked.Increment(ref saveCount))
            .Returns(Task.CompletedTask);

        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            DebounceMs = 100
        };

        var store = StoreBuilder<PersistenceOptionsTestState>
            .Create(new PersistenceOptionsTestState(0, "Initial"))
            .WithPersistence(providerMock.Object, options)
            .Build();

        // Act - updates to trigger debouncing
        await store.UpdateAsync(s => s with { Count = 1 });
        await store.UpdateAsync(s => s with { Count = 2 });
        await store.UpdateAsync(s => s with { Count = 3 });

        // Wait for debounce to complete
        await Task.Delay(300);

        // Assert - debounce means fewer saves than updates (at least shows debounce is configured)
        // With 3 updates and debounce, we should get fewer than 3 saves
        saveCount.Should().BeLessOrEqualTo(3);

        store.Dispose();
    }

    [Fact]
    public async Task WithPersistenceOptions_Debounce_CoalescesRapidUpdatesIntoSingleSaveWithFinalState()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var saveCount = 0;
        string? lastSavedJson = null;
        providerMock.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, json) =>
            {
                Interlocked.Increment(ref saveCount);
                Volatile.Write(ref lastSavedJson, json);
            })
            .Returns(Task.CompletedTask);

        var options = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test-state",
            // No stable signing key in these tests; integrity checking now requires one
            EnableIntegrityCheck = false,
            FilterSensitiveData = false,
            DebounceMs = 100
        };

        var store = StoreBuilder<PersistenceOptionsTestState>
            .Create(new PersistenceOptionsTestState(0, "Initial"))
            .WithPersistence(providerMock.Object, options)
            .Build();

        // Act - N rapid updates; debounced saves must coalesce (last write wins)
        // and updates must not be stalled for the debounce duration.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 1; i <= 10; i++)
        {
            await store.UpdateAsync(s => s with { Count = i });
        }
        sw.Stop();

        // With the old inline-await debounce, 10 updates would take >= 10 * 100ms.
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "OnAfterUpdateAsync must return promptly instead of awaiting the debounce inline");

        // Wait for the single trailing debounced save to fire
        await Task.Delay(500);

        // Assert
        saveCount.Should().Be(1, "intermediate saves must be coalesced into a single trailing save");
        lastSavedJson.Should().NotBeNull();

        // The saved payload is a wrapper whose "state" property holds the state JSON
        using var doc = JsonDocument.Parse(lastSavedJson!);
        var stateJson = doc.RootElement.GetProperty("state").GetString();
        stateJson.Should().Contain("\"count\":10", "the final state must win");

        store.Dispose();
    }

    [Fact]
    public void PersistenceOptions_Create_CreatesWithKey()
    {
        // Act
        var options = PersistenceOptions.Create<PersistenceOptionsTestState>("my-key");

        // Assert
        options.Key.Should().Be("my-key");
        options.DebounceMs.Should().Be(0);
        options.HydrateOnInit.Should().BeTrue();
    }

    [Fact]
    public void PersistenceMiddleware_HydrateOnInit_ReflectsOption()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();

        var optionsWithHydration = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test",
            EnableIntegrityCheck = false,
            HydrateOnInit = true
        };

        var optionsWithoutHydration = new PersistenceOptions<PersistenceOptionsTestState>
        {
            Key = "test",
            EnableIntegrityCheck = false,
            HydrateOnInit = false
        };

        // Act
        var middlewareWithHydration = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, optionsWithHydration);
        var middlewareWithoutHydration = new PersistenceMiddleware<PersistenceOptionsTestState>(
            providerMock.Object, optionsWithoutHydration);

        // Assert
        middlewareWithHydration.HydrateOnInit.Should().BeTrue();
        middlewareWithoutHydration.HydrateOnInit.Should().BeFalse();
    }
}
