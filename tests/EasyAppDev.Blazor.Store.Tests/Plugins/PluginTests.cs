// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Plugins;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Plugins;

public record PluginTestState(int Count);

public class TestPlugin : StorePluginBase<PluginTestState>
{
    public bool ConfigureCalled { get; private set; }
    public bool OnStoreCreatedCalled { get; private set; }
    public bool OnBeforeUpdateCalled { get; private set; }
    public bool OnAfterUpdateCalled { get; private set; }
    public bool OnStoreDisposingCalled { get; private set; }

    public override string Name => "TestPlugin";
    public override Version Version => new(1, 0, 0);

    public override void Configure(StoreBuilder<PluginTestState> builder, IServiceProvider services)
    {
        ConfigureCalled = true;
    }

    public override Task OnStoreCreatedAsync(IStore<PluginTestState> store)
    {
        OnStoreCreatedCalled = true;
        return Task.CompletedTask;
    }

    public override Task OnBeforeUpdateAsync(PluginTestState currentState, string? action)
    {
        OnBeforeUpdateCalled = true;
        return Task.CompletedTask;
    }

    public override Task OnAfterUpdateAsync(PluginTestState previousState, PluginTestState newState, string? action)
    {
        OnAfterUpdateCalled = true;
        return Task.CompletedTask;
    }

    public override Task OnStoreDisposingAsync()
    {
        OnStoreDisposingCalled = true;
        return Task.CompletedTask;
    }
}

public class PluginWithDependency : StorePluginBase<PluginTestState>
{
    public override string Name => "PluginWithDependency";
    public override IReadOnlyList<string> Dependencies => new[] { "TestPlugin" };
}

public class PluginWithMiddleware : StorePluginBase<PluginTestState>
{
    public bool MiddlewareExecuted { get; private set; }

    public override string Name => "PluginWithMiddleware";

    public override IMiddleware<PluginTestState>? GetMiddleware()
    {
        return new TestMiddleware(this);
    }

    private class TestMiddleware : IMiddleware<PluginTestState>
    {
        private readonly PluginWithMiddleware _plugin;

        public TestMiddleware(PluginWithMiddleware plugin) => _plugin = plugin;

        public Task OnBeforeUpdateAsync(PluginTestState currentState, string? action)
        {
            _plugin.MiddlewareExecuted = true;
            return Task.CompletedTask;
        }

        public Task OnAfterUpdateAsync(PluginTestState previousState, PluginTestState currentState, string? action)
            => Task.CompletedTask;
    }
}

public class PluginTests
{
    private readonly IServiceProvider _serviceProvider;

    public PluginTests()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Plugin_ShouldHaveNameAndVersion()
    {
        // Arrange & Act
        var plugin = new TestPlugin();

        // Assert
        plugin.Name.Should().Be("TestPlugin");
        plugin.Version.Should().Be(new Version(1, 0, 0));
    }

    [Fact]
    public void PluginHost_Register_ShouldAddPlugin()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();

        // Act
        host.Register(plugin);

