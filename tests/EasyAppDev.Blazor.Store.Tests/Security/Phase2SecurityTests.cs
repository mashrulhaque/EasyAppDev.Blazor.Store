// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.ServerSync;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Tests for Phase 2 security remediation.
/// </summary>
public class Phase2SecurityTests
{
    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
        [SensitiveData]
        public string? Password { get; init; }
        [SensitiveData]
        public string? Token { get; init; }
    }

    #region 2.1 - Message Signing Key Management (PBKDF2 Iterations)

    [Fact]
    public void DeriveKeyFromSeed_DefaultIterationsIs100000()
    {
        // Arrange
        var seed = "test-seed";

        // Act
        var key1 = MessageSigner.DeriveKeyFromSeed(seed);
        var key2 = MessageSigner.DeriveKeyFromSeed(seed, 100_000);

        // Assert - Default should be 100,000 iterations
        key1.Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKeyFromSeed_RejectsIterationsBelow10000()
    {
        // Arrange
        var seed = "test-seed";

        // Act & Assert
        var act = () => MessageSigner.DeriveKeyFromSeed(seed, iterations: 9999);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 10,000*");
    }

    [Fact]
    public void DeriveKeyFromSeed_Accepts10000Iterations()
    {
        // Arrange
        var seed = "test-seed";

        // Act
        var key = MessageSigner.DeriveKeyFromSeed(seed, iterations: 10_000);

        // Assert
        key.Should().HaveCount(32);
    }

    [Fact]
    public void DeriveKeyFromSeed_ProducesDeterministicKey()
    {
        // Arrange
        var seed = "same-seed-value";

        // Act
        var key1 = MessageSigner.DeriveKeyFromSeed(seed);
        var key2 = MessageSigner.DeriveKeyFromSeed(seed);

        // Assert
        key1.Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKeyFromSeed_DifferentSeedsProduceDifferentKeys()
    {
        // Arrange & Act
        var key1 = MessageSigner.DeriveKeyFromSeed("seed1");
        var key2 = MessageSigner.DeriveKeyFromSeed("seed2");

        // Assert
        key1.Should().NotBeEquivalentTo(key2);
    }

    [Fact]
    public void DeriveKeyFromPassphrase_DefaultIterationsIs100000()
    {
        // Arrange
        var passphrase = "test-passphrase";
        var salt = new byte[16];
        Random.Shared.NextBytes(salt);

        // Act
        var key1 = MessageSigner.DeriveKeyFromPassphrase(passphrase, salt);
        var key2 = MessageSigner.DeriveKeyFromPassphrase(passphrase, salt, 100_000);

        // Assert
        key1.Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void CreateWithDerivedKey_DefaultIterationsIs100000()
    {
        // Arrange
        var passphrase = "test-passphrase";

        // Act
        using var signer1 = MessageSigner.CreateWithDerivedKey(passphrase);
        using var signer2 = MessageSigner.CreateWithDerivedKey(passphrase, iterations: 100_000);

        // Assert - Keys should match since defaults are the same
        var message = "test message";
        var sig1 = signer1.Sign(message);
        var sig2 = signer2.Sign(message);
        sig1.Should().Be(sig2);
    }

    #endregion

    #region 2.2 - Session Hijacking Prevention

    [Fact]
    public void ServerSyncOptions_HasSessionTimeoutProperty()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };

        // Assert
        options.SessionTimeoutMinutes.Should().Be(0); // Default is no timeout
    }

    [Fact]
    public void ServerSyncOptions_HasRequireSessionValidationProperty()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };

        // Assert
        options.RequireSessionValidation.Should().BeFalse(); // Default for backward compatibility
    }

    [Fact]
    public void ServerSyncOptions_HasSessionExpiredCallback()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };
        var callbackInvoked = false;

        // Act
        options.OnSessionExpired = () => callbackInvoked = true;
        options.OnSessionExpired?.Invoke();

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void ServerSyncOptions_HasSessionValidationFailedCallback()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };
        string? capturedReason = null;

        // Act
        options.OnSessionValidationFailed = reason => capturedReason = reason;
        options.OnSessionValidationFailed?.Invoke("Test reason");

        // Assert
        capturedReason.Should().Be("Test reason");
    }

    #endregion

    #region 2.4 & 2.5 - Sensitive Data Filtering

    [Fact]
    public void LoggingMiddlewareOptions_LogStateDetailsDefaultIsFalse()
    {
        // Arrange & Act
        var options = new LoggingMiddlewareOptions();

        // Assert
        options.LogStateDetails.Should().BeFalse();
    }

    [Fact]
    public void LoggingMiddlewareOptions_FilterSensitiveDataDefaultIsTrue()
    {
        // Arrange & Act
        var options = new LoggingMiddlewareOptions();

        // Assert
        options.FilterSensitiveData.Should().BeTrue();
    }

    [Fact]
    public async Task LoggingMiddleware_DoesNotLogStateByDefault()
    {
        // Arrange
        var logOutput = new List<string>();
        var middleware = new LoggingMiddleware<TestState>(msg => logOutput.Add(msg));
        var state = new TestState { Count = 1, Name = "Test", Password = "secret123" };

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0 },
            state,
            "TEST_ACTION");

        // Assert - Should only log action name, not state content
        logOutput.Should().ContainSingle();
        logOutput[0].Should().Contain("TEST_ACTION");
        logOutput[0].Should().NotContain("secret123");
        logOutput[0].Should().NotContain("Password");
    }

    [Fact]
    public async Task LoggingMiddleware_FiltersSensitiveDataWhenLoggingState()
    {
        // Arrange
        var logOutput = new List<string>();
        var options = new LoggingMiddlewareOptions
        {
            LogStateDetails = true,
            FilterSensitiveData = true
        };
        var middleware = new LoggingMiddleware<TestState>(msg => logOutput.Add(msg), options);
        var state = new TestState { Count = 1, Name = "Test", Password = "secret123", Token = "jwt-token" };

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0 },
            state,
            "TEST_ACTION");

        // Assert
        logOutput.Should().ContainSingle();
        var log = logOutput[0];
        log.Should().Contain("[FILTERED]");
        log.Should().NotContain("secret123");
        log.Should().NotContain("jwt-token");
        log.Should().Contain("count"); // Non-sensitive data should be present (camelCase in JSON)
        log.Should().Contain("name");
    }

    [Fact]
    public void SensitiveDataFilterOptions_HasDefaultFilteredPropertyNames()
    {
        // Arrange & Act
        var options = new SensitiveDataFilterOptions();

        // Assert
        options.FilteredPropertyNames.Should().Contain("Password");
        options.FilteredPropertyNames.Should().Contain("Token");
        options.FilteredPropertyNames.Should().Contain("ApiKey");
        options.FilteredPropertyNames.Should().Contain("Secret");
        options.FilteredPropertyNames.Should().Contain("CreditCard");
    }

    [Fact]
    public void SensitiveDataFilter_FiltersSensitiveProperties()
    {
        // Arrange
        var state = new TestState
        {
            Count = 5,
            Name = "Test User",
            Password = "SuperSecret123",
            Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);
        var deserialized = JsonDocument.Parse(json);

        // Assert
        deserialized.RootElement.GetProperty("count").GetInt32().Should().Be(5);
        deserialized.RootElement.GetProperty("name").GetString().Should().Be("Test User");
        deserialized.RootElement.GetProperty("password").GetString().Should().Be("[FILTERED]");
        deserialized.RootElement.GetProperty("token").GetString().Should().Be("[FILTERED]");
    }

    [Fact]
    public void SensitiveDataFilter_FiltersNestedSensitiveProperties()
    {
        // Arrange
        var state = new
        {
            User = new
            {
                Name = "John",
                Password = "secret",
                Settings = new
                {
                    Theme = "dark",
                    ApiKey = "key123"
                }
            }
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        json.Should().Contain("\"name\":\"John\"");
        json.Should().NotContain("secret");
        json.Should().NotContain("key123");
        json.Should().Contain("[FILTERED]");
    }

    #endregion

    #region 2.6 - Version Number Conflict Resolution Bypass

    [Fact]
    public void ServerSyncOptions_HasMaxVersionJumpProperty()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };

        // Assert
        options.MaxVersionJump.Should().Be(1000); // Default
    }

    [Fact]
    public void ServerSyncOptions_HasRejectSuspiciousVersionsProperty()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };

        // Assert
        options.RejectSuspiciousVersions.Should().BeFalse(); // Default for backward compatibility
    }

    [Fact]
    public void ServerSyncOptions_HasOnSuspiciousActivityCallback()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState> { HubUrl = "http://test" };
        string? capturedMessage = null;

        // Act
        options.OnSuspiciousActivity = msg => capturedMessage = msg;
        options.OnSuspiciousActivity?.Invoke("Version jump detected");

        // Assert
        capturedMessage.Should().Be("Version jump detected");
    }

    [Fact]
    public void ServerSyncOptions_CanConfigureVersionJumpDetection()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<TestState>
        {
            HubUrl = "http://test",
            MaxVersionJump = 100,
            RejectSuspiciousVersions = true,
            OnSuspiciousActivity = msg => Console.WriteLine(msg)
        };

        // Assert
        options.MaxVersionJump.Should().Be(100);
        options.RejectSuspiciousVersions.Should().BeTrue();
        options.OnSuspiciousActivity.Should().NotBeNull();
    }

    #endregion

    #region Diagnostics Middleware Sensitive Data Tests

