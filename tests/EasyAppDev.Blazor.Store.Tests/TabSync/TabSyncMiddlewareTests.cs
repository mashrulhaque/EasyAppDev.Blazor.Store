// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.TabSync;

/// <summary>
/// Tests for the TabSync middleware receive path (store wiring via
/// IStoreAwareMiddleware), debounced broadcasting, and channel naming.
/// </summary>
public class TabSyncMiddlewareTests
{
    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static IServiceProvider BuildServiceProvider(IJSRuntime? jsRuntime = null)
    {
        var services = new ServiceCollection();
        if (jsRuntime != null)
        {
            services.AddSingleton(jsRuntime);
        }
        return services.BuildServiceProvider();
    }

    private static string BuildSyncMessage(TestState state, string action = "TEST_ACTION", string tabId = "other-tab")
    {
        var stateJson = JsonSerializer.Serialize(state, JsonOptions);
        var message = new
        {
            tabId,
            action,
            state = stateJson,
            timestamp = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(message, JsonOptions);
    }

    [Fact]
    public void TabSyncMiddleware_ImplementsIStoreAwareMiddleware()
    {
        // Arrange
        var middleware = new TabSyncMiddleware<TestState>(BuildServiceProvider());

        // Assert - StoreBuilder.Build wires the store through this interface
        middleware.Should().BeAssignableTo<IStoreAwareMiddleware<TestState>>();
    }

    [Fact]
    public async Task OnMessageReceived_AfterAttachStore_AppliesStateFromOtherTab()
    {
        // Arrange
        var middleware = new TabSyncMiddleware<TestState>(BuildServiceProvider());
        var store = StoreBuilder<TestState>.Create(new TestState { Count = 0 }).Build();

        ((IStoreAwareMiddleware<TestState>)middleware).AttachStore(store);

        var messageJson = BuildSyncMessage(new TestState { Count = 42, Name = "from-other-tab" });

        // Act - invoke the JS message handler directly
        await middleware.OnMessageReceived(messageJson);

        // Assert - the received state must be applied to the store
        store.GetState().Count.Should().Be(42);
        store.GetState().Name.Should().Be("from-other-tab");

        store.Dispose();
        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task OnMessageReceived_WithoutAttachedStore_IsIgnored()
    {
        // Arrange - no AttachStore call
        var middleware = new TabSyncMiddleware<TestState>(BuildServiceProvider());
        var messageJson = BuildSyncMessage(new TestState { Count = 42 });

        // Act & Assert - must not throw
        await middleware.OnMessageReceived(messageJson);

        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task AttachStore_IsIdempotent_KeepsFirstStore()
    {
        // Arrange
        var middleware = new TabSyncMiddleware<TestState>(BuildServiceProvider());
        var firstStore = StoreBuilder<TestState>.Create(new TestState { Count = 0 }).Build();
        var secondStore = StoreBuilder<TestState>.Create(new TestState { Count = 100 }).Build();

        var storeAware = (IStoreAwareMiddleware<TestState>)middleware;

        // Act - attach twice (Build may call AttachStore again defensively)
        storeAware.AttachStore(firstStore);
        storeAware.AttachStore(secondStore);

        await middleware.OnMessageReceived(BuildSyncMessage(new TestState { Count = 7 }));

        // Assert - the first attached store receives the update; the second is untouched
        firstStore.GetState().Count.Should().Be(7);
        secondStore.GetState().Count.Should().Be(100);

        firstStore.Dispose();
        secondStore.Dispose();
        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task OnAfterUpdate_WithDebounce_CoalescesRapidUpdatesIntoSingleBroadcast()
    {
        // Arrange
        var postCount = 0;
        string? lastPostedMessage = null;

        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<bool>("__initTabSync", It.IsAny<object?[]?>()))
            .Returns(new ValueTask<bool>(true));
        mockJs.Setup(js => js.InvokeAsync<IJSVoidResult>("__postTabSyncMessage", It.IsAny<object?[]?>()))
            .Callback<string, object?[]?>((_, args) =>
            {
                Interlocked.Increment(ref postCount);
                Volatile.Write(ref lastPostedMessage, args?[1] as string);
            })
            .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        var middleware = new TabSyncMiddleware<TestState>(
            BuildServiceProvider(mockJs.Object),
            new TabSyncOptions { DebounceMs = 100 });

        var storeMiddleware = (IMiddleware<TestState>)middleware;

        // Act - N rapid updates; broadcasts must coalesce (last write wins)
        // and OnAfterUpdateAsync must not stall for the debounce duration.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 1; i <= 10; i++)
        {
            await storeMiddleware.OnAfterUpdateAsync(
                new TestState { Count = i - 1 },
                new TestState { Count = i },
                "INCREMENT");
        }
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "OnAfterUpdateAsync must return promptly instead of awaiting the debounce inline");

        await Task.Delay(500);

        // Assert - a single broadcast carrying the final state
        postCount.Should().Be(1, "rapid updates must be coalesced into a single broadcast");
        lastPostedMessage.Should().NotBeNull();

        using var doc = JsonDocument.Parse(lastPostedMessage!);
        var stateJson = doc.RootElement.GetProperty("state").GetString();
        stateJson.Should().Contain("\"count\":10", "the final state must win");

        await middleware.DisposeAsync();
    }

    [Fact]
    public async Task DefaultChannelName_UsesFullTypeName()
    {
        // Arrange
        string? channelName = null;

        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<bool>("__initTabSync", It.IsAny<object?[]?>()))
            .Callback<string, object?[]?>((_, args) => channelName = args?[0] as string)
            .Returns(new ValueTask<bool>(true));
        mockJs.Setup(js => js.InvokeAsync<IJSVoidResult>("__postTabSyncMessage", It.IsAny<object?[]?>()))
            .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        var middleware = new TabSyncMiddleware<TestState>(BuildServiceProvider(mockJs.Object));
        var storeMiddleware = (IMiddleware<TestState>)middleware;

        // Act - triggering a broadcast initializes the channel
        await storeMiddleware.OnAfterUpdateAsync(
            new TestState { Count = 0 },
            new TestState { Count = 1 },
            "INCREMENT");

        // Assert - simple type names collide across namespaces; the full name must be used
        channelName.Should().NotBeNull();
        channelName.Should().Be($"store-{typeof(TestState).FullName}");
        channelName.Should().Contain("TabSyncMiddlewareTests");

        await middleware.DisposeAsync();
    }
}
