// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.TabSync;

/// <summary>
/// Security-focused tests for TabSync middleware.
/// </summary>
public class TabSyncSecurityTests
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

    #region Clock Skew Tests

    [Fact]
    public void TabSyncOptions_ClockSkewToleranceDefaults()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.ClockSkewToleranceSeconds.Should().Be(5);
        options.MaxMessageAgeSeconds.Should().Be(30);
        options.ValidateTimestamp.Should().BeTrue();
    }

    [Fact]
    public void TabSyncOptions_AllowsCustomClockSkewTolerance()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            ClockSkewToleranceSeconds = 10,
            MaxMessageAgeSeconds = 60
        };

        // Assert
        options.ClockSkewToleranceSeconds.Should().Be(10);
        options.MaxMessageAgeSeconds.Should().Be(60);
    }

    [Fact]
    public void TabSyncOptions_ValidationCanBeDisabled()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            ValidateTimestamp = false
        };

        // Assert
        options.ValidateTimestamp.Should().BeFalse();
    }

    #endregion

    #region Message Size Tests

    [Fact]
    public void TabSyncOptions_MaxMessageSizeDefaults()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.MaxMessageSizeBytes.Should().Be(1_048_576); // 1MB
    }

    [Fact]
    public void TabSyncOptions_AllowsCustomMessageSizeLimit()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            MaxMessageSizeBytes = 512 * 1024 // 512KB
        };

        // Assert
        options.MaxMessageSizeBytes.Should().Be(512 * 1024);
    }

    [Fact]
    public void TabSyncOptions_ZeroMessageSizeDisablesCheck()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            MaxMessageSizeBytes = 0
        };

        // Assert
        options.MaxMessageSizeBytes.Should().Be(0);
    }

    #endregion

    #region State Validation Tests

    [Fact]
    public void TabSyncOptions_StateValidatorDefaults()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.StateValidator.Should().BeNull();
        options.RejectInvalidState.Should().BeTrue();
    }

    [Fact]
    public void TabSyncOptions_AllowsCustomValidator()
    {
        // Arrange
        var validator = new TestStateValidator();
        var options = new TabSyncOptions
        {
            StateValidator = validator,
            RejectInvalidState = true
        };

        // Assert
        options.StateValidator.Should().Be(validator);
        options.RejectInvalidState.Should().BeTrue();
    }

    [Fact]
    public void TabSyncOptions_AllowsValidationWithoutRejection()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            StateValidator = new TestStateValidator(),
            RejectInvalidState = false
        };

        // Assert
        options.RejectInvalidState.Should().BeFalse();
    }

    #endregion

    #region Signature Verification Tests

    [Fact]
    public void TabSyncOptions_SigningDefaults()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.EnableMessageSigning.Should().BeFalse();
        options.RequireValidSignature.Should().BeTrue();
        options.SigningKey.Should().BeNull();
        options.DeriveKeyFromOrigin.Should().BeFalse();
    }

    [Fact]
    public void TabSyncSecurityExtensions_WithSharedSigningKey_ConfiguresOptions()
    {
        // Arrange
        var key = new byte[32];
        new Random(42).NextBytes(key);
        var options = new TabSyncOptions();

        // Act
        options.WithSharedSigningKey(key);

        // Assert
        options.EnableMessageSigning.Should().BeTrue();
        options.SigningKey.Should().BeEquivalentTo(key);
    }

    [Fact]
    public void TabSyncSecurityExtensions_WithSharedSigningKey_RejectsShortKey()
    {
        // Arrange
        var shortKey = new byte[16]; // Less than 32 bytes
        var options = new TabSyncOptions();

        // Act & Assert
        var act = () => options.WithSharedSigningKey(shortKey);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void TabSyncSecurityExtensions_WithOriginDerivedKey_ConfiguresOptions()
    {
        // Arrange
        var options = new TabSyncOptions();

        // Act
        options.WithOriginDerivedKey();

        // Assert
        options.EnableMessageSigning.Should().BeTrue();
        options.DeriveKeyFromOrigin.Should().BeTrue();
    }

    [Fact]
    public void TabSyncSecurityExtensions_WithDerivedSigningKey_CreatesKey()
    {
        // Arrange
        var options = new TabSyncOptions();

        // Act
        options.WithDerivedSigningKey("test-seed");

        // Assert
        options.EnableMessageSigning.Should().BeTrue();
        options.SigningKey.Should().NotBeNull();
        options.SigningKey.Should().HaveCount(32);
    }

    [Fact]
    public void TabSyncSecurityExtensions_WithoutMessageSigning_DisablesSigning()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            EnableMessageSigning = true,
            SigningKey = new byte[32],
            DeriveKeyFromOrigin = true
        };

        // Act
        options.WithoutMessageSigning();

        // Assert
        options.EnableMessageSigning.Should().BeFalse();
        options.SigningKey.Should().BeNull();
        options.DeriveKeyFromOrigin.Should().BeFalse();
    }

    [Fact]
    public void TabSyncSecurityExtensions_DerivedKeysAreConsistent()
    {
        // Arrange
        var options1 = new TabSyncOptions();
        var options2 = new TabSyncOptions();
        const string seed = "consistent-seed";

        // Act
        options1.WithDerivedSigningKey(seed);
        options2.WithDerivedSigningKey(seed);

        // Assert
        options1.SigningKey.Should().BeEquivalentTo(options2.SigningKey);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void TabSyncMiddleware_CanBeCreatedWithSecurityOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockJs = new Mock<IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        var serviceProvider = services.BuildServiceProvider();

        var options = new TabSyncOptions
        {
            ChannelName = "test-channel",
            EnableMessageSigning = true,
            DeriveKeyFromOrigin = true,
            MaxMessageSizeBytes = 512 * 1024,
            ClockSkewToleranceSeconds = 10,
            MaxMessageAgeSeconds = 60,
            ValidateTimestamp = true,
            StateValidator = new TestStateValidator(),
            RejectInvalidState = true
        };

        // Act
        var middleware = new TabSyncMiddleware<TestState>(serviceProvider, options);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public void StoreBuilder_WithTabSync_AcceptsSecurityConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockJs = new Mock<IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        var serviceProvider = services.BuildServiceProvider();

        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act & Assert - Should not throw
        builder.WithTabSync(serviceProvider, opts => opts
            .Channel("secure-channel")
            .WithOriginDerivedKey());
    }

    #endregion

    #region Callback Tests

    [Fact]
    public void TabSyncOptions_OnValidationFailed_CanBeConfigured()
    {
        // Arrange
        StateValidationResult? capturedResult = null;
        var options = new TabSyncOptions
        {
            OnValidationFailed = result => capturedResult = result
        };

        // Act
        options.OnValidationFailed?.Invoke(StateValidationResult.Failure("Test error"));

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.IsValid.Should().BeFalse();
        capturedResult.Errors.Should().Contain("Test error");
    }

    [Fact]
    public void TabSyncOptions_OnInvalidSignature_CanBeConfigured()
    {
        // Arrange
        string? capturedReason = null;
        var options = new TabSyncOptions
        {
            OnInvalidSignature = reason => capturedReason = reason
        };

        // Act
        options.OnInvalidSignature?.Invoke("Signature mismatch");

        // Assert
        capturedReason.Should().Be("Signature mismatch");
    }

    [Fact]
    public void TabSyncOptions_OnSyncError_CanBeConfigured()
    {
        // Arrange
        Exception? capturedException = null;
        var options = new TabSyncOptions
        {
            OnSyncError = ex => capturedException = ex
        };

        // Act
        options.OnSyncError?.Invoke(new InvalidOperationException("Test error"));

        // Assert
        capturedException.Should().NotBeNull();
        capturedException.Should().BeOfType<InvalidOperationException>();
        capturedException!.Message.Should().Be("Test error");
    }

    #endregion

    #region Helper Classes

    private class TestStateValidator : IStateValidator<object>
    {
        public StateValidationResult Validate(object state)
        {
            if (state is TestState testState)
            {
                if (testState.Count < 0)
                    return StateValidationResult.Failure("Count cannot be negative");
            }

            return StateValidationResult.Success();
        }
    }

    #endregion
}
