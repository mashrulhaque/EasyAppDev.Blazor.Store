using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.DevTools;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace EasyAppDev.Blazor.Store.Tests.DevTools;

public record EnhancedTestState(int Counter, string Message);

public record SecretState(string Username, string Password);

/// <summary>
/// Tests for EnhancedDevToolsMiddleware: runtime gating, time-travel index
/// mapping (DevTools actionId 0 = @@INIT), trim-offset handling, and
/// sensitive-data filtering.
/// </summary>
public class EnhancedDevToolsMiddlewareTests
{
    private static (Mock<IServiceProvider>, Mock<IJSRuntime>, Mock<IJSObjectReference>) CreateMocks()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        return (serviceProviderMock, jsRuntimeMock, jsModuleMock);
    }

    private static async Task<(EnhancedDevToolsMiddleware<EnhancedTestState>, IStore<EnhancedTestState>, Mock<IJSObjectReference>)>
        CreateTimeTravelSetupAsync(int maxHistory = 100)
    {
        var (serviceProviderMock, _, jsModuleMock) = CreateMocks();

        var options = new DevToolsOptions<EnhancedTestState>
        {
            Enabled = true,
            EnableTimeTravel = true,
            MaxHistory = maxHistory,
            Name = "TimeTravelStore"
        };

        var middleware = new EnhancedDevToolsMiddleware<EnhancedTestState>(
            serviceProviderMock.Object,
            options);

        var store = StoreBuilder<EnhancedTestState>
            .Create(new EnhancedTestState(0, "init"))
            .Build();

        middleware.AttachStore(store);

        // Initialize via the first OnBeforeUpdate
        await middleware.OnBeforeUpdateAsync(new EnhancedTestState(0, "init"), "A1");

        return (middleware, store, jsModuleMock);
    }

    [Fact]
    public async Task JumpToState_ActionIdZero_RestoresInitialState()
    {
        // Arrange
        var (middleware, store, _) = await CreateTimeTravelSetupAsync();

        var initial = new EnhancedTestState(0, "init");
        var s1 = new EnhancedTestState(1, "s1");
        var s2 = new EnhancedTestState(2, "s2");

        await middleware.OnAfterUpdateAsync(initial, s1, "A1");
        await middleware.OnAfterUpdateAsync(s1, s2, "A2");
        await store.UpdateAsync(_ => s2);

        // Act - DevTools actionId 0 is @@INIT
        await middleware.JumpToStateAsync(0);

        // Assert
        store.GetState().Should().Be(initial);
    }

    [Fact]
    public async Task JumpToState_ActionIdN_RestoresNthActionPostState()
    {
        // Arrange
        var (middleware, store, _) = await CreateTimeTravelSetupAsync();

        var initial = new EnhancedTestState(0, "init");
        var s1 = new EnhancedTestState(1, "s1");
        var s2 = new EnhancedTestState(2, "s2");

        await middleware.OnAfterUpdateAsync(initial, s1, "A1");
        await middleware.OnAfterUpdateAsync(s1, s2, "A2");
        await store.UpdateAsync(_ => s2);

        // Act - actionId 1 is the first action's post-state (previously this was
        // off by one: _history[1] would have been used)
        await middleware.JumpToStateAsync(1);

        // Assert
        store.GetState().Should().Be(s1);
    }

    [Fact]
    public async Task JumpToState_AfterTrim_AdjustsForTrimOffset()
    {
        // Arrange - MaxHistory 2 means the first entry gets trimmed
        var (middleware, store, _) = await CreateTimeTravelSetupAsync(maxHistory: 2);

        var initial = new EnhancedTestState(0, "init");
        var s1 = new EnhancedTestState(1, "s1");
        var s2 = new EnhancedTestState(2, "s2");
        var s3 = new EnhancedTestState(3, "s3");

        await middleware.OnAfterUpdateAsync(initial, s1, "A1");
        await middleware.OnAfterUpdateAsync(s1, s2, "A2");
        await middleware.OnAfterUpdateAsync(s2, s3, "A3"); // trims s1, trimOffset = 1
        await store.UpdateAsync(_ => s3);

        // Act - actionId 2 (second action) maps to history index 2 - 1 - 1 = 0 (s2)
        await middleware.JumpToStateAsync(2);

        // Assert
        store.GetState().Should().Be(s2);
    }

    [Fact]
    public async Task JumpToState_TrimmedActionId_IsIgnored()
    {
        // Arrange
        var (middleware, store, _) = await CreateTimeTravelSetupAsync(maxHistory: 2);

        var initial = new EnhancedTestState(0, "init");
        var s1 = new EnhancedTestState(1, "s1");
        var s2 = new EnhancedTestState(2, "s2");
        var s3 = new EnhancedTestState(3, "s3");

        await middleware.OnAfterUpdateAsync(initial, s1, "A1");
        await middleware.OnAfterUpdateAsync(s1, s2, "A2");
        await middleware.OnAfterUpdateAsync(s2, s3, "A3"); // s1 trimmed
        await store.UpdateAsync(_ => s3);

        // Act - actionId 1 was trimmed; out-of-range jumps log and are ignored
        await middleware.JumpToStateAsync(1);

        // Assert - state unchanged
        store.GetState().Should().Be(s3);
    }

    [Fact]
    public async Task JumpToState_OutOfRangeActionId_IsIgnored()
    {
        // Arrange
        var (middleware, store, _) = await CreateTimeTravelSetupAsync();

        var initial = new EnhancedTestState(0, "init");
        var s1 = new EnhancedTestState(1, "s1");

        await middleware.OnAfterUpdateAsync(initial, s1, "A1");
        await store.UpdateAsync(_ => s1);

        // Act
        await middleware.JumpToStateAsync(99);

        // Assert - state unchanged
        store.GetState().Should().Be(s1);
    }

    [Fact]
    public async Task OnAfterUpdateAsync_FiltersSensitiveData()
    {
        // Arrange - DevToolsOptions enables SensitiveDataFilter by default
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var jsModuleMock = new Mock<IJSObjectReference>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        string? capturedStateJson = null;
        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("sendEnhancedAction", It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                // args: [storeName, actionJson, stateJson, performanceJson]
                if (args.Length > 2)
                    capturedStateJson = args[2]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var options = DevToolsOptions<SecretState>.Default("SecretStore");
        options.Enabled = true;

        var middleware = new EnhancedDevToolsMiddleware<SecretState>(
            serviceProviderMock.Object,
            options);

        var prev = new SecretState("alice", "hunter2");
        var curr = new SecretState("alice", "hunter3");

        // Act
        await middleware.OnBeforeUpdateAsync(prev, "CHANGE_PASSWORD");
        await middleware.OnAfterUpdateAsync(prev, curr, "CHANGE_PASSWORD");

        // Assert - the password value must not leak to DevTools
        capturedStateJson.Should().NotBeNull();
        capturedStateJson.Should().NotContain("hunter3");
        capturedStateJson.Should().Contain("alice");
    }

    [Fact]
    public async Task OnAfterUpdateAsync_PassesStoreNameToJs()
    {
        // Arrange - multi-store support: JS keys connections by store name
        var (serviceProviderMock, _, jsModuleMock) = CreateMocks();

        string? capturedStoreName = null;
        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("sendEnhancedAction", It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                if (args.Length > 0)
                    capturedStoreName = args[0]?.ToString();
            })
            .Returns(new ValueTask<IJSVoidResult>());

        var middleware = new EnhancedDevToolsMiddleware<EnhancedTestState>(
            serviceProviderMock.Object,
            new DevToolsOptions<EnhancedTestState> { Enabled = true, Name = "StoreB" });

        var prev = new EnhancedTestState(0, "a");
        var curr = new EnhancedTestState(1, "b");

        // Act
        await middleware.OnBeforeUpdateAsync(prev, "X");
        await middleware.OnAfterUpdateAsync(prev, curr, "X");

        // Assert
        capturedStoreName.Should().Be("StoreB");
    }

    [Fact]
    public async Task Middleware_WhenDisabled_DoesNotTouchJsRuntime()
    {
        // Arrange
        var (serviceProviderMock, jsRuntimeMock, _) = CreateMocks();

        var middleware = new EnhancedDevToolsMiddleware<EnhancedTestState>(
            serviceProviderMock.Object,
            new DevToolsOptions<EnhancedTestState> { Enabled = false });

        // Act
        await middleware.OnBeforeUpdateAsync(new EnhancedTestState(0, "a"), "X");
        await middleware.OnAfterUpdateAsync(
            new EnhancedTestState(0, "a"),
            new EnhancedTestState(1, "b"),
            "X");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureInitialized_PrerenderInvalidOperation_RetriesOnNextUpdate()
    {
        // Arrange
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
                    throw new InvalidOperationException("Prerendering - JS interop unavailable");
                }
                return new ValueTask<IJSObjectReference>(jsModuleMock.Object);
            });

        serviceProviderMock
            .Setup(x => x.GetService(typeof(IJSRuntime)))
            .Returns(jsRuntimeMock.Object);

        var middleware = new EnhancedDevToolsMiddleware<EnhancedTestState>(
            serviceProviderMock.Object,
            new DevToolsOptions<EnhancedTestState> { Enabled = true });

        // Act
        await middleware.OnBeforeUpdateAsync(new EnhancedTestState(0, "a"), "A");
        await middleware.OnBeforeUpdateAsync(new EnhancedTestState(1, "b"), "B");

        // Assert - initialization retried after prerender failure
        callCount.Should().Be(2);
    }
}