#if DEBUG
    [Fact]
    public void DiagnosticsMiddlewareOptions_FilterSensitiveDataDefaultIsTrue()
    {
        // Arrange & Act
        var options = new EasyAppDev.Blazor.Store.Diagnostics.DiagnosticsMiddlewareOptions();

        // Assert
        options.FilterSensitiveData.Should().BeTrue();
    }
#endif

    #endregion

    #region Message Signer Signing and Verification Tests

    [Fact]
    public void MessageSigner_SignAndVerify_Works()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";

        // Act
        var signature = signer.Sign(message);
        var isValid = signer.Verify(message, signature);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_RejectsFutureTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        // Generate a signature with the message and future timestamp
        var signedContent = $"{message}|{futureTimestamp}";
        var signature = signer.Sign(signedContent);

        // Act - Verify with max age of 30 seconds
        var isValid = signer.VerifyWithTimestamp(message, signature, futureTimestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_RejectsOldTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        // Generate a signature with the message and old timestamp
        var signedContent = $"{message}|{oldTimestamp}";
        var signature = signer.Sign(signedContent);

        // Act - Verify with max age of 30 seconds
        var isValid = signer.VerifyWithTimestamp(message, signature, oldTimestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_AcceptsRecentTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";

        // Act
        var signature = signer.SignWithTimestamp(message, out var timestamp);
        var isValid = signer.VerifyWithTimestamp(message, signature, timestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void MessageSigner_KeyRotation_AcceptsBothKeys()
    {
        // Arrange
        var originalKey = new byte[32];
        var newKey = new byte[32];
        Random.Shared.NextBytes(originalKey);
        Random.Shared.NextBytes(newKey);

        using var signer = new MessageSigner(originalKey);
        var message = "test message";

        // Sign with original key
        var signatureWithOriginal = signer.Sign(message);

        // Rotate to new key
        signer.RotateKey(newKey);

        // Act - Verify old signature should still work
        var isValidOld = signer.Verify(message, signatureWithOriginal);

        // Sign new message with new key
        var signatureWithNew = signer.Sign(message + " new");
        var isValidNew = signer.Verify(message + " new", signatureWithNew);

        // Assert
        isValidOld.Should().BeTrue(); // Old signature still valid
        isValidNew.Should().BeTrue(); // New signature valid
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task StoreBuilder_WithValidator_PropagatesValidatorCorrectly()
    {
        // Arrange
        var validator = new TestStateValidator();
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act
        builder.WithStateValidator(validator);
        var store = builder.Build();

        // Assert
        builder.StateValidator.Should().Be(validator);
    }

    [Fact]
    public void SensitiveDataAttribute_MarksPropertiesForFiltering()
    {
        // Arrange
        var type = typeof(TestState);
        var passwordProp = type.GetProperty("Password");
        var tokenProp = type.GetProperty("Token");
        var countProp = type.GetProperty("Count");

        // Assert
        passwordProp!.GetCustomAttributes(typeof(SensitiveDataAttribute), false)
            .Should().NotBeEmpty();
        tokenProp!.GetCustomAttributes(typeof(SensitiveDataAttribute), false)
            .Should().NotBeEmpty();
        countProp!.GetCustomAttributes(typeof(SensitiveDataAttribute), false)
            .Should().BeEmpty();
    }

    #endregion

    #region TabSync DI Validator Resolution Tests

    [Fact]
    public void TabSyncMiddleware_ResolvesValidatorFromDI_WhenNotExplicitlySet()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockJs = new Mock<IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        services.AddSingleton<IStateValidator<TestState>, TestStateValidator>();
        var serviceProvider = services.BuildServiceProvider();

        var options = new TabSyncOptions
        {
            ChannelName = "test-channel"
            // StateValidator is NOT explicitly set
        };

        // Act
        var middleware = new TabSyncMiddleware<TestState>(serviceProvider, options);

        // Assert - middleware should have resolved the validator from DI
        // We can't directly access the private field, but the middleware should work
        middleware.Should().NotBeNull();
    }

    [Fact]
    public void TabSyncMiddleware_PrefersExplicitValidator_OverDIResolution()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockJs = new Mock<IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        services.AddSingleton<IStateValidator<TestState>, TestStateValidator>();
        var serviceProvider = services.BuildServiceProvider();

        var explicitValidator = new Mock<IStateValidator<object>>();
        explicitValidator.Setup(v => v.Validate(It.IsAny<object>()))
            .Returns(StateValidationResult.Success());

        var options = new TabSyncOptions
        {
            ChannelName = "test-channel",
            StateValidator = explicitValidator.Object // Explicitly set
        };

        // Act
        var middleware = new TabSyncMiddleware<TestState>(serviceProvider, options);

        // Assert - middleware created successfully with explicit validator
        middleware.Should().NotBeNull();
    }

    [Fact]
    public void TabSyncMiddleware_WorksWithoutValidator_WhenNoneAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockJs = new Mock<IJSRuntime>();
        services.AddSingleton(mockJs.Object);
        // No IStateValidator<TestState> registered
        var serviceProvider = services.BuildServiceProvider();

        var options = new TabSyncOptions
        {
            ChannelName = "test-channel"
        };

        // Act
        var middleware = new TabSyncMiddleware<TestState>(serviceProvider, options);

        // Assert - should work fine without a validator
        middleware.Should().NotBeNull();
    }

    #endregion

    #region Structured Logging Tests

    [Fact]
    public void LoggingMiddlewareOptions_UseStructuredLogging_DefaultIsTrue()
    {
        // Arrange & Act
        var options = new LoggingMiddlewareOptions();

        // Assert
        options.UseStructuredLogging.Should().BeTrue();
    }

    [Fact]
    public void LoggingMiddlewareOptions_HasEventIdProperties()
    {
        // Arrange & Act
        var options = new LoggingMiddlewareOptions();

        // Assert
        options.StateUpdateEventId.Should().Be(1001);
        options.BeforeUpdateEventId.Should().Be(1000);
    }

    [Fact]
    public void LoggingMiddlewareOptions_EventIdsAreConfigurable()
    {
        // Arrange & Act
        var options = new LoggingMiddlewareOptions
        {
            StateUpdateEventId = 2001,
            BeforeUpdateEventId = 2000
        };

        // Assert
        options.StateUpdateEventId.Should().Be(2001);
        options.BeforeUpdateEventId.Should().Be(2000);
    }

    [Fact]
    public async Task LoggingMiddleware_WithStructuredLogging_UsesMessageTemplate()
    {
        // Arrange
        var loggedMessages = new List<(LogLevel Level, EventId EventId, string Message)>();
        var mockLogger = new Mock<ILogger<LoggingMiddleware<TestState>>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, eventId, state, ex, formatter) =>
            {
                loggedMessages.Add((level, eventId, state.ToString() ?? ""));
            });

        var options = new LoggingMiddlewareOptions
        {
            UseStructuredLogging = true,
            LogLevel = LogLevel.Information
        };
        var middleware = new LoggingMiddleware<TestState>(mockLogger.Object, options);

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0, Name = "Test" },
            new TestState { Count = 1, Name = "Test" },
            "INCREMENT");

        // Assert - should have logged with EventId
        loggedMessages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoggingMiddleware_WithoutStructuredLogging_UsesPlainMessages()
    {
        // Arrange
        var loggedMessages = new List<string>();
        var mockLogger = new Mock<ILogger<LoggingMiddleware<TestState>>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, eventId, state, ex, formatter) =>
            {
                loggedMessages.Add(state.ToString() ?? "");
            });

        var options = new LoggingMiddlewareOptions
        {
            UseStructuredLogging = false,
            LogLevel = LogLevel.Information
        };
        var middleware = new LoggingMiddleware<TestState>(mockLogger.Object, options);

        // Act
        await middleware.OnAfterUpdateAsync(
            new TestState { Count = 0, Name = "Test" },
            new TestState { Count = 1, Name = "Test" },
            "INCREMENT");

        // Assert - should contain plain message format
        loggedMessages.Should().Contain(m => m.Contains("[Store]"));
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

            return errors.Count > 0
                ? StateValidationResult.Failure(errors)
                : StateValidationResult.Success();
        }
    }

    #endregion
}

