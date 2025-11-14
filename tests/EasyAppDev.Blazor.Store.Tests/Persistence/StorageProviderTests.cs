using EasyAppDev.Blazor.Store.Persistence;
using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Persistence;

public class StorageProviderTests
{
    [Fact]
    public async Task LocalStorageProvider_SavesAndLoads()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act
        await provider.SaveAsync("test-key", "test-value");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.setItem",
                It.Is<object[]>(args =>
                    args[0].ToString() == "test-key" &&
                    args[1].ToString() == "test-value")),
            Times.Once);
    }

    [Fact]
    public async Task LocalStorageProvider_LoadAsync_InvokesGetItem()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("stored-value");

        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.LoadAsync("test-key");

        // Assert
        result.Should().Be("stored-value");
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<string?>(
                "localStorage.getItem",
                It.Is<object[]>(args => args[0].ToString() == "test-key")),
            Times.Once);
    }

    [Fact]
    public async Task LocalStorageProvider_RemoveAsync_InvokesRemoveItem()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act
        await provider.RemoveAsync("test-key");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object[]>(args => args[0].ToString() == "test-key")),
            Times.Once);
    }

    [Fact]
    public async Task LocalStorageProvider_ContainsKeyAsync_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("some-value");

        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.ContainsKeyAsync("test-key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LocalStorageProvider_ContainsKeyAsync_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.ContainsKeyAsync("test-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void LocalStorageProvider_WithNullJSRuntime_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LocalStorageProvider(null!));
    }

    [Fact]
    public async Task LocalStorageProvider_SaveAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.SaveAsync("", "value"));
    }

    [Fact]
    public async Task LocalStorageProvider_SaveAsync_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new LocalStorageProvider(jsRuntimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider.SaveAsync("key", null!));
    }

    [Fact]
    public async Task SessionStorageProvider_SavesAndLoads()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act
        await provider.SaveAsync("test-key", "test-value");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.setItem",
                It.Is<object[]>(args =>
                    args[0].ToString() == "test-key" &&
                    args[1].ToString() == "test-value")),
            Times.Once);
    }

    [Fact]
    public async Task SessionStorageProvider_LoadAsync_InvokesGetItem()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("sessionStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("stored-value");

        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.LoadAsync("test-key");

        // Assert
        result.Should().Be("stored-value");
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<string?>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args[0].ToString() == "test-key")),
            Times.Once);
    }

    [Fact]
    public async Task SessionStorageProvider_RemoveAsync_InvokesRemoveItem()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act
        await provider.RemoveAsync("test-key");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.removeItem",
                It.Is<object[]>(args => args[0].ToString() == "test-key")),
            Times.Once);
    }

    [Fact]
    public async Task SessionStorageProvider_ContainsKeyAsync_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("sessionStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("some-value");

        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.ContainsKeyAsync("test-key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SessionStorageProvider_ContainsKeyAsync_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("sessionStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act
        var result = await provider.ContainsKeyAsync("test-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SessionStorageProvider_WithNullJSRuntime_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SessionStorageProvider(null!));
    }

    [Fact]
    public async Task SessionStorageProvider_SaveAsync_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.SaveAsync("", "value"));
    }

    [Fact]
    public async Task SessionStorageProvider_SaveAsync_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var provider = new SessionStorageProvider(jsRuntimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider.SaveAsync("key", null!));
    }
}
