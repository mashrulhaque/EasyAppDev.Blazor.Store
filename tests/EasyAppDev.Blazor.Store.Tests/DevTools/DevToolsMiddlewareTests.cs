using EasyAppDev.Blazor.Store.DevTools;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace EasyAppDev.Blazor.Store.Tests.DevTools;

public record TestState(int Counter, string Message);

public class DevToolsMiddlewareTests
{
    [Fact]
    public async Task OnAfterUpdateAsync_SendsActionToDevTools()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        var middleware = new DevToolsMiddleware<TestState>(
            jsRuntimeMock.Object,
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
    public void Constructor_WithNullJSRuntime_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DevToolsMiddleware<TestState>((IJSRuntime)null!, "TestStore"));
    }

    [Fact]
    public async Task OnAfterUpdateAsync_WhenDevToolsNotAvailable_DoesNotThrow()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("DevTools not available"));

        var middleware = new DevToolsMiddleware<TestState>(
            jsRuntimeMock.Object,
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
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        string? capturedActionName = null;

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

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
            jsRuntimeMock.Object,
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
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        var middleware = new DevToolsMiddleware<TestState>(
            jsRuntimeMock.Object,
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
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var middleware = new DevToolsMiddleware<TestState>(
            jsRuntimeMock.Object,
            "TestStore");

        // Act & Assert - should not throw
        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task WithDevTools_AddsDevToolsMiddleware()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithJSRuntime(jsRuntimeMock.Object)
            .WithDevTools("TestStore")
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
    public void WithDevTools_WithoutJSRuntime_SilentlySkipsDevTools()
    {
        // Arrange
        var builder = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"));

        // Act - should not throw, just skip DevTools
#pragma warning disable CS0618 // Type or member is obsolete
        var result = builder.WithDevTools("TestStore");
#pragma warning restore CS0618 // Type or member is obsolete

        // Assert - builder should still be usable, just without DevTools
        result.Should().NotBeNull();
        var store = result.Build();
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task WithDevTools_WithExplicitJSRuntime_WorksCorrectly()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithDevTools(jsRuntimeMock.Object, "TestStore")
            .Build();

        await store.UpdateAsync(
            state => state with { Counter = 1 },
            action: "INCREMENT");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task DevToolsMiddleware_SerializesStateWithCamelCase()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        string? capturedStateJson = null;

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

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
            jsRuntimeMock.Object,
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
}
