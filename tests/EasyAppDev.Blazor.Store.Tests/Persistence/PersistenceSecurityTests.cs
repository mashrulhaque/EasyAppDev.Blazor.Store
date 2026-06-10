// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Persistence;

public class PersistenceSecurityTests
{
    public record TestState
    {
        public int Count { get; init; }
        [SensitiveData]
        public string? Password { get; init; }
        public string? Token { get; init; }
        public string? Username { get; init; }
    }

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static byte[] CreateTestSigningKey()
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
            key[i] = (byte)(i + 1);
        return key;
    }

    [Fact]
    public async Task SaveStateAsync_WithIntegrityCheck_StoresSignedState()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = CreateTestSigningKey(),
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);
        var state = new TestState { Count = 42, Password = "secret123", Token = "token456", Username = "john" };

        // Act
        await middleware.OnAfterUpdateAsync(state, state, "TEST");

        // Assert
        mockProvider.Verify(p => p.SaveAsync(
            "test-key",
            It.Is<string>(json =>
                json.Contains("\"signature\":") &&
                json.Contains("\"state\":") &&
                json.Contains("\"version\":1")
            )),
            Times.Once);
    }

    [Fact]
    public async Task SaveStateAsync_WithSensitiveDataFiltering_FiltersPassword()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = false,
            FilterSensitiveData = true
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);
        var state = new TestState { Count = 42, Password = "secret123", Token = "token456", Username = "john" };

        // Act
        await middleware.OnAfterUpdateAsync(state, state, "TEST");

        // Assert
        mockProvider.Verify(p => p.SaveAsync(
            "test-key",
            It.Is<string>(json =>
                json.Contains("[FILTERED]") && // Password should be filtered
                json.Contains("[FILTERED]") && // Token should be filtered
                json.Contains("john")          // Username should not be filtered
            )),
            Times.Once);
    }

    [Fact]
    public async Task SaveStateAsync_ExceedingSizeLimit_ThrowsException()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = false,
            FilterSensitiveData = false,
            MaxStateSize = 10 // Very small limit
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);
        var state = new TestState { Count = 42, Password = "very-long-password-that-exceeds-size-limit", Username = "john" };

        // Act & Assert
        await Assert.ThrowsAsync<StateSizeExceededException>(async () =>
            await middleware.OnAfterUpdateAsync(state, state, "TEST"));

        mockProvider.Verify(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadStateAsync_WithValidSignature_LoadsState()
    {
        // Arrange
        var testState = new TestState { Count = 42, Username = "john" };
        var signer = new MessageSigner();
        var stateJson = JsonSerializer.Serialize(testState, CamelCaseOptions);
        var signature = signer.Sign(stateJson);

        var wrapper = new PersistedStateWrapper
        {
            Version = 1,
            State = stateJson,
            Signature = signature,
            Size = stateJson.Length,
            Timestamp = DateTimeOffset.UtcNow
        };

        var wrapperJson = JsonSerializer.Serialize(wrapper, CamelCaseOptions);

        var mockProvider = new Mock<IPersistenceProvider>();
        mockProvider.Setup(p => p.LoadAsync("test-key")).ReturnsAsync(wrapperJson);

        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = signer.Key,
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Count.Should().Be(42);
        loadedState.Username.Should().Be("john");
    }

    [Fact]
    public async Task LoadStateAsync_WithInvalidSignature_ReturnsNull()
    {
        // Arrange
        var testState = new TestState { Count = 42, Username = "john" };
        var stateJson = JsonSerializer.Serialize(testState, CamelCaseOptions);

        var wrapper = new PersistedStateWrapper
        {
            Version = 1,
            State = stateJson,
            Signature = "invalid-signature-base64==",
            Size = stateJson.Length,
            Timestamp = DateTimeOffset.UtcNow
        };

        var wrapperJson = JsonSerializer.Serialize(wrapper, CamelCaseOptions);

        var mockProvider = new Mock<IPersistenceProvider>();
        mockProvider.Setup(p => p.LoadAsync("test-key")).ReturnsAsync(wrapperJson);

        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = CreateTestSigningKey(),
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().BeNull();
    }

    [Fact]
    public async Task LoadStateAsync_LegacyFormat_LoadsSuccessfully()
    {
        // Arrange
        var testState = new TestState { Count = 42, Username = "john" };
        var legacyJson = JsonSerializer.Serialize(testState, CamelCaseOptions);

        var mockProvider = new Mock<IPersistenceProvider>();
        mockProvider.Setup(p => p.LoadAsync("test-key")).ReturnsAsync(legacyJson);

        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = false,
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().NotBeNull();
        loadedState!.Count.Should().Be(42);
        loadedState.Username.Should().Be("john");
    }

    [Fact]
    public async Task LoadStateAsync_TamperedState_DetectsModification()
    {
        // Arrange
        var originalState = new TestState { Count = 42, Username = "john" };
        var signer = new MessageSigner();
        var stateJson = JsonSerializer.Serialize(originalState, CamelCaseOptions);
        var signature = signer.Sign(stateJson);

        // Tamper with the state
        var tamperedState = new TestState { Count = 999, Username = "hacker" };
        var tamperedJson = JsonSerializer.Serialize(tamperedState, CamelCaseOptions);

        var wrapper = new PersistedStateWrapper
        {
            Version = 1,
            State = tamperedJson, // Tampered
            Signature = signature, // Original signature
            Size = tamperedJson.Length,
            Timestamp = DateTimeOffset.UtcNow
        };

        var wrapperJson = JsonSerializer.Serialize(wrapper, CamelCaseOptions);

        var mockProvider = new Mock<IPersistenceProvider>();
        mockProvider.Setup(p => p.LoadAsync("test-key")).ReturnsAsync(wrapperJson);

        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = signer.Key,
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Act
        var loadedState = await middleware.LoadStateAsync();

        // Assert
        loadedState.Should().BeNull(); // Should reject tampered state
    }

    [Fact]
    public void PersistenceOptions_DefaultValues_AreSecure()
    {
        // Arrange & Act
        var options = new PersistenceOptions<TestState>
        {
            Key = "test"
        };

        // Assert
        options.EnableIntegrityCheck.Should().BeTrue("integrity check should be enabled by default");
        options.FilterSensitiveData.Should().BeTrue("sensitive data filtering should be enabled by default");
        options.MaxStateSize.Should().Be(1_048_576, "size limit should be 1 MB by default");
    }

    [Fact]
    public void WithSecurePersistence_ConfiguresAllSecurityFeatures()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act
        var configuredBuilder = builder.WithSecurePersistence(
            mockProvider.Object,
            "test-key",
            maxSizeBytes: 2_097_152,
            signingKey: CreateTestSigningKey());

        // Assert
        configuredBuilder.Should().NotBeNull();
    }

    [Fact]
    public void WithSecurePersistence_WithoutSigningKey_ThrowsActionableException()
    {
        // Arrange - integrity checking without a stable key would silently
        // discard all persisted state on every reload, so it must fail fast.
        var mockProvider = new Mock<IPersistenceProvider>();
        var builder = StoreBuilder<TestState>.Create(new TestState { Count = 0 });

        // Act
        var act = () => builder.WithSecurePersistence(mockProvider.Object, "test-key");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKey*")
            .WithMessage("*EnableIntegrityCheck*");
    }

    [Fact]
    public void PersistenceMiddleware_IntegrityCheckWithoutKey_ThrowsAtConstruction()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = null
        };

        // Act
        var act = () => new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*stable signing key*");
    }

    [Fact]
    public void PersistenceMiddleware_Dispose_ReleasesResourcesWithoutThrowing()
    {
        // Arrange
        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = CreateTestSigningKey(),
            DebounceMs = 100
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);

        // Act - dispose must release the signer and debounce CTS; double-dispose is safe
        var act = () =>
        {
            middleware.Dispose();
            middleware.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void PersistenceOptionsBuilder_FluentAPI_ConfiguresCorrectly()
    {
        // Arrange & Act
        var builder = new PersistenceOptionsBuilder<TestState>("test-key")
            .WithIntegrityCheck()
            .WithSizeLimit(2_097_152)
            .WithSensitiveDataFiltering()
            .WithDebounce(500);

        var options = builder.Build();

        // Assert
        options.Key.Should().Be("test-key");
        options.EnableIntegrityCheck.Should().BeTrue();
        options.MaxStateSize.Should().Be(2_097_152);
        options.FilterSensitiveData.Should().BeTrue();
        options.DebounceMs.Should().Be(500);
    }

    [Fact]
    public void StateSizeExceededException_ContainsCorrectInformation()
    {
        // Arrange
        var actualSize = 2_000_000;
        var maxSize = 1_000_000;

        // Act
        var exception = new StateSizeExceededException(actualSize, maxSize);

        // Assert
        exception.ActualSize.Should().Be(actualSize);
        exception.MaxSize.Should().Be(maxSize);
        exception.Message.Should().Contain("2,000,000");
        exception.Message.Should().Contain("1,000,000");
    }

    [Fact]
    public void StateIntegrityException_HasAppropriateMessage()
    {
        // Act
        var exception = new StateIntegrityException();

        // Assert
        exception.Message.Should().Contain("integrity");
        exception.Message.Should().Contain("tampered");
    }

    [Fact]
    public async Task SaveStateAsync_WithCustomSigningKey_UsesProvidedKey()
    {
        // Arrange
        var customKey = new byte[32];
        for (int i = 0; i < 32; i++)
            customKey[i] = (byte)i;

        var mockProvider = new Mock<IPersistenceProvider>();
        var options = new PersistenceOptions<TestState>
        {
            Key = "test-key",
            EnableIntegrityCheck = true,
            SigningKey = customKey,
            FilterSensitiveData = false
        };

        var middleware = new PersistenceMiddleware<TestState>(mockProvider.Object, options);
        var state = new TestState { Count = 42 };

        // Act
        await middleware.OnAfterUpdateAsync(state, state, "TEST");

        // Assert - verify that signature was created
        mockProvider.Verify(p => p.SaveAsync(
            "test-key",
            It.Is<string>(json => json.Contains("\"signature\":"))
        ), Times.Once);
    }

    [Fact]
    public void PersistedStateWrapper_HasCorrectDefaultVersion()
    {
        // Act
        var wrapper = new PersistedStateWrapper
        {
            State = "test",
            Size = 4
        };

        // Assert
        wrapper.Version.Should().Be(1);
        wrapper.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}
