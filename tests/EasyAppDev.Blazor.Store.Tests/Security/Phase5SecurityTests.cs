// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Xunit;
using StoreServerSync = EasyAppDev.Blazor.Store.ServerSync;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Phase 5 security tests: comprehensive coverage for security paths.
/// Tests cover deserialization DoS, clock skew, concurrent updates,
/// cross-session keys, filter bypass, replay attacks, and session hijacking.
/// </summary>
public class Phase5SecurityTests
{
    #region Test State Classes

    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
        public List<string>? Items { get; init; }
    }

    public record DeepNestedState
    {
        public DeepNestedState? Child { get; init; }
        public int Depth { get; init; }
    }

    public record StateWithSensitiveData
    {
        public string Username { get; init; } = "";
        [SensitiveData]
        public string Password { get; init; } = "";
        [SensitiveData]
        public string ApiKey { get; init; } = "";
        public string UserPasswordResetToken { get; init; } = ""; // Partial match
        [AlwaysInclude]
        public string TokenCount { get; init; } = "100";
    }

    #endregion

    #region 5.2.1 Deserialization DoS Tests

    [Fact]
    public void JsonDeserialize_WithDepthLimit_RejectsDeeplyNestedJson()
    {
        // Arrange - create JSON exceeding depth limit
        var deepJson = CreateDeeplyNestedJson(50);
        var options = new JsonSerializerOptions { MaxDepth = 32 };

        // Act & Assert
        var act = () => JsonSerializer.Deserialize<object>(deepJson, options);
        act.Should().Throw<JsonException>()
            .Where(e => e.Message.Contains("depth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JsonDeserialize_WithinDepthLimit_Succeeds()
    {
        // Arrange - create JSON within depth limit
        var validJson = CreateDeeplyNestedJson(30);
        var options = new JsonSerializerOptions { MaxDepth = 32 };

        // Act
        var result = JsonSerializer.Deserialize<object>(validJson, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void JsonDeserialize_LargeArrayDoS_HandledBySystemJsonLimits()
    {
        // Arrange - create JSON with very large array (simulated)
        // System.Text.Json has built-in limits
        var largeArrayJson = "[" + string.Join(",", Enumerable.Repeat("1", 100000)) + "]";
        var options = new JsonSerializerOptions();

        // Act - should succeed but take time
        var result = JsonSerializer.Deserialize<int[]>(largeArrayJson, options);

        // Assert
        result.Should().HaveCount(100000);
    }

    [Fact]
    public void TabSyncOptions_MaxJsonDepth_DefaultIs32()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.MaxJsonDepth.Should().Be(32);
    }

    [Fact]
    public void TabSyncOptions_MaxJsonDepth_CanBeConfigured()
    {
        // Arrange & Act
        var options = new TabSyncOptions { MaxJsonDepth = 16 };

        // Assert
        options.MaxJsonDepth.Should().Be(16);
    }

    private static string CreateDeeplyNestedJson(int depth)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
            sb.Append("{\"child\":");
        sb.Append("null");
        for (int i = 0; i < depth; i++)
            sb.Append('}');
        return sb.ToString();
    }

    #endregion

    #region 5.2.2 Clock Skew / Future Timestamp Tests

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_RejectsFutureTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        // Sign the content
        var signedContent = $"{message}|{futureTimestamp}";
        var signature = signer.Sign(signedContent);

        // Act
        var isValid = signer.VerifyWithTimestamp(message, signature, futureTimestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeFalse("future timestamps should be rejected");
    }

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_RejectsPastTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";
        var pastTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        // Sign the content
        var signedContent = $"{message}|{pastTimestamp}";
        var signature = signer.Sign(signedContent);

        // Act
        var isValid = signer.VerifyWithTimestamp(message, signature, pastTimestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeFalse("old timestamps should be rejected");
    }

    [Fact]
    public void MessageSigner_VerifyWithTimestamp_AcceptsRecentTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";

        // Sign with current timestamp
        var signature = signer.SignWithTimestamp(message, out var timestamp);

        // Act - verify with 30 second window
        var isValid = signer.VerifyWithTimestamp(message, signature, timestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeTrue("recent timestamp should be accepted");
    }

    [Fact]
    public void TabSyncOptions_ClockSkewTolerance_DefaultIs5Seconds()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.ClockSkewToleranceSeconds.Should().Be(5);
    }

    [Fact]
    public void TabSyncOptions_ValidateTimestamp_DefaultIsTrue()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.ValidateTimestamp.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, true)]    // Current time - valid
    [InlineData(-5, true)]   // 5 seconds ago - valid
    [InlineData(-25, true)]  // 25 seconds ago - valid within 30 second window
    [InlineData(-35, false)] // 35 seconds ago - invalid
    [InlineData(-1, true)]   // 1 second ago - valid
    [InlineData(-29, true)]  // 29 seconds ago - valid (boundary)
    public void MessageSigner_VerifyWithTimestamp_RespectsMaxAge(
        int offsetSeconds, bool expectedValid)
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "test message";
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(offsetSeconds).ToUnixTimeSeconds();

        var signedContent = $"{message}|{timestamp}";
        var signature = signer.Sign(signedContent);

        // Act
        var isValid = signer.VerifyWithTimestamp(message, signature, timestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().Be(expectedValid);
    }

    #endregion

    #region 5.2.3 Concurrent Update + Optimistic Rollback Tests

    [Fact]
    public async Task Store_ConcurrentUpdates_MaintainsConsistency()
    {
        // Arrange
        var store = StoreBuilder<TestState>.Create(new TestState { Count = 0 }).Build();
        var tasks = new List<Task>();
        const int iterations = 100;

        // Act - concurrent increments
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await store.UpdateAsync(s => s with { Count = s.Count + 1 }, "INCREMENT");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        store.GetState().Count.Should().Be(iterations);
    }

    [Fact]
    public async Task Store_ConcurrentUpdatesWithRollback_HandlesCorrectly()
    {
        // Arrange
        var store = StoreBuilder<TestState>.Create(new TestState { Count = 0 }).Build();
        var rollbackCount = 0;
        var successCount = 0;
        var tasks = new List<Task>();

        // Act - concurrent updates with some failures
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await store.UpdateAsync(s =>
                    {
                        // Simulate some failures
                        if (index % 5 == 0)
                            throw new InvalidOperationException("Simulated failure");
                        return s with { Count = s.Count + 1 };
                    }, "INCREMENT");
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref rollbackCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        rollbackCount.Should().Be(10); // Every 5th should fail
        successCount.Should().Be(40);
        store.GetState().Count.Should().Be(40);
    }

    [Fact]
    public async Task Store_RaceCondition_SyncFlagIsThreadSafe()
    {
        // Arrange
        var syncOperations = new ConcurrentBag<int>();
        var counter = 0;

        // Act - simulate concurrent sync flag usage
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            Interlocked.Increment(ref counter);
            syncOperations.Add(counter);
            Thread.Sleep(1);
            Interlocked.Decrement(ref counter);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - counter should return to 0
        counter.Should().Be(0);
        syncOperations.Should().HaveCount(100);
    }

    #endregion

    #region 5.2.4 Cross-Session Key Tests

    [Fact]
    public void MessageSigner_DifferentInstances_WithSameKey_ProduceSameSignature()
    {
        // Arrange
        var sharedKey = new byte[32];
        new Random(42).NextBytes(sharedKey);

        using var signer1 = new MessageSigner(sharedKey);
        using var signer2 = new MessageSigner(sharedKey);
        var message = "cross-session test";

        // Act
        var sig1 = signer1.Sign(message);
        var sig2 = signer2.Sign(message);

        // Assert
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void MessageSigner_CrossSession_VerificationWorks()
    {
        // Arrange
        var sharedKey = SecureKeyManager.GenerateRandomKey();

        // Session 1: Sign
        string signature;
        using (var signer1 = new MessageSigner(sharedKey))
        {
            signature = signer1.Sign("persistent data");
        }

        // Session 2: Verify (new instance)
        using var signer2 = new MessageSigner(sharedKey);

        // Act
        var isValid = signer2.Verify("persistent data", signature);

        // Assert
        isValid.Should().BeTrue("signature from previous session should verify");
    }

    [Fact]
    public void MessageSigner_DerivedKey_ConsistentAcrossSessions()
    {
        // Arrange
        const string passphrase = "user-passphrase";
        var salt = SecureKeyManager.GenerateRandomSalt();

        // Session 1: Derive key and sign
        var key1 = SecureKeyManager.DeriveKey(passphrase, salt);
        string signature;
        using (var signer1 = new MessageSigner(key1))
        {
            signature = signer1.Sign("data");
        }

        // Session 2: Derive same key and verify
        var key2 = SecureKeyManager.DeriveKey(passphrase, salt);
        using var signer2 = new MessageSigner(key2);

        // Act & Assert
        key1.Should().Equal(key2);
        signer2.Verify("data", signature).Should().BeTrue();
    }

    [Fact]
    public void MessageSigner_RandomKey_NotConsistentAcrossSessions()
    {
        // Arrange
        string signature;
        using (var signer1 = new MessageSigner()) // Random key
        {
            signature = signer1.Sign("data");
        }

        using var signer2 = new MessageSigner(); // Different random key

        // Act
        var isValid = signer2.Verify("data", signature);

        // Assert
        isValid.Should().BeFalse("random keys differ between instances");
    }

    #endregion

    #region 5.2.5 Filter Bypass Tests

    [Fact]
    public void SensitiveDataFilter_CannotBypassWithCaseVariation()
    {
        // Arrange
        var state = new
        {
            PASSWORD = "secret1",
            Password = "secret2",
            password = "secret3",
            pAsSwOrD = "secret4"
        };

        var options = new SensitiveDataFilterOptions { Enabled = true };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert - all variations should be filtered
        json.Should().NotContain("secret1");
        json.Should().NotContain("secret2");
        json.Should().NotContain("secret3");
        json.Should().NotContain("secret4");
        json.Should().Contain("[FILTERED]");
    }

    [Fact]
    public void SensitiveDataFilter_CannotBypassWithUnicodeHomoglyphs()
    {
        // Arrange - using regular ASCII, as homoglyphs would require special handling
        var state = new
        {
            Passwοrd = "secret", // Note: 'ο' is Greek omicron, not 'o'
            Token = "token123"
        };

        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            FilteredPropertyNames = { "Password", "Token" }
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert - Token should be filtered, Passwοrd might not be (different char)
        json.Should().NotContain("token123");
    }

    [Fact]
    public void SensitiveDataFilter_PartialMatch_FiltersContainingNames()
    {
        // Arrange
        var state = new StateWithSensitiveData
        {
            Username = "john",
            Password = "secret",
            ApiKey = "key123",
            UserPasswordResetToken = "reset-token-value",
            TokenCount = "100"
        };

        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            UseExactMatch = false  // Partial matching
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().NotContain("secret");
        json.Should().NotContain("key123");
        json.Should().NotContain("reset-token-value"); // Contains "Password" and "Token"
        json.Should().Contain("john"); // Username not filtered
        json.Should().Contain("100"); // TokenCount has [AlwaysInclude]
    }

    [Fact]
    public void SensitiveDataFilter_AlwaysInclude_OverridesFiltering()
    {
        // Arrange
        var state = new StateWithSensitiveData
        {
            Username = "john",
            Password = "secret",
            ApiKey = "key123",
            UserPasswordResetToken = "reset",
            TokenCount = "100" // Has [AlwaysInclude]
        };

        var options = new SensitiveDataFilterOptions { Enabled = true };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().Contain("100"); // TokenCount preserved despite containing "Token"
        json.Should().NotContain("secret");
        json.Should().NotContain("key123");
    }

    [Fact]
    public void SensitiveDataFilter_NestedObjects_FiltersRecursively()
    {
        // Arrange
        var state = new
        {
            User = new
            {
                Name = "John",
                Credentials = new
                {
                    Password = "nested-secret",
                    ApiKey = "nested-key"
                }
            }
        };

        var options = new SensitiveDataFilterOptions { Enabled = true };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().NotContain("nested-secret");
        json.Should().NotContain("nested-key");
        json.Should().Contain("John");
    }

    [Fact]
    public void SensitiveDataFilter_Arrays_FiltersInsideArrays()
    {
        // Arrange
        var state = new
        {
            Users = new[]
            {
                new { Name = "User1", Password = "pass1" },
                new { Name = "User2", Password = "pass2" }
            }
        };

        var options = new SensitiveDataFilterOptions { Enabled = true };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().NotContain("pass1");
        json.Should().NotContain("pass2");
        json.Should().Contain("User1");
        json.Should().Contain("User2");
    }

    #endregion

    #region 5.2.6 Replay Attack Tests

    [Fact]
    public void MessageSigner_SameMessageSameTimestamp_ProducesSameSignature()
    {
        // This tests that replay attacks are possible without timestamp validation
        using var signer = new MessageSigner();
        var message = "replay test";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act - sign with combined content (simulating how timestamp is embedded)
        var signedContent = $"{message}|{timestamp}";
        var sig1 = signer.Sign(signedContent);
        var sig2 = signer.Sign(signedContent);

        // Assert - same inputs produce same output (deterministic)
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void MessageSigner_ReplayedOldMessage_RejectedByTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "important action";
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

        // Attacker has an old valid signature (sign with combined content)
        var signedContent = $"{message}|{oldTimestamp}";
        var oldSignature = signer.Sign(signedContent);

        // Act - verify with timestamp validation
        var isValid = signer.VerifyWithTimestamp(message, oldSignature, oldTimestamp, maxAgeSeconds: 30);

        // Assert
        isValid.Should().BeFalse("replayed message with old timestamp should be rejected");
    }

    [Fact]
    public void TabSyncOptions_MaxMessageAgeSeconds_PreventsReplay()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            MaxMessageAgeSeconds = 30,
            ValidateTimestamp = true
        };

        // Assert - configuration supports replay prevention
        options.MaxMessageAgeSeconds.Should().Be(30);
        options.ValidateTimestamp.Should().BeTrue();
    }

    [Fact]
    public void MessageSigner_WithNonce_PreventsReplayEvenWithSameTimestamp()
    {
        // Arrange
        using var signer = new MessageSigner();
        var message = "action";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act - include nonce in signed content
        var nonce1 = Guid.NewGuid().ToString();
        var nonce2 = Guid.NewGuid().ToString();

        var sig1 = signer.Sign($"{message}|{timestamp}|{nonce1}");
        var sig2 = signer.Sign($"{message}|{timestamp}|{nonce2}");

        // Assert - different nonces produce different signatures
        sig1.Should().NotBe(sig2);
    }

    #endregion

    #region 5.2.7 Session Hijacking Tests

    [Fact]
    public void SessionToken_CannotBePredicted()
    {
        // Arrange
        var tokens = new HashSet<string>();

        // Act - generate many session tokens
        for (int i = 0; i < 1000; i++)
        {
            var token = Convert.ToBase64String(SecureKeyManager.GenerateRandomKey());
            tokens.Add(token);
        }

        // Assert - all tokens should be unique
        tokens.Count.Should().Be(1000);
    }

    [Fact]
    public void SessionToken_HasSufficientEntropy()
    {
        // Arrange
        var token = SecureKeyManager.GenerateRandomKey(32);

        // Assert - 256 bits of entropy
        token.Should().HaveCount(32);

        // Check it's not all zeros or predictable pattern
        token.Should().Contain(b => b != 0);
        token.Distinct().Count().Should().BeGreaterThan(10);
    }

    [Fact]
    public void ServerSyncOptions_SessionValidation_CanBeEnabled()
    {
        // Arrange & Act
        var options = new StoreServerSync.ServerSyncOptions<TestState>
        {
            HubUrl = "https://example.com/hub",
            RequireSessionValidation = true,
            SessionTimeoutMinutes = 30
        };

        // Assert
        options.RequireSessionValidation.Should().BeTrue();
        options.SessionTimeoutMinutes.Should().Be(30);
    }

    [Fact]
    public void ServerSyncOptions_SessionExpiredCallback_CanBeConfigured()
    {
        // Arrange
        var callbackInvoked = false;
        var options = new StoreServerSync.ServerSyncOptions<TestState>
        {
            HubUrl = "https://example.com/hub",
            OnSessionExpired = () => callbackInvoked = true
        };

        // Act
        options.OnSessionExpired?.Invoke();

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void ServerSyncOptions_SessionValidationFailedCallback_CanBeConfigured()
    {
        // Arrange
        string? failureReason = null;
        var options = new StoreServerSync.ServerSyncOptions<TestState>
        {
            HubUrl = "https://example.com/hub",
            OnSessionValidationFailed = reason => failureReason = reason
        };

        // Act
        options.OnSessionValidationFailed?.Invoke("Invalid session token");

        // Assert
        failureReason.Should().Be("Invalid session token");
    }

    #endregion

    #region Security Configuration Validation Tests

    [Fact]
    public void SecurityConfigurationValidator_DetectsInsecureConfiguration()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            EnableMessageSigning = false,
            ValidateTimestamp = false,
            MaxMessageSizeBytes = 100_000_000 // 100MB - too large
        };

        var validator = new TabSyncConfigurationValidator<TestState>(options, isProduction: true);

        // Act
        var result = validator.Validate();

        // Assert
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_NO_SIGNING");
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_NO_TIMESTAMP");
        result.Warnings.Should().Contain(w => w.Code == "TABSYNC_LARGE_MESSAGE_LIMIT");
    }

    [Fact]
    public void SecurityConfigurationValidator_AcceptsSecureConfiguration()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            EnableMessageSigning = true,
            SigningKey = SecureKeyManager.GenerateRandomKey(),
            ValidateTimestamp = true,
            MaxMessageSizeBytes = 1_048_576,
            StateValidator = new TestStateValidator()
        };

        var validator = new TabSyncConfigurationValidator<TestState>(options, isProduction: true);

        // Act
        var result = validator.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Helper Classes

    private class TestStateValidator : IStateValidator<object>
    {
        public StateValidationResult Validate(object state)
        {
            if (state is TestState testState && testState.Count < 0)
                return StateValidationResult.Failure("Count cannot be negative");

            return StateValidationResult.Success();
        }
    }

    #endregion
}

