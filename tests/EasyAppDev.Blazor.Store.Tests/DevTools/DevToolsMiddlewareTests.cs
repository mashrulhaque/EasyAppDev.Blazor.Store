using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.DevTools;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace EasyAppDev.Blazor.Store.Tests.DevTools;

public record TestState(int Counter, string Message);

/// <summary>
/// Tests for DevToolsMiddleware. DevTools are now gated at RUNTIME via
/// DevToolsOptions.Enabled (default: only when a debugger is attached),
/// so tests explicitly enable them.
/// </summary>
public class DevToolsMiddlewareTests
{
    private static DevToolsOptions<TestState> EnabledOptions() => new()
    {
        Enabled = true
    };

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
            "TestStore",
            EnabledOptions());

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
    public async Task OnAfterUpdateAsync_WhenDisabled_DoesNotTouchJsRuntime()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            new DevToolsOptions<TestState> { Enabled = false });

        // Act
        await middleware.OnBeforeUpdateAsync(new TestState(0, "Before"), "INCREMENT");
        await middleware.OnAfterUpdateAsync(new TestState(0, "Before"), new TestState(1, "After"), "INCREMENT");

        // Assert - no JS interaction at all when disabled
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Never);
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("sendToDevTools", It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureInitialized_PrerenderInvalidOperation_RetriesOnNextUpdate()
    {
        // Arrange - first import throws InvalidOperationException (prerendering),
        // subsequent calls succeed. The failure must NOT latch permanently.
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        var callCount = 0;
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException(
                        "JavaScript interop calls cannot be issued at this time (prerendering).");
                }
                return new ValueTask<IJSObjectReference>(jsModuleMock.Object);
            });

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

        var prev = new TestState(0, "Before");
        var curr = new TestState(1, "After");

        // Act - first update happens during "prerendering"
        await middleware.OnBeforeUpdateAsync(prev, "A");
        await middleware.OnAfterUpdateAsync(prev, curr, "A");

        // Second update: interop is now available, initialization should retry
        await middleware.OnBeforeUpdateAsync(curr, "B");
        await middleware.OnAfterUpdateAsync(curr, new TestState(2, "Later"), "B");

        // Assert - retried and eventually sent an action
        callCount.Should().Be(2);
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("sendToDevTools", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureInitialized_GenuineJsFailure_LatchesAndDoesNotRetry()
    {
        // Arrange - JSException is a genuine failure and should latch
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("module not found"));

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

        var prev = new TestState(0, "Before");

        // Act
        await middleware.OnBeforeUpdateAsync(prev, "A");
        await middleware.OnBeforeUpdateAsync(prev, "B");
        await middleware.OnBeforeUpdateAsync(prev, "C");

        // Assert - only attempted once, then latched
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
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
            "TestStore",
            EnabledOptions());

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
                // args: [storeName, actionName, stateJson]
                if (args.Length > 1)
                    capturedActionName = args[1]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(1, "After");

        // Act
        await middleware.OnBeforeUpdateAsync(previousState, null);
        await middleware.OnAfterUpdateAsync(previousState, currentState, null);

        // Assert
        capturedActionName.Should().Be("UPDATE_STATE");
    }

    [Fact]
    public async Task OnAfterUpdateAsync_PassesStoreNameToJs()
    {
        // Arrange - multi-store support: the JS module keys connections by store name
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();
        string? capturedStoreName = null;

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sendToDevTools",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length > 0)
                    capturedStoreName = args[0]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "MySecondStore",
            EnabledOptions());

        // Act
        await middleware.OnBeforeUpdateAsync(new TestState(0, "Before"), "X");
        await middleware.OnAfterUpdateAsync(new TestState(0, "Before"), new TestState(1, "After"), "X");

        // Assert
        capturedStoreName.Should().Be("MySecondStore");
    }

    [Fact]
    public async Task DisposeAsync_DisposesJSModuleGracefully()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

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
            "TestStore",
            EnabledOptions());

        // Act & Assert - should not throw
        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task WithMiddleware_EnabledDevTools_InitializesOnUpdate()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, jsModuleMock) = CreateMocks();

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
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
                // args: [storeName, actionName, stateJson]
                if (args.Length > 2)
                    capturedStateJson = args[2]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new DevToolsMiddleware<TestState>(
            serviceProviderMock.Object,
            "TestStore",
            EnabledOptions());

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
            "TestStore",
            EnabledOptions());

        var previousState = new TestState(0, "Before");
        var currentState = new TestState(1, "After");

        // Act & Assert - should not throw
        await middleware.OnBeforeUpdateAsync(previousState, "INCREMENT");
        await middleware.OnAfterUpdateAsync(previousState, currentState, "INCREMENT");
    }
}