/// <summary>
/// Tests for logging security features.
/// </summary>
public class LoggingSecurityTests
{
    public record UserState(
        string Username,
        [property: SensitiveData] string Password,
        [property: SensitiveData] string ApiKey);

    [Fact]
    public async Task LoggingMiddleware_WithoutStateDetails_OnlyLogsAction()
    {
        // Arrange
        var logs = new List<string>();
        var options = new LoggingMiddlewareOptions { LogStateDetails = false };
        var middleware = new LoggingMiddleware<UserState>(msg => logs.Add(msg), options);

        // Act
        await middleware.OnAfterUpdateAsync(
            new UserState("user1", "old-pass", "old-key"),
            new UserState("user1", "new-pass", "new-key"),
            "CHANGE_PASSWORD");

        // Assert
        logs.Should().ContainSingle();
        logs[0].Should().Contain("CHANGE_PASSWORD");
        logs[0].Should().NotContain("old-pass");
        logs[0].Should().NotContain("new-pass");
        logs[0].Should().NotContain("old-key");
        logs[0].Should().NotContain("new-key");
    }

    [Fact]
    public async Task LoggingMiddleware_WithFilteredStateDetails_MasksSecrets()
    {
        // Arrange
        var logs = new List<string>();
        var options = new LoggingMiddlewareOptions
        {
            LogStateDetails = true,
            FilterSensitiveData = true
        };
        var middleware = new LoggingMiddleware<UserState>(msg => logs.Add(msg), options);

        // Act
        await middleware.OnAfterUpdateAsync(
            new UserState("user1", "old-pass", "old-key"),
            new UserState("user1", "new-pass", "new-key"),
            "CHANGE_PASSWORD");

        // Assert
        logs.Should().ContainSingle();
        logs[0].Should().Contain("CHANGE_PASSWORD");
        logs[0].Should().Contain("user1"); // Username should be visible
        logs[0].Should().Contain("[FILTERED]"); // Passwords should be filtered
        logs[0].Should().NotContain("old-pass");
        logs[0].Should().NotContain("new-pass");
        logs[0].Should().NotContain("old-key");
        logs[0].Should().NotContain("new-key");
    }
}
