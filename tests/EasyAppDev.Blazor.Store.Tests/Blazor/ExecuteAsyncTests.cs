using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;

namespace EasyAppDev.Blazor.Store.Tests.Blazor;

public class ExecuteAsyncTests : TestContext
{
    [Fact]
    public async Task ExecuteAsync_WithSuccess_UpdatesStateCorrectly()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataSuccess());

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.Data.HasData.Should().BeTrue();
        component.Instance.State.Data.Data.Should().Be("success data");
    }

    [Fact]
    public async Task ExecuteAsync_WithError_UpdatesStateWithError()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataError());

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.Data.HasError.Should().BeTrue();
        component.Instance.State.Data.Error.Should().Be("Simulated error");
    }

    [Fact]
    public async Task ExecuteAsync_SetsLoadingStateDuringExecution()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act - Start async operation but don't await
        var task = component.InvokeAsync(async () =>
            await component.Instance.LoadDataSlow());

        // Check loading state immediately
        await Task.Delay(50);
        component.Instance.State.Data.IsLoading.Should().BeTrue();

        // Wait for completion
        await task;
        await Task.Delay(100);

        // Assert - Should transition to success
        component.Instance.State.Data.HasData.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutErrorHandler_DoesNotThrow()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act & Assert - Should not throw, should log instead
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataErrorNoHandler());

        // Wait for async operations
        await Task.Delay(100);

        // State should remain in loading (no error handler to transition it)
        component.Instance.State.Data.IsLoading.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_Works()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.DeleteData());

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SimplifiedOverload_DiscardsResult()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.ValidateData());

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        component.Instance.State.IsValidated.Should().BeTrue();
        // Result was discarded, but operation succeeded
    }

    [Fact]
    public async Task ExecuteAsync_TransitionsFromNotAskedToLoadingToSuccess()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Initial state should be NotAsked
        component.Instance.State.Data.IsNotAsked.Should().BeTrue();

        // Act
        var task = component.InvokeAsync(async () =>
            await component.Instance.LoadDataSlow());

        // Should transition to Loading
        await Task.Delay(50);
        component.Instance.State.Data.IsLoading.Should().BeTrue();

        // Wait for completion
        await task;
        await Task.Delay(100);

        // Should transition to Success
        component.Instance.State.Data.HasData.Should().BeTrue();
        component.Instance.State.Data.IsLoading.Should().BeFalse();
        component.Instance.State.Data.IsNotAsked.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_TransitionsFromNotAskedToLoadingToError()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Initial state should be NotAsked
        component.Instance.State.Data.IsNotAsked.Should().BeTrue();

        // Act
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataError());

        // Wait for async operations
        await Task.Delay(100);

        // Should transition to Error
        component.Instance.State.Data.HasError.Should().BeTrue();
        component.Instance.State.Data.IsLoading.Should().BeFalse();
        component.Instance.State.Data.IsNotAsked.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSequentialCalls_Work()
    {
        // Arrange
        Services.AddStore(TestState.Initial, (store, sp) => store);
        StoreTestHelpers.RegisterStoreUtilities<TestState>(Services);
        var component = RenderComponent<TestExecuteAsyncComponent>();

        // Act - First call succeeds
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataSuccess());
        await Task.Delay(100);

        component.Instance.State.Data.HasData.Should().BeTrue();

        // Act - Second call fails
        await component.InvokeAsync(async () =>
            await component.Instance.LoadDataError());
        await Task.Delay(100);

        // Assert - Should transition from success to error
        component.Instance.State.Data.HasError.Should().BeTrue();
        component.Instance.State.Data.HasData.Should().BeFalse();
    }

    // Test component
    private class TestExecuteAsyncComponent : StoreComponent<TestState>
    {
        // Expose State publicly for tests
        public new TestState State => base.State;

        public async Task LoadDataSuccess()
        {
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    return "success data";
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, result) => s with { Data = AsyncData<string>.Success(result) },
                error: (s, ex) => s with { Data = AsyncData<string>.Failure(ex.Message) }
            );
        }

        public async Task LoadDataError()
        {
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    throw new InvalidOperationException("Simulated error");
#pragma warning disable CS0162 // Unreachable code detected
                    return "never reached";
#pragma warning restore CS0162
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, result) => s with { Data = AsyncData<string>.Success(result) },
                error: (s, ex) => s with { Data = AsyncData<string>.Failure(ex.Message) }
            );
        }

        public async Task LoadDataSlow()
        {
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(200);  // Slower to check loading state
                    return "slow data";
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, result) => s with { Data = AsyncData<string>.Success(result) },
                error: (s, ex) => s with { Data = AsyncData<string>.Failure(ex.Message) }
            );
        }

        public async Task LoadDataErrorNoHandler()
        {
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    throw new Exception("Error without handler");
#pragma warning disable CS0162 // Unreachable code detected
                    return "never reached";
#pragma warning restore CS0162
                },
                loading: s => s with { Data = s.Data.ToLoading() },
                success: (s, result) => s with { Data = AsyncData<string>.Success(result) }
                // No error handler provided
            );
        }

        public async Task DeleteData()
        {
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    // Void operation
                },
                loading: s => s with { IsDeleting = true },
                success: s => s with { IsDeleting = false, IsDeleted = true },
                error: (s, ex) => s with { IsDeleting = false, DeleteError = ex.Message }
            );
        }

        public async Task ValidateData()
        {
            // Simplified overload - result is discarded
            await ExecuteAsync(
                async () =>
                {
                    await Task.Delay(50);
                    return "validation result";  // This will be discarded
                },
                loading: s => s with { IsValidating = true },
                success: s => s with { IsValidating = false, IsValidated = true }
                // No error handler provided
            );
        }
    }

    private record TestState(
        AsyncData<string> Data,
        bool IsDeleting = false,
        bool IsDeleted = false,
        string? DeleteError = null,
        bool IsValidating = false,
        bool IsValidated = false)
    {
        public static TestState Initial => new(AsyncData<string>.NotAsked());
    }
}
