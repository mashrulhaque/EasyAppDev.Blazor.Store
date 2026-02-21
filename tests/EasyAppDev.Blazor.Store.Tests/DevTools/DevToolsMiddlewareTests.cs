using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.DevTools;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace EasyAppDev.Blazor.Store.Tests.DevTools;

public record TestState(int Counter, string Message);

/// <summary>
/// Tests for DevToolsMiddleware. These tests only run in DEBUG builds since
/// DevToolsMiddleware is a no-op stub in Release builds for security reasons.
/// </summary>
#if !DEBUG
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
public class DevToolsMiddlewareTests
{
    private static (Mock<IServiceProvider>, Mock<IJSRuntime>, Mock<IJSObjectReference>) CreateMocks()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        return (serviceProviderMock, jsRuntimeMock, jsModuleMock);
    }

    [Fact]
    public async Task OnAfterUpdateAsync_SendsActionToDevTools()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(1, "After");

        // Act
        await middleware.OnBeforeUpdateAsync(previousState, "INCREMENT");
        await middleware.OnAfterUpdateAsync(previousState, currentState, "INCREMENT");

        // Assert - InvokeVoidAsync uses IJSVoidResult internally
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "sendToDevTools",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DevToolsMiddleware<TestState>((IServiceProvider)null!, "TestStore"));
    }

    [Fact]
    public async Task OnAfterUpdateAsync_WhenDevToolsNotAvailable_DoesNotThrow()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("DevTools not available"));

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        // Act & Assert - should not throw
        await middleware.OnBeforeUpdateAsync(new TestState(0, "Test"), null);
        await middleware.OnAfterUpdateAsync(
            new TestState(0, "Before"),
            new TestState(1, "After"),
            "TEST");
    }

    [Fact]
    public async Task OnAfterUpdateAsync_WithNullAction_UsesDefaultActionName()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();
        string? capturedActionName = null;

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sendToDevTools",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length > 0)
                    capturedActionName = args[0]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(1, "After");

        // Act
        await middleware.OnBeforeUpdateAsync(previousState, null);
        await middleware.OnAfterUpdateAsync(previousState, currentState, null);

        // Assert
        capturedActionName.Should().Be("UPDATE_STATE");
    }

    [Fact]
    public async Task DisposeAsync_DisposesJSModuleGracefully()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        // Initialize the middleware
        await middleware.OnBeforeUpdateAsync(new TestState(0, "Test"), "INIT");

        // Act
        await middleware.DisposeAsync();

        // Assert
        jsModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleNotInitialized_DoesNotThrow()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns((IJSRuntime?)null);

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        // Act & Assert - should not throw
        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task WithDevTools_AddsDevToolsMiddleware()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithDevTools(serviceProviderMock.Object, "TestStore")
            .Build();

        await store.UpdateAsync(
            state => state with { Counter = 1 },
            action: "INCREMENT");

        // Assert - DevTools initialization should be called
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task WithDevTools_WhenJSRuntimeNotAvailable_SilentlySkipsDevTools()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns((IJSRuntime?)null);

        // Act - should not throw, just skip DevTools
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithDevTools(serviceProviderMock.Object, "TestStore")
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 }, "INCREMENT");

        // Assert - store should still be usable, just without DevTools
        store.GetState().Counter.Should().Be(1);
    }

    [Fact]
    public async Task DevToolsMiddleware_SerializesStateWithCamelCase()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();
        string? capturedStateJson = null;

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sendToDevTools",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length > 1)
                    capturedStateJson = args[1]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(42, "After");

        // Act
        await middleware.OnBeforeUpdateAsync(previousState, "INCREMENT");
        await middleware.OnAfterUpdateAsync(previousState, currentState, "INCREMENT");

        // Assert
        capturedStateJson.Should().NotBeNull();
        capturedStateJson.Should().Contain("\"counter\":42"); // camelCase
        capturedStateJson.Should().Contain("\"message\":\"After\""); // camelCase
    }

    [Fact]
    public async Task DevToolsMiddleware_WhenJSRuntimeNotInServiceProvider_GracefullyFails()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns((IJSRuntime?)null);

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore");

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(1, "After");

        // Act & Assert - should not throw
        await middleware.OnBeforeUpdateAsync(previousState, "INCREMENT");
        await middleware.OnAfterUpdateAsync(previousState, currentState, "INCREMENT");
    }
}
