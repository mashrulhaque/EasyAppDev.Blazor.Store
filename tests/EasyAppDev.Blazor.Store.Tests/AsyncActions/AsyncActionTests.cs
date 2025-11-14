using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;

namespace EasyAppDev.Blazor.Store.Tests.AsyncActions;

public record TestStateWithAsync(int Counter, AsyncActionState<string> DataAction);

public class AsyncActionTests
{
    [Fact]
    public async Task ExecuteAsync_SetsLoadingState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        var tcs = new TaskCompletionSource<string>();

        // Act
        var executeTask = asyncAction.ExecuteAsync(() => tcs.Task);

        // Allow loading state to be set
        await Task.Delay(10);

        // Assert
        store.GetState().DataAction.IsLoading.Should().BeTrue();

        // Complete the action
        tcs.SetResult("Success");
        await executeTask;
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_SetsSuccessState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        var result = await asyncAction.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return "Success Data";
        });

        // Assert
        result.Should().Be("Success Data");
        var state = store.GetState().DataAction;
        state.IsSuccess.Should().BeTrue();
        state.Data.Should().Be("Success Data");
        state.Error.Should().BeNull();
        state.IsLoading.Should().BeFalse();
        state.LastUpdated.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_SetsErrorState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await asyncAction.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test error");
#pragma warning disable CS0162 // Unreachable code detected
                return ""; // Never reached but needed for type inference
#pragma warning restore CS0162
            });
        });

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<InvalidOperationException>();
        var state = store.GetState().DataAction;
        state.IsError.Should().BeTrue();
        state.Error.Should().Contain("Test error");
        state.IsLoading.Should().BeFalse();
        state.Data.Should().BeNull();
        state.LastUpdated.Should().NotBeNull();
    }

    [Fact]
    public async Task ResetAsync_ResetsToIdleState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Success("Data")));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        await asyncAction.ResetAsync();

        // Assert
        var state = store.GetState().DataAction;
        state.IsIdle.Should().BeTrue();
        state.IsLoading.Should().BeFalse();
        state.Data.Should().BeNull();
        state.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithParameter_ExecutesSuccessfully()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        var result = await asyncAction.ExecuteAsync(
            async (int value) =>
            {
                await Task.Delay(10);
                return $"Value: {value}";
            },
            42);

        // Assert
        result.Should().Be("Value: 42");
        store.GetState().DataAction.IsSuccess.Should().BeTrue();
        store.GetState().DataAction.Data.Should().Be("Value: 42");
    }

    [Fact]
    public void GetState_ReturnsCurrentActionState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Loading()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        var actionState = asyncAction.GetState();

        // Assert
        actionState.IsLoading.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithActionName_PassesActionNameToStore()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        await asyncAction.ExecuteAsync(
            async () =>
            {
                await Task.Delay(10);
                return "Data";
            },
            actionName: "LOAD_DATA");

        // Assert - action completed successfully
        store.GetState().DataAction.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AsyncAction<TestStateWithAsync, string>(
            null!,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public void Constructor_WithNullStateSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        // Act
        var act = () => new AsyncAction<TestStateWithAsync, string>(
            store,
            null!,
            (state, actionState) => state with { DataAction = actionState });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stateSelector");
    }

    [Fact]
    public void Constructor_WithNullStateUpdater_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        // Act
        var act = () => new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stateUpdater");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(
            new TestStateWithAsync(0, AsyncActionState<string>.Idle()));

        var asyncAction = new AsyncAction<TestStateWithAsync, string>(
            store,
            state => state.DataAction,
            (state, actionState) => state with { DataAction = actionState });

        // Act
        var act = async () => await asyncAction.ExecuteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class AsyncActionStateTests
{
    [Fact]
    public void Idle_CreatesIdleState()
    {
        // Act
        var state = AsyncActionState<string>.Idle();

        // Assert
        state.IsIdle.Should().BeTrue();
        state.IsLoading.Should().BeFalse();
        state.IsSuccess.Should().BeFalse();
        state.IsError.Should().BeFalse();
        state.Data.Should().BeNull();
        state.Error.Should().BeNull();
        state.LastUpdated.Should().BeNull();
    }

    [Fact]
    public void Loading_CreatesLoadingState()
    {
        // Act
        var state = AsyncActionState<string>.Loading();

        // Assert
        state.IsLoading.Should().BeTrue();
        state.IsIdle.Should().BeFalse();
        state.IsSuccess.Should().BeFalse();
        state.IsError.Should().BeFalse();
        state.Data.Should().BeNull();
        state.Error.Should().BeNull();
    }

    [Fact]
    public void Success_CreatesSuccessState()
    {
        // Act
        var state = AsyncActionState<string>.Success("Test Data");

        // Assert
        state.IsSuccess.Should().BeTrue();
        state.IsLoading.Should().BeFalse();
        state.IsIdle.Should().BeFalse();
        state.IsError.Should().BeFalse();
        state.Data.Should().Be("Test Data");
        state.Error.Should().BeNull();
        state.LastUpdated.Should().NotBeNull();
        state.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Failed_CreatesErrorState()
    {
        // Act
        var state = AsyncActionState<string>.Failed("Test error");

        // Assert
        state.IsError.Should().BeTrue();
        state.IsLoading.Should().BeFalse();
        state.IsIdle.Should().BeFalse();
        state.IsSuccess.Should().BeFalse();
        state.Data.Should().BeNull();
        state.Error.Should().Be("Test error");
        state.LastUpdated.Should().NotBeNull();
        state.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