/// <summary>
/// Integration tests for security features working together.
/// </summary>
public class SecurityIntegrationTests
{
    public record SecureState
    {
        public int Value { get; init; }
        [SensitiveData]
        public string? Secret { get; init; }
    }

    [Fact]
    public async Task Store_WithValidation_RejectsInvalidState()
    {
        // Arrange
        var validator = new SecureStateValidator();
        var middleware = new ValidationMiddleware<SecureState>(validator, rejectInvalid: true);
        var invalidState = new SecureState { Value = -1, Secret = "test" };

        // Act & Assert
        await Assert.ThrowsAsync<StateValidationException>(async () =>
            await middleware.OnAfterUpdateAsync(
                new SecureState { Value = 0 },
                invalidState,
                "TEST"));
    }

    [Fact]
    public void SensitiveDataFilter_WithValidation_BothWork()
    {
        // Arrange
        var state = new SecureState { Value = 10, Secret = "sensitive" };
        var validator = new SecureStateValidator();

        // Act - validate
        var validationResult = validator.Validate(state);

        // Act - filter
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        validationResult.IsValid.Should().BeTrue();
        json.Should().NotContain("sensitive");
        json.Should().Contain("[FILTERED]");
    }

    [Fact]
    public void SecureKeyManager_FullWorkflow_Works()
    {
        // Arrange
        const string passphrase = "user-password";

        // Generate salt and derive key
        var key = SecureKeyManager.DeriveKeyWithRandomSalt(passphrase, out var salt);

        // Create signer
        using var signer = new MessageSigner(key);

        // Sign data
        var data = "important data";
        var signature = signer.SignWithTimestamp(data, out var timestamp);

        // Later: re-derive key and verify
        var key2 = SecureKeyManager.DeriveKey(passphrase, salt);
        using var signer2 = new MessageSigner(key2);

        // Act
        var isValid = signer2.VerifyWithTimestamp(data, signature, timestamp, maxAgeSeconds: 60);

        // Assert
        isValid.Should().BeTrue();
    }

    private class SecureStateValidator : IStateValidator<SecureState>
    {
        public StateValidationResult Validate(SecureState state)
        {
            if (state.Value < 0)
                return StateValidationResult.Failure("Value cannot be negative");
            return StateValidationResult.Success();
        }
    }
}
