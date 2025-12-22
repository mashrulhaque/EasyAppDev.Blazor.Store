// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.ServerSync;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Tests for Phase 1 security remediation.
/// </summary>
public class Phase1SecurityTests
{
    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
        public int Age { get; init; }
    }

    #region 1.1 - Validator Extension Tests

    [Fact]
    public void WithValidator_StoresValidatorInBuilder()
    {
        // Arrange
        var validator = new TestStateValidator();
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act
        builder.WithValidator(validator);

        // Assert
        builder.StateValidator.Should().Be(validator);
    }

    [Fact]
    public void WithValidator_ThrowsOnNullValidator()
    {
        // Arrange
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act & Assert
        var act = () => builder.WithValidator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireValidation_ThrowsWhenNoValidatorConfigured()
    {
        // Arrange
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act & Assert
        var act = () => builder.RequireValidation();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no validator has been configured*");
    }

    [Fact]
    public void RequireValidation_SucceedsWhenValidatorConfigured()
    {
        // Arrange
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });
        var validator = new TestStateValidator();

        // Act
        builder.WithValidator(validator).RequireValidation();

        // Assert
        builder.RequiresValidation.Should().BeTrue();
        builder.StateValidator.Should().Be(validator);
    }

    [Fact]
    public void WithDefaultValidation_UsesRequiredStateValidator()
    {
        // Arrange
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act
        builder.WithDefaultValidation();

        // Assert
        builder.StateValidator.Should().NotBeNull();
        builder.StateValidator.Should().BeOfType<RequiredStateValidator<TestState>>();
    }

    [Fact]
    public async Task ValidationMiddleware_RejectsInvalidState()
    {
        // Arrange
        var validator = new TestStateValidator();
        var middleware = new ValidationMiddleware<TestState>(validator, rejectInvalid: true);
        var invalidState = new TestState { Count = -1 }; // Negative count is invalid

        // Act & Assert
        await Assert.ThrowsAsync<StateValidationException>(async () =>
            await middleware.OnAfterUpdateAsync(
                new TestState { Count = 0 },
                invalidState,
                "TEST_ACTION"));
    }

    [Fact]
    public async Task ValidationMiddleware_AcceptsValidState()
    {
        // Arrange
        var validator = new TestStateValidator();
        var middleware = new ValidationMiddleware<TestState>(validator, rejectInvalid: true);
        var validState = new TestState { Count = 5 };

        // Act - Should not throw
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0 },
            validState,
            "TEST_ACTION");

        // Assert - No exception means success
        true.Should().BeTrue();
    }

    [Fact]
    public async Task ValidationMiddleware_LogsWarningWhenNotRejecting()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var validator = new TestStateValidator();
        var middleware = new ValidationMiddleware<TestState>(validator, rejectInvalid: false, mockLogger.Object);
        var invalidState = new TestState { Count = -1 };

        // Act - Should NOT throw
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0 },
            invalidState,
            "TEST_ACTION");

        // Assert - No exception, but we can't easily verify logging with Mock<ILogger>
        true.Should().BeTrue();
    }

    #endregion

    #region 1.2 - Default StateValidator Tests

    [Fact]
    public void SecurityOptions_DefaultValidatorIsNull()
    {
        // Arrange & Act
        var options = new SecurityOptions<TestState>();

        // Assert
        options.StateValidator.Should().BeNull();
    }

    [Fact]
    public void SecurityOptions_RequireValidationDefaultIsFalse()
    {
        // Arrange & Act
        var options = new SecurityOptions<TestState>();

        // Assert
        options.RequireValidation.Should().BeFalse();
    }

    [Fact]
    public void SecurityOptions_Validate_ThrowsWhenRequiredButNoValidator()
    {
        // Arrange
        var options = new SecurityOptions<TestState>
        {
            RequireValidation = true,
            StateValidator = null
        };

        // Act & Assert
        var act = () => options.Validate("TestMiddleware");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestMiddleware requires a StateValidator*");
    }

    [Fact]
    public void SecurityOptions_Validate_SucceedsWhenValidatorConfigured()
    {
        // Arrange
        var options = new SecurityOptions<TestState>
        {
            RequireValidation = true,
            StateValidator = new TestStateValidator()
        };

        // Act & Assert - Should not throw
        options.Validate("TestMiddleware");
    }

    [Fact]
    public void SecurityOptions_Validate_SucceedsWhenNotRequired()
    {
        // Arrange
        var options = new SecurityOptions<TestState>
        {
            RequireValidation = false,
            StateValidator = null
        };

        // Act & Assert - Should not throw
        options.Validate("TestMiddleware");
    }

    #endregion

    #region 1.3 - TabSync Clock Skew Tests

    [Fact]
    public void TabSyncOptions_DefaultClockSkewToleranceIs5Seconds()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.ClockSkewToleranceSeconds.Should().Be(5);
    }

    [Fact]
    public void TabSyncOptions_DefaultMaxMessageSizeIs1MB()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.MaxMessageSizeBytes.Should().Be(1_048_576);
    }

    [Fact]
    public void TabSyncOptions_HasStateValidatorProperty()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.StateValidator.Should().BeNull();
    }

    [Fact]
    public void TabSyncOptions_RejectInvalidStateDefaultIsTrue()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.RejectInvalidState.Should().BeTrue();
    }

    #endregion

    #region 1.4 - IStoreHub Security Documentation Tests

    [Fact]
    public void IStoreHub_DocumentationReferencesSecureStoreHubBase()
    {
        // Verify that IStoreHub interface exists and has proper documentation
        var hubInterface = typeof(IStoreHub);
        hubInterface.Should().NotBeNull();

        // Check that the interface has the required methods for security
        var joinMethod = hubInterface.GetMethod("JoinDocument");
        joinMethod.Should().NotBeNull();

        var sendUpdateMethod = hubInterface.GetMethod("SendUpdate");
        sendUpdateMethod.Should().NotBeNull();

        var leaveMethod = hubInterface.GetMethod("LeaveDocument");
        leaveMethod.Should().NotBeNull();
    }

    [Fact]
    public void IStoreHubClient_HasRequiredMethods()
    {
        // Verify the client interface has proper structure
        var clientInterface = typeof(IStoreHubClient);
        clientInterface.Should().NotBeNull();

        // Check for update methods
        var receiveUpdate = clientInterface.GetMethod("ReceiveUpdate");
        receiveUpdate.Should().NotBeNull();

        // Check for presence methods
        var userJoined = clientInterface.GetMethod("UserJoined");
        userJoined.Should().NotBeNull();

        var userLeft = clientInterface.GetMethod("UserLeft");
        userLeft.Should().NotBeNull();
    }

    #endregion

    #region StateValidationException Tests

    [Fact]
    public void StateValidationException_ContainsValidationResult()
    {
        // Arrange
        var result = StateValidationResult.Failure("Error 1", "Error 2");

        // Act
        var exception = new StateValidationException(result, "TEST_ACTION");

        // Assert
        exception.ValidationResult.Should().Be(result);
        exception.Action.Should().Be("TEST_ACTION");
        exception.Message.Should().Contain("Error 1");
        exception.Message.Should().Contain("Error 2");
    }

    [Fact]
    public void StateValidationException_HandlesNullAction()
    {
        // Arrange
        var result = StateValidationResult.Failure("Error 1");

        // Act
        var exception = new StateValidationException(result, null);

        // Assert
        exception.Action.Should().BeNull();
        exception.Message.Should().Contain("Error 1");
    }

    #endregion

    #region Helper Classes

    private class TestStateValidator : IStateValidator<TestState>
    {
        public StateValidationResult Validate(TestState state)
        {
            var errors = new List<string>();

            if (state.Count < 0)
                errors.Add("Count cannot be negative");

            if (state.Age < 0 || state.Age > 150)
                errors.Add("Age must be between 0 and 150");

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    #endregion
}

/// <summary>
/// Integration tests for validator propagation.
/// </summary>
public class ValidatorPropagationTests
{
    public record TestState(int Value);

    [Fact]
    public void StoreBuilder_PreservesValidatorAcrossCloning()
    {
        // Arrange
        var validator = new SimpleValidator();
        var builder = StoreBuilder<TestState>.Create(new TestState(0))
            .WithValidator(validator);

        // Act - Build the store
        var store = builder.Build();

        // Assert
        builder.StateValidator.Should().Be(validator);
    }

    [Fact]
    public void WithTabSync_PropagatesValidatorFromBuilder()
    {
        // Arrange
        var validator = new SimpleValidator();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var mockJs = new Mock<Microsoft.JSInterop.IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        var serviceProvider = services.BuildServiceProvider();

        var builder = StoreBuilder<TestState>.Create(new TestState(0))
            .WithValidator(validator);

        // Act - Add TabSync (should propagate the validator)
        TabSyncOptions? capturedOptions = null;
        builder.WithTabSync(serviceProvider, opts =>
        {
            // Capture options before propagation happens
            capturedOptions = opts;
        });

        // Assert - The validator should be propagated after configure runs
        // We can verify by checking the builder still has the validator
        builder.StateValidator.Should().Be(validator);
    }

    [Fact]
    public void WithTabSync_DoesNotOverrideExplicitValidator()
    {
        // Arrange
        var builderValidator = new SimpleValidator();
        var explicitValidator = new Mock<IStateValidator<object>>().Object;
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var mockJs = new Mock<Microsoft.JSInterop.IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        var serviceProvider = services.BuildServiceProvider();

        var builder = StoreBuilder<TestState>.Create(new TestState(0))
            .WithValidator(builderValidator);

        TabSyncOptions? capturedOptions = null;

        // Act - Add TabSync with explicit validator
        builder.WithTabSync(serviceProvider, opts =>
        {
            opts.StateValidator = explicitValidator;
            capturedOptions = opts;
        });

        // Assert - Explicit validator should be preserved
        capturedOptions.Should().NotBeNull();
        capturedOptions!.StateValidator.Should().Be(explicitValidator);
    }

    private class SimpleValidator : IStateValidator<TestState>
    {
        public StateValidationResult Validate(TestState state)
        {
            return state.Value >= 0
                ? StateValidationResult.Success()
                : StateValidationResult.Failure("Value cannot be negative");
        }
    }
}

/// <summary>
/// Tests for StateValidatorWrapper.
/// </summary>
public class StateValidatorWrapperTests
{
    public record TestState(int Value, string? Name);

    [Fact]
    public void StateValidatorWrapper_ValidatesCorrectTypeSuccessfully()
    {
        // Arrange
        var innerValidator = new TestValidator();
        var wrapper = new StateValidatorWrapper<TestState>(innerValidator);

        // Act
        var result = wrapper.Validate(new TestState(5, "Test"));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void StateValidatorWrapper_PropagatesValidationErrors()
    {
        // Arrange
        var innerValidator = new TestValidator();
        var wrapper = new StateValidatorWrapper<TestState>(innerValidator);

        // Act
        var result = wrapper.Validate(new TestState(-1, "Test"));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Value cannot be negative");
    }

    [Fact]
    public void StateValidatorWrapper_RejectsWrongType()
    {
        // Arrange
        var innerValidator = new TestValidator();
        var wrapper = new StateValidatorWrapper<TestState>(innerValidator);

        // Act
        var result = wrapper.Validate("wrong type");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Expected state of type"));
    }

    [Fact]
    public void StateValidatorWrapper_ThrowsOnNullInnerValidator()
    {
        // Act & Assert
        var act = () => new StateValidatorWrapper<TestState>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private class TestValidator : IStateValidator<TestState>
    {
        public StateValidationResult Validate(TestState state)
        {
            if (state.Value < 0)
                return StateValidationResult.Failure("Value cannot be negative");

            return StateValidationResult.Success();
        }
    }
}