        // Assert
        host.Plugins.Should().HaveCount(1);
        host.Plugins[0].Should().Be(plugin);
    }

    [Fact]
    public void PluginHost_Register_ShouldPreventDuplicates()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        host.Register(new TestPlugin());

        // Act
        Action act = () => host.Register(new TestPlugin());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void PluginHost_GetPlugin_ByName_ShouldReturnPlugin()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();
        host.Register(plugin);

        // Act
        var result = host.GetPlugin("TestPlugin");

        // Assert
        result.Should().Be(plugin);
    }

    [Fact]
    public void PluginHost_GetPlugin_ByType_ShouldReturnPlugin()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();
        host.Register(plugin);

        // Act
        var result = host.GetPlugin<TestPlugin>();

        // Assert
        result.Should().Be(plugin);
    }

    [Fact]
    public void PluginHost_GetPlugin_ShouldReturnNullForMissing()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);

        // Act
        var result = host.GetPlugin("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void PluginHost_ConfigurePlugins_ShouldCallConfigure()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();
        host.Register(plugin);

        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));

        // Act
        host.ConfigurePlugins(builder);

        // Assert
        plugin.ConfigureCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PluginHost_InitializePlugins_ShouldCallOnStoreCreated()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();
        host.Register(plugin);

        var store = StoreBuilder<PluginTestState>.Create(new PluginTestState(0)).Build();

        // Act
        await host.InitializePluginsAsync(store);

        // Assert
        plugin.OnStoreCreatedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PluginHost_Dispose_ShouldCallOnStoreDisposing()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        var plugin = new TestPlugin();
        host.Register(plugin);

        // Act
        await host.DisposeAsync();

        // Assert
        plugin.OnStoreDisposingCalled.Should().BeTrue();
    }

    [Fact]
    public void PluginHost_ValidateDependencies_ShouldThrowForMissing()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        host.Register(new PluginWithDependency()); // Depends on TestPlugin

        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));

        // Act
        Action act = () => host.ConfigurePlugins(builder);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing dependency*TestPlugin*");
    }

    [Fact]
    public void PluginHost_ValidateDependencies_ShouldPassWithDependency()
    {
        // Arrange
        var host = new PluginHost<PluginTestState>(_serviceProvider);
        host.Register(new TestPlugin());
        host.Register(new PluginWithDependency());

        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));

        // Act
        Action act = () => host.ConfigurePlugins(builder);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StoreBuilder_WithPlugin_ShouldAddPlugin()
    {
        // Arrange
        var plugin = new TestPlugin();
        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));

        // Act
        builder.WithPlugin(plugin, _serviceProvider);

        // Assert
        plugin.ConfigureCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PluginWithMiddleware_ShouldAddMiddleware()
    {
        // Arrange
        var plugin = new PluginWithMiddleware();
        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));
        builder.WithPlugin(plugin, _serviceProvider);
        var store = builder.Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        plugin.MiddlewareExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task WithPlugin_UpdateHooks_ShouldFire()
    {
        // Arrange - WithPlugin must wrap the plugin in a PluginMiddleware so the
        // OnBeforeUpdateAsync/OnAfterUpdateAsync lifecycle hooks actually fire.
        var plugin = new TestPlugin();
        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));
        builder.WithPlugin(plugin, _serviceProvider);
        var store = builder.Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 });

        // Assert
        plugin.OnBeforeUpdateCalled.Should().BeTrue();
        plugin.OnAfterUpdateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task WithPlugin_AnalyticsPlugin_RecordsActions()
    {
        // Arrange
        var plugin = new EasyAppDev.Blazor.Store.Plugins.BuiltInPlugins.AnalyticsPlugin<PluginTestState>();
        var builder = StoreBuilder<PluginTestState>.Create(new PluginTestState(0));
        builder.WithPlugin(plugin, _serviceProvider);
        var store = builder.Build();

        // Act
        await store.UpdateAsync(s => s with { Count = 1 }, "INCREMENT");
        await store.UpdateAsync(s => s with { Count = 2 }, "INCREMENT");

        // Assert
        var metrics = plugin.GetMetrics();
        metrics.TotalActions.Should().Be(2);
        metrics.RecentActions.Should().HaveCount(2);
        metrics.RecentActions[0].Action.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task PluginMiddleware_AttachStore_FiresOnStoreCreated()
    {
        // Arrange
        var plugin = new TestPlugin();
        var middleware = new PluginMiddleware<PluginTestState>(plugin);
        var store = StoreBuilder<PluginTestState>.Create(new PluginTestState(0)).Build();

        // Act
        middleware.AttachStore(store);

        // Assert - OnStoreCreatedAsync is fired in the background; wait for it
        await WaitForAsync(() => plugin.OnStoreCreatedCalled);
        plugin.OnStoreCreatedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PluginMiddleware_AttachStore_IsIdempotent()
    {
        // Arrange
        var plugin = new CountingPlugin();
        var middleware = new PluginMiddleware<PluginTestState>(plugin);
        var store = StoreBuilder<PluginTestState>.Create(new PluginTestState(0)).Build();

        // Act
        middleware.AttachStore(store);
        middleware.AttachStore(store);
        middleware.AttachStore(store);

        await WaitForAsync(() => plugin.StoreCreatedCount > 0);
        await Task.Delay(100); // give any spurious extra invocations time to surface

        // Assert
        plugin.StoreCreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task PluginMiddleware_DisposeAsync_FiresOnStoreDisposing()
    {
        // Arrange
        var plugin = new TestPlugin();
        var middleware = new PluginMiddleware<PluginTestState>(plugin);

        // Act
        await middleware.DisposeAsync();

        // Assert
        plugin.OnStoreDisposingCalled.Should().BeTrue();
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private class CountingPlugin : StorePluginBase<PluginTestState>
    {
        private int _storeCreatedCount;

        public int StoreCreatedCount => Volatile.Read(ref _storeCreatedCount);

        public override string Name => "CountingPlugin";

        public override Task OnStoreCreatedAsync(IStore<PluginTestState> store)
        {
            Interlocked.Increment(ref _storeCreatedCount);
            return Task.CompletedTask;
        }
    }
}
