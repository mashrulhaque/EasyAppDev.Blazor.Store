using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Middleware;

public record AwareState(int Value);

/// <summary>
/// Tests for store-aware middleware wiring and the middleware disposal pipeline.
/// </summary>
public class StoreAwareMiddlewareTests
{
    private sealed class TrackingStoreAwareMiddleware : IStoreAwareMiddleware<AwareState>
    {
        public IStore<AwareState>? AttachedStore { get; private set; }
        public int AttachCount { get; private set; }

        public void AttachStore(IStore<AwareState> store)
        {
            AttachedStore = store;
            AttachCount++;
        }

        public Task OnBeforeUpdateAsync(AwareState currentState, string? action) => Task.CompletedTask;

        public Task OnAfterUpdateAsync(AwareState previousState, AwareState currentState, string? action) =>
            Task.CompletedTask;
    }

    private sealed class DisposableMiddleware : IMiddleware<AwareState>, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;

        public Task OnBeforeUpdateAsync(AwareState currentState, string? action) => Task.CompletedTask;

        public Task OnAfterUpdateAsync(AwareState previousState, AwareState currentState, string? action) =>
            Task.CompletedTask;
    }

    private sealed class AsyncDisposableMiddleware : IMiddleware<AwareState>, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public Task OnBeforeUpdateAsync(AwareState currentState, string? action) => Task.CompletedTask;

        public Task OnAfterUpdateAsync(AwareState previousState, AwareState currentState, string? action) =>
            Task.CompletedTask;
    }

    [Fact]
    public void Build_AttachesStore_ToStoreAwareMiddlewares()
    {
        // Arrange
        var middleware = new TrackingStoreAwareMiddleware();

        // Act
        var store = StoreBuilder<AwareState>.Create(new AwareState(0))
            .WithMiddleware(middleware)
            .Build();

        // Assert
        middleware.AttachedStore.Should().BeSameAs(store);
        middleware.AttachCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_DisposesDisposableMiddlewares()
    {
        // Arrange
        var middleware = new DisposableMiddleware();
        var store = StoreBuilder<AwareState>.Create(new AwareState(0))
            .WithMiddleware(middleware)
            .Build();

        // Act
        store.Dispose();

        // Assert
        middleware.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncDisposableMiddlewares()
    {
        // Arrange
        var disposable = new DisposableMiddleware();
        var asyncDisposable = new AsyncDisposableMiddleware();
        var store = (Store<AwareState>)StoreBuilder<AwareState>.Create(new AwareState(0))
            .WithMiddleware(disposable)
            .WithMiddleware(asyncDisposable)
            .Build();

        // Act
        await store.DisposeAsync();

        // Assert
        disposable.Disposed.Should().BeTrue();
        asyncDisposable.Disposed.Should().BeTrue();
    }
}
