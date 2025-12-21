using EasyAppDev.Blazor.Store.Persistence;
using FluentAssertions;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Persistence;

public record TestState(int Counter, string Message);

public class PersistenceTests
{
    [Fact]
    public async Task PersistenceMiddleware_SavesStateAfterUpdate()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key");

        var state = new TestState(1, "Updated");

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState(0, "Initial"),
            state,
            "UPDATE");

        // Assert
        providerMock.Verify(
            x => x.SaveAsync("test-key", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task PersistenceMiddleware_SerializesStateCorrectly()
    {
        // Arrange
        string? savedJson = null;
        var providerMock = new Mock<IPersistenceProvider>();
        providerMock
            .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((key, value) => savedJson = value)
            .Returns(Task.CompletedTask);

        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key");

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState(0, "Before"),
            new TestState(5, "After"),
            "UPDATE");

        // Assert
        savedJson.Should().NotBeNull();
        // The state is wrapped in a PersistedStateWrapper, so check the 'state' field
        savedJson.Should().Contain("\"state\":");
        savedJson.Should().Contain("counter");
        savedJson.Should().Contain("After");
    }

    [Fact]
    public async Task LoadStateAsync_ReturnsPersistedState()
    {
        // Arrange
        var json = "{\"counter\":10,\"message\":\"Persisted\"}";
        var providerMock = new Mock<IPersistenceProvider>();
        providerMock
            .Setup(x => x.LoadAsync("test-key"))
            .ReturnsAsync(json);

        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key");

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Counter.Should().Be(10);
        loadedState.Message.Should().Be("Persisted");
    }

    [Fact]
    public async Task LoadStateAsync_WhenNoPersistedState_ReturnsNull()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        providerMock
            .Setup(x => x.LoadAsync("test-key"))
            .ReturnsAsync((string?)null);

        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key");

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().BeNull();
    }

    [Fact]
    public async Task PersistenceMiddleware_WithDebounce_SavesAfterDelay()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key",
            debounceMs: 100);

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState(0, "Before"),
            new TestState(1, "After"),
            "UPDATE");

        // Wait for debounce
        await Task.Delay(150);

        // Assert
        providerMock.Verify(
            x => x.SaveAsync("test-key", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task OnBeforeUpdateAsync_DoesNothing()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();
        var middleware = new PersistenceMiddleware<TestState>(
            providerMock.Object,
            "test-key");

        // Act
        await middleware.OnBeforeUpdateAsync(new TestState(0, "Test"), "ACTION");

        // Assert
        providerMock.Verify(
            x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void PersistenceMiddleware_WithNullProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PersistenceMiddleware<TestState>(null!, "test-key"));
    }

    [Fact]
    public void PersistenceMiddleware_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var providerMock = new Mock<IPersistenceProvider>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PersistenceMiddleware<TestState>(providerMock.Object, key: null!));
    }
}
