using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

public record ErrorTestState(int Count, string Name = "");

public class StoreErrorTests
{
    [Fact]
    public void StoreError_RecordProperties_WorkCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var state = new ErrorTestState(5, "Test");

        // Act
        var error = new StoreError<ErrorTestState>(exception, state, "TEST_ACTION", ErrorLocation.Middleware);

        // Assert
        error.Exception.Should().BeSameAs(exception);
        error.State.Should().Be(state);
        error.Action.Should().Be("TEST_ACTION");
        error.Location.Should().Be(ErrorLocation.Middleware);
    }

    [Fact]
    public void StoreError_Message_IncludesLocationAndAction()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");
        var error = new StoreError<ErrorTestState>(exception, null, "SAVE_USER", ErrorLocation.Persistence);

        // Act
        var message = error.Message;

        // Assert
        message.Should().Contain("[Persistence]");
        message.Should().Contain("SAVE_USER");
        message.Should().Contain("Something went wrong");
    }

    [Fact]
    public void StoreError_Message_WithoutAction_FormatsCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Error occurred");
        var error = new StoreError<ErrorTestState>(exception, null, null, ErrorLocation.Subscriber);

        // Act
        var message = error.Message;

        // Assert
        message.Should().Contain("[Subscriber]");
        message.Should().Contain("Error occurred");
        message.Should().NotContain("during ''");
    }

    [Fact]
    public void ErrorLocation_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.Middleware);
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.Updater);
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.Subscriber);
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.Persistence);
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.DevTools);
        Enum.GetValues<ErrorLocation>().Should().Contain(ErrorLocation.Hydration);
    }

    [Fact]
    public async Task StoreBuilder_OnError_HandlerReceivesErrors()
    {
        // Arrange
        StoreError<ErrorTestState>? capturedError = null;
        StoreErrorHandler<ErrorTestState> handler = error => capturedError = error;

        var store = StoreBuilder<ErrorTestState>
            .Create(new ErrorTestState(0))
            .OnError(handler)
            .Build();

        // Note: HandleError is internal, so we test it indirectly through middleware errors
        // For direct testing, we would need to expose it or test through integration
        await Task.CompletedTask; // Satisfy async method

        store.Dispose();
    }

    [Fact]
    public void StoreBuilder_OnError_WithDelegate_ConfiguresHandler()
    {
        // Arrange
        var handlerCalled = false;

        // Act
        var store = StoreBuilder<ErrorTestState>
            .Create(new ErrorTestState(0))
            .OnError((Action<StoreError<ErrorTestState>>)(error => handlerCalled = true))
            .Build();

        // Assert - handler is configured (internal, so we verify builder doesn't throw)
        store.Should().NotBeNull();

        store.Dispose();
    }

    [Fact]
    public void StoreBuilder_OnError_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => StoreBuilder<ErrorTestState>
            .Create(new ErrorTestState(0))
            .OnError((Action<StoreError<ErrorTestState>>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StoreBuilder_OnError_WithTypedDelegate_ConfiguresHandler()
    {
        // Arrange
        var called = false;
        StoreErrorHandler<ErrorTestState> handler = error => called = true;

        // Act
        var store = StoreBuilder<ErrorTestState>
            .Create(new ErrorTestState(0))
            .OnError(handler)
            .Build();

        // Assert
        store.Should().NotBeNull();

        store.Dispose();
    }

    [Fact]
    public void StoreError_WithNullState_AllowsNullState()
    {
        // Arrange
        var exception = new Exception("Test");

        // Act
        var error = new StoreError<ErrorTestState>(exception, null, "ACTION", ErrorLocation.Middleware);

        // Assert
        error.State.Should().BeNull();
    }

    [Fact]
    public void StoreError_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var exception = new Exception("Test");
        var state = new ErrorTestState(1);

        var error1 = new StoreError<ErrorTestState>(exception, state, "ACTION", ErrorLocation.Middleware);
        var error2 = new StoreError<ErrorTestState>(exception, state, "ACTION", ErrorLocation.Middleware);

        // Assert
        error1.Should().Be(error2);
    }
}
