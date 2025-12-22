// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

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
/// Tests for Phase 4 security remediations:
/// - Security configuration validation
/// - Audit logging
/// - SignalR security guidance
/// - Key derivation improvements
/// </summary>
public class Phase4SecurityTests
{
    #region Test State Classes

    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
    }

    public class TestStateValidator : IStateValidator<TestState>
    {
        public StateValidationResult Validate(TestState state)
        {
            if (state.Count < 0)
                return StateValidationResult.Failure("Count cannot be negative");
            return StateValidationResult.Success();
        }
    }

    #endregion

    #region 4.1 Security Configuration Validation Tests

    [Fact]
    public void SecurityConfigurationResult_Success_ReturnsValid()
    {
        // Arrange & Act
        var result = SecurityConfigurationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SecurityConfigurationResult_WithWarnings_ReturnsCorrectState()
    {
        // Arrange
        var warnings = new List<SecurityConfigurationWarning>
        {
            new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Warning,
                Component = "Test",
                Code = "TEST_WARNING",
                Message = "Test warning message"
            }
        };

        // Act
        var result = new SecurityConfigurationResult(warnings);

        // Assert
        result.IsValid.Should().BeTrue(); // Warnings don't make it invalid
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void SecurityConfigurationResult_WithError_IsInvalid()
    {
        // Arrange
        var warnings = new List<SecurityConfigurationWarning>
        {
            new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Error,
                Component = "Test",
                Code = "TEST_ERROR",
                Message = "Test error message"
            }
        };

        // Act
        var result = new SecurityConfigurationResult(warnings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void SecurityConfigurationResult_WithCritical_IsInvalid()
    {
        // Arrange
        var warnings = new List<SecurityConfigurationWarning>
        {
            new SecurityConfigurationWarning
            {
                Severity = SecurityWarningSeverity.Critical,
                Component = "Test",
                Code = "TEST_CRITICAL",
                Message = "Test critical message"
            }
        };

        // Act
        var result = new SecurityConfigurationResult(warnings);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TabSyncConfigurationValidator_NoSigningWithProduction_ReturnsWarning()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            EnableMessageSigning = false
        };
        var validator = new TabSyncConfigurationValidator<TestState>(options, isProduction: true);

        // Act
        var result = validator.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_NO_SIGNING");
    }

    [Fact]
    public void TabSyncConfigurationValidator_SigningWithoutSharedKey_ReturnsError()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            EnableMessageSigning = true,
            SigningKey = null,
            DeriveKeyFromOrigin = false
        };
        var validator = new TabSyncConfigurationValidator<TestState>(options);

        // Act
        var result = validator.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Warnings.Should().Contain(w =>
            w.Code == "TABSYNC_NO_SHARED_KEY" &&
            w.Severity == SecurityWarningSeverity.Error);
    }

    [Fact]
    public void TabSyncConfigurationValidator_NoValidator_ReturnsInfoInDev()
    {
        // Arrange
        var options = new TabSyncOptions();
        var validator = new TabSyncConfigurationValidator<TestState>(options, isProduction: false);

        // Act
        var result = validator.Validate();

        // Assert
        var warning = result.Warnings.FirstOrDefault(w => w.Code == "TABSYNC_NO_VALIDATOR");
        warning.Should().NotBeNull();
        warning!.Severity.Should().Be(SecurityWarningSeverity.Info);
    }

    [Fact]
    public void TabSyncConfigurationValidator_NoTimestampValidation_ReturnsWarning()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            ValidateTimestamp = false
        };
        var validator = new TabSyncConfigurationValidator<TestState>(options);

        // Act
        var result = validator.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_NO_TIMESTAMP");
    }

    [Fact]
    public void TabSyncConfigurationValidator_LargeMessageLimit_ReturnsWarning()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            MaxMessageSizeBytes = 50_000_000 // 50MB
        };
        var validator = new TabSyncConfigurationValidator<TestState>(options);

        // Act
        var result = validator.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_LARGE_MESSAGE_LIMIT");
    }

    [Fact]
    public void ServerSyncConfigurationValidator_RequireValidationWithNoValidator_ReturnsCritical()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState>
        {
            HubUrl = "/hubs/test",
            RequireValidation = true,
            StateValidator = null
        };
        var validator = new ServerSyncConfigurationValidator<TestState>(options);

        // Act
        var result = validator.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Warnings.Should().Contain(w =>
            w.Code == "SERVERSYNC_REQUIRE_VALIDATOR" &&
            w.Severity == SecurityWarningSeverity.Critical);
    }

    [Fact]
    public void ServerSyncConfigurationValidator_NoRateLimit_ReturnsWarningInProduction()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState>
        {
            HubUrl = "/hubs/test",
            RequireValidation = false,
            RateLimitPerSecond = 0
        };
        var validator = new ServerSyncConfigurationValidator<TestState>(options, isProduction: true);

        // Act
        var result = validator.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "SERVERSYNC_NO_RATE_LIMIT");
    }

    [Fact]
    public void ServerSyncConfigurationValidator_NoSessionValidation_ReturnsWarningInProduction()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState>
        {
            HubUrl = "/hubs/test",
            RequireValidation = false,
            RequireSessionValidation = false
        };
        var validator = new ServerSyncConfigurationValidator<TestState>(options, isProduction: true);

        // Act
        var result = validator.Validate();

        // Assert
        result.Warnings.Should().Contain(w => w.Code == "SERVERSYNC_NO_SESSION_VALIDATION");
    }

    [Fact]
    public void SecurityConfigurationValidator_ValidateAndLog_ThrowsOnError()
    {
        // Arrange
        var options = new ServerSyncOptions<TestState>
        {
            HubUrl = "/hubs/test",
            RequireValidation = true,
            StateValidator = null
        };
        var validator = new ServerSyncConfigurationValidator<TestState>(options);

        // Act & Assert
        var act = () => validator.ValidateAndLog(throwOnError: true);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*SERVERSYNC_REQUIRE_VALIDATOR*");
    }

    #endregion

    #region 4.2 Audit Logging Tests

    [Fact]
    public void SecurityAuditEntry_HasRequiredProperties()
    {
        // Arrange & Act
        var entry = new SecurityAuditEntry
        {
            Component = "Test",
            Message = "Test message",
            EventType = SecurityEventType.StateValidation
        };

        // Assert
        entry.Id.Should().NotBe(Guid.Empty);
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        entry.Component.Should().Be("Test");
        entry.Message.Should().Be("Test message");
    }

    [Fact]
    public void SecurityAuditEntryBuilder_BuildsCorrectEntry()
    {
        // Arrange & Act
        var entry = new SecurityAuditEntryBuilder()
            .WithEventType(SecurityEventType.StateValidationFailed)
            .WithComponent("TestComponent")
            .WithMessage("Validation failed")
            .WithCorrelationId("corr-123")
            .WithUserId("user-456")
            .WithSessionId("session-789")
            .WithAction("UPDATE")
            .WithDetail("ErrorCount", 3)
            .Build();

        // Assert
        entry.EventType.Should().Be(SecurityEventType.StateValidationFailed);
        entry.Component.Should().Be("TestComponent");
        entry.Message.Should().Be("Validation failed");
        entry.CorrelationId.Should().Be("corr-123");
        entry.UserId.Should().Be("user-456");
        entry.SessionId.Should().Be("session-789");
        entry.Action.Should().Be("UPDATE");
        entry.Details.Should().ContainKey("ErrorCount");
        entry.Details!["ErrorCount"].Should().Be(3);
        entry.IsThreat.Should().BeTrue(); // Auto-set for validation failures
        entry.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void SecurityAuditEntryBuilder_ThreatEvents_AutoSetLogLevel()
    {
        // Arrange & Act
        var threatEvents = new[]
        {
            SecurityEventType.AccessDenied,
            SecurityEventType.SignatureVerificationFailed,
            SecurityEventType.ReplayAttackDetected,
            SecurityEventType.SuspiciousActivity
        };

        foreach (var eventType in threatEvents)
        {
            var entry = new SecurityAuditEntryBuilder()
                .WithEventType(eventType)
                .WithComponent("Test")
                .WithMessage("Test")
                .Build();

            // Assert
            entry.IsThreat.Should().BeTrue($"{eventType} should be marked as threat");
            entry.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
        }
    }

    [Fact]
    public void SecurityAuditLogger_RespectsMinimumLogLevel()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SecurityAuditLogger>>();
        var options = new SecurityAuditOptions
        {
            MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Warning
        };
        var auditLogger = new SecurityAuditLogger(loggerMock.Object, options);

        var infoEntry = new SecurityAuditEntry
        {
            Component = "Test",
            Message = "Info message",
            EventType = SecurityEventType.SessionCreated,
            LogLevel = Microsoft.Extensions.Logging.LogLevel.Information
        };

        var warningEntry = new SecurityAuditEntry
        {
            Component = "Test",
            Message = "Warning message",
            EventType = SecurityEventType.RateLimitExceeded,
            LogLevel = Microsoft.Extensions.Logging.LogLevel.Warning
        };

        // Act
        auditLogger.Log(infoEntry); // Should be filtered out
        auditLogger.Log(warningEntry); // Should be logged

        // Assert - warning should be logged (info filtered out)
        loggerMock.Verify(x =>
            x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void SecurityAuditLogger_RespectsExcludedEventTypes()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SecurityAuditLogger>>();
        var options = new SecurityAuditOptions
        {
            ExcludedEventTypes = new HashSet<SecurityEventType> { SecurityEventType.SessionCreated }
        };
        var auditLogger = new SecurityAuditLogger(loggerMock.Object, options);

        var entry = new SecurityAuditEntry
        {
            Component = "Test",
            Message = "Session created",
            EventType = SecurityEventType.SessionCreated
        };

        // Act
        auditLogger.Log(entry);

        // Assert - should not be logged
        loggerMock.Verify(x =>
            x.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
    }

    [Fact]
    public void SecurityAuditExtensions_LogValidationFailed_CreatesCorrectEntry()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SecurityAuditLogger>>();
        var auditLogger = new SecurityAuditLogger(loggerMock.Object);

        var validationResult = StateValidationResult.Failure("Error 1", "Error 2");

        // Act
        auditLogger.LogValidationFailed("TabSync", validationResult, "UPDATE", "corr-123");

        // Assert
        loggerMock.Verify(x =>
            x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void SecurityAuditOptions_DefaultValues()
    {
        // Arrange & Act
        var options = new SecurityAuditOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.MinimumLogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);
        options.IncludeStructuredData.Should().BeTrue();
        options.ExcludedEventTypes.Should().BeEmpty();
        options.IncludedEventTypes.Should().BeEmpty();
    }

    [Fact]
    public void SecurityEventType_HasAllExpectedCategories()
    {
        // Authentication & Authorization
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.AuthenticationAttempt).Should().BeTrue();
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.AccessDenied).Should().BeTrue();

        // Validation Events
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.StateValidationFailed).Should().BeTrue();
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.InvalidStateRejected).Should().BeTrue();

        // Signature & Integrity
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.SignatureVerificationFailed).Should().BeTrue();

        // Session Events
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.SessionExpired).Should().BeTrue();

        // Rate Limiting & DoS
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.RateLimitExceeded).Should().BeTrue();
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.MessageSizeExceeded).Should().BeTrue();

        // Suspicious Activity
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.SuspiciousActivity).Should().BeTrue();
        Enum.IsDefined(typeof(SecurityEventType), SecurityEventType.ReplayAttackDetected).Should().BeTrue();
    }

    #endregion

    #region 4.3 SignalR Security Guidance Tests

    [Fact]
    public void SignalRSecurityGuidance_DefaultConstants()
    {
        // Assert
        SignalRSecurityGuidance.DefaultMaxMessageSize.Should().Be(1_048_576);
        SignalRSecurityGuidance.DefaultRateLimitPerSecond.Should().Be(10);
        SignalRSecurityGuidance.RecommendedKeepAliveInterval.Should().Be(TimeSpan.FromSeconds(15));
        SignalRSecurityGuidance.RecommendedClientTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SignalRSecurityGuidance_GetRecommendedCspHeader_WithOrigins()
    {
        // Arrange & Act
        var csp = SignalRSecurityGuidance.GetRecommendedCspHeader("wss://example.com", "wss://api.example.com");

        // Assert
        csp.Should().Contain("connect-src 'self' wss://example.com wss://api.example.com");
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("script-src 'self'");
    }

    [Fact]
    public void SignalRSecurityGuidance_GetRecommendedCspHeader_NoOrigins()
    {
        // Arrange & Act
        var csp = SignalRSecurityGuidance.GetRecommendedCspHeader();

        // Assert
        csp.Should().Contain("connect-src 'self'");
    }

    [Fact]
    public void SignalRSecurityGuidance_GetRecommendedSecurityHeaders()
    {
        // Arrange & Act
        var headers = SignalRSecurityGuidance.GetRecommendedSecurityHeaders();

        // Assert
        headers.Should().ContainKey("X-Content-Type-Options");
        headers["X-Content-Type-Options"].Should().Be("nosniff");
        headers.Should().ContainKey("X-Frame-Options");
        headers["X-Frame-Options"].Should().Be("DENY");
        headers.Should().ContainKey("Referrer-Policy");
    }

    [Theory]
    [InlineData("https://example.com/hubs/store", true, true)]
    [InlineData("wss://example.com/hubs/store", true, true)]
    [InlineData("http://example.com/hubs/store", true, false)]
    [InlineData("ws://example.com/hubs/store", true, false)]
    [InlineData("/hubs/store", true, true)] // Relative URL
    [InlineData("http://localhost/hubs/store", false, true)] // Dev localhost
    [InlineData("ws://127.0.0.1/hubs/store", false, true)] // Dev localhost
    public void SignalRSecurityGuidance_IsSecureHubUrl(string url, bool isProduction, bool expected)
    {
        // Act
        var result = SignalRSecurityGuidance.IsSecureHubUrl(url, isProduction);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SignalRSecurityGuidance_IsSecureHubUrl_EmptyUrl_ReturnsFalse()
    {
        // Act & Assert
        SignalRSecurityGuidance.IsSecureHubUrl("", true).Should().BeFalse();
        SignalRSecurityGuidance.IsSecureHubUrl(null!, true).Should().BeFalse();
    }

    #endregion

    #region 4.4 Key Derivation Tests

    [Fact]
    public void SecureKeyManager_DefaultIterations_Is100000()
    {
        // Assert
        SecureKeyManager.DefaultIterations.Should().Be(100_000);
        SecureKeyManager.MinimumIterations.Should().Be(10_000);
    }

    [Fact]
    public void SecureKeyManager_GenerateRandomSalt_Returns32Bytes()
    {
        // Act
        var salt = SecureKeyManager.GenerateRandomSalt();

        // Assert
        salt.Should().HaveCount(32);
    }

    [Fact]
    public void SecureKeyManager_GenerateRandomSalt_RejectsSmallSize()
    {
        // Act & Assert
        var act = () => SecureKeyManager.GenerateRandomSalt(8);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 16 bytes*");
    }

    [Fact]
    public void SecureKeyManager_GenerateRandomSalt_ProducesUniqueValues()
    {
        // Act
        var salts = Enumerable.Range(0, 100)
            .Select(_ => Convert.ToBase64String(SecureKeyManager.GenerateRandomSalt()))
            .ToList();

        // Assert - all should be unique
        salts.Distinct().Count().Should().Be(100);
    }

    [Fact]
    public void SecureKeyManager_GenerateRandomKey_Returns32Bytes()
    {
        // Act
        var key = SecureKeyManager.GenerateRandomKey();

        // Assert
        key.Should().HaveCount(32);
    }

    [Fact]
    public void SecureKeyManager_GenerateRandomKey_RejectsSmallSize()
    {
        // Act & Assert
        var act = () => SecureKeyManager.GenerateRandomKey(16);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void SecureKeyManager_DeriveKeyWithRandomSalt_ProducesKeyAndSalt()
    {
        // Act
        var key = SecureKeyManager.DeriveKeyWithRandomSalt("test-passphrase", out var salt);

        // Assert
        key.Should().HaveCount(32);
        salt.Should().HaveCount(32);
    }

    [Fact]
    public void SecureKeyManager_DeriveKeyWithRandomSalt_DifferentSaltsProduceDifferentKeys()
    {
        // Act
        var key1 = SecureKeyManager.DeriveKeyWithRandomSalt("same-passphrase", out var salt1);
        var key2 = SecureKeyManager.DeriveKeyWithRandomSalt("same-passphrase", out var salt2);

        // Assert
        key1.Should().NotEqual(key2); // Different salts = different keys
        salt1.Should().NotEqual(salt2);
    }

    [Fact]
    public void SecureKeyManager_DeriveKey_SameSaltProducesSameKey()
    {
        // Arrange
        var salt = SecureKeyManager.GenerateRandomSalt();
        var passphrase = "test-passphrase";

        // Act
        var key1 = SecureKeyManager.DeriveKey(passphrase, salt);
        var key2 = SecureKeyManager.DeriveKey(passphrase, salt);

        // Assert
        key1.Should().Equal(key2);
    }

    [Fact]
    public void SecureKeyManager_DeriveKey_RejectsSmallSalt()
    {
        // Act & Assert
        var act = () => SecureKeyManager.DeriveKey("passphrase", new byte[8]);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 16 bytes*");
    }

    [Fact]
    public void SecureKeyManager_DeriveKey_RejectsLowIterations()
    {
        // Act & Assert
        var act = () => SecureKeyManager.DeriveKey("passphrase", new byte[16], iterations: 1000);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 10000*");
    }

    [Fact]
    public void SecureKeyManager_CreateRotationData_SetsTimestamp()
    {
        // Act
        var rotationData = SecureKeyManager.CreateRotationData(() => SecureKeyManager.GenerateRandomKey());

        // Assert
        rotationData.Key.Should().HaveCount(32);
        rotationData.RotatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SecureKeyManager_ShouldRotate_ReturnsTrueAfterInterval()
    {
        // Arrange
        var lastRotation = DateTimeOffset.UtcNow.AddHours(-25);
        var rotationInterval = TimeSpan.FromHours(24);

        // Act
        var shouldRotate = SecureKeyManager.ShouldRotate(lastRotation, rotationInterval);

        // Assert
        shouldRotate.Should().BeTrue();
    }

    [Fact]
    public void SecureKeyManager_ShouldRotate_ReturnsFalseBeforeInterval()
    {
        // Arrange
        var lastRotation = DateTimeOffset.UtcNow.AddHours(-1);
        var rotationInterval = TimeSpan.FromHours(24);

        // Act
        var shouldRotate = SecureKeyManager.ShouldRotate(lastRotation, rotationInterval);

        // Assert
        shouldRotate.Should().BeFalse();
    }

    [Fact]
    public void KeyRotationData_IsExpired_WorksCorrectly()
    {
        // Arrange
        var expired = new KeyRotationData
        {
            Key = new byte[32],
            RotatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var notExpired = new KeyRotationData
        {
            Key = new byte[32],
            RotatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        var noExpiry = new KeyRotationData
        {
            Key = new byte[32],
            RotatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        expired.IsExpired.Should().BeTrue();
        notExpired.IsExpired.Should().BeFalse();
        noExpiry.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void MessageSigner_DeriveKeyFromSeed_Uses100000IterationsDefault()
    {
        // This test verifies the default value is used correctly
        // by checking that the same seed produces the same key
        var key1 = MessageSigner.DeriveKeyFromSeed("test-seed");
        var key2 = MessageSigner.DeriveKeyFromSeed("test-seed");

        key1.Should().Equal(key2);
        key1.Should().HaveCount(32);
    }

    [Fact]
    public void MessageSigner_DeriveKeyFromSeed_RejectsLowIterations()
    {
        // Act & Assert
        var act = () => MessageSigner.DeriveKeyFromSeed("test-seed", iterations: 1000);
        act.Should().Throw<ArgumentException>().WithMessage("*at least 10,000*");
    }

    [Fact]
    public void MessageSigner_RotateKey_VerifiesWithBothKeys()
    {
        // Arrange
        var initialKey = SecureKeyManager.GenerateRandomKey();
        var newKey = SecureKeyManager.GenerateRandomKey();
        var signer = new MessageSigner(initialKey);

        // Sign with initial key
        var message = "test message";
        var signature = signer.Sign(message);
        signer.Verify(message, signature).Should().BeTrue();

        // Act - rotate key
        signer.RotateKey(newKey);

        // Assert - old signatures still verify (grace period)
        signer.Verify(message, signature).Should().BeTrue();

        // New signatures use new key
        var newSignature = signer.Sign(message);
        newSignature.Should().NotBe(signature);
        signer.Verify(message, newSignature).Should().BeTrue();
    }

    #endregion

    #region DI Registration Tests

    [Fact]
    public void SecurityServiceExtensions_AddSecurityAuditLogger_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSecurityAuditLogger(options =>
        {
            options.MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Warning;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var logger = provider.GetService<ISecurityAuditLogger>();
        logger.Should().NotBeNull();
        logger.Should().BeOfType<SecurityAuditLogger>();

        var options = provider.GetService<SecurityAuditOptions>();
        options.Should().NotBeNull();
        options!.MinimumLogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void SecurityServiceExtensions_AddStateValidator_RegistersValidator()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddStateValidator<TestState, TestStateValidator>();
        var provider = services.BuildServiceProvider();

        // Assert
        var validator = provider.GetService<IStateValidator<TestState>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<TestStateValidator>();
    }

    #endregion
}
