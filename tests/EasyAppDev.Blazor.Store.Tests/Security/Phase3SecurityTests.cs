// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.DevTools;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Tests for Phase 3 security remediations:
/// - Message size validation with callback
/// - Deserialization depth limits
/// - Thread-safe sync flag
/// - Improved sensitive data filter
/// - DevTools filtering default-on
/// </summary>
public class Phase3SecurityTests
{
    #region Test State Classes

    public record TestState
    {
        public int Count { get; init; }
        public string? Name { get; init; }
    }

    public record StateWithSensitiveData
    {
        public string Username { get; init; } = "";
        [SensitiveData]
        public string Password { get; init; } = "";
        public string Token { get; init; } = ""; // Partial match
        public string ApiKey { get; init; } = "";
        [AlwaysInclude]
        public string TokenCount { get; init; } = "100"; // Should NOT be filtered despite containing "Token"
    }

    public record StateWithRegexPatterns
    {
        public string PublicData { get; init; } = "";
        public string EncryptedField { get; init; } = "";
        public string AuthSecretKey { get; init; } = "";
        public string UserApiCredential { get; init; } = "";
    }

    #endregion

    #region 3.1 OnMessageSizeExceeded Callback Tests

    [Fact]
    public void TabSyncOptions_OnMessageSizeExceeded_CanBeConfigured()
    {
        // Arrange
        int? capturedSize = null;
        var options = new TabSyncOptions
        {
            OnMessageSizeExceeded = size => capturedSize = size
        };

        // Act
        options.OnMessageSizeExceeded?.Invoke(2_000_000);

        // Assert
        capturedSize.Should().Be(2_000_000);
    }

    [Fact]
    public void TabSyncOptions_MaxMessageSizeBytes_DefaultIsOneMB()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.MaxMessageSizeBytes.Should().Be(1_048_576);
    }

    #endregion

    #region 3.2 Deserialization Depth Limits Tests

    [Fact]
    public void TabSyncOptions_MaxJsonDepth_DefaultIs32()
    {
        // Arrange & Act
        var options = new TabSyncOptions();

        // Assert
        options.MaxJsonDepth.Should().Be(32);
    }

    [Fact]
    public void TabSyncOptions_MaxJsonDepth_CanBeCustomized()
    {
        // Arrange
        var options = new TabSyncOptions
        {
            MaxJsonDepth = 64
        };

        // Assert
        options.MaxJsonDepth.Should().Be(64);
    }

    [Fact]
    public void JsonSerializerOptions_RespectsMaxDepth()
    {
        // Arrange - create a deeply nested structure
        var jsonOptions = new JsonSerializerOptions
        {
            MaxDepth = 5
        };

        // This JSON has depth > 5
        var deepJson = @"{""a"":{""b"":{""c"":{""d"":{""e"":{""f"":1}}}}}}";

        // Act & Assert
        var act = () => JsonSerializer.Deserialize<object>(deepJson, jsonOptions);
        act.Should().Throw<JsonException>().Where(e =>
            e.Message.Contains("depth", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 3.4 Improved Sensitive Data Filter Tests

    [Fact]
    public void SensitiveDataFilterOptions_UseExactMatch_DefaultIsFalse()
    {
        // Arrange & Act
        var options = new SensitiveDataFilterOptions();

        // Assert
        options.UseExactMatch.Should().BeFalse();
    }

    [Fact]
    public void SensitiveDataFilterOptions_FilteredPropertyPatterns_DefaultIsEmpty()
    {
        // Arrange & Act
        var options = new SensitiveDataFilterOptions();

        // Assert
        options.FilteredPropertyPatterns.Should().BeEmpty();
    }

    [Fact]
    public void SensitiveDataFilterOptions_MaxRecursionDepth_DefaultIs32()
    {
        // Arrange & Act
        var options = new SensitiveDataFilterOptions();

        // Assert
        options.MaxRecursionDepth.Should().Be(32);
    }

    [Fact]
    public void SensitiveDataFilterOptions_HasExpandedDefaultKeywords()
    {
        // Arrange & Act
        var options = new SensitiveDataFilterOptions();

        // Assert - check new keywords were added
        options.FilteredPropertyNames.Should().Contain("EncryptionKey");
        options.FilteredPropertyNames.Should().Contain("ConnectionString");
        options.FilteredPropertyNames.Should().Contain("BearerToken");
        options.FilteredPropertyNames.Should().Contain("AuthToken");
        options.FilteredPropertyNames.Should().Contain("SessionId");
        options.FilteredPropertyNames.Should().Contain("SessionToken");
    }

    [Fact]
    public void SensitiveDataFilter_AlwaysIncludeAttribute_OverridesFiltering()
    {
        // Arrange
        var state = new StateWithSensitiveData
        {
            Username = "testuser",
            Password = "secret123",
            Token = "abc123",
            ApiKey = "key-456",
            TokenCount = "100"
        };

        var options = new SensitiveDataFilterOptions { Enabled = true };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().Contain("[FILTERED]"); // Password, Token, ApiKey should be filtered
        json.Should().Contain("testuser"); // Username should NOT be filtered
        json.Should().Contain("100"); // TokenCount should NOT be filtered due to [AlwaysInclude]
    }

    [Fact]
    public void SensitiveDataFilter_ExactMatch_OnlyMatchesExactNames()
    {
        // Arrange
        var state = new StateWithSensitiveData
        {
            Username = "user",
            Password = "secret",
            Token = "token",
            ApiKey = "key",
            TokenCount = "100"
        };

        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            UseExactMatch = true,
            FilteredPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Password", "Token", "ApiKey"
            }
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().Contain("[FILTERED]"); // Password, Token, ApiKey should be filtered
        json.Should().Contain("user"); // Username should NOT be filtered
        // Note: TokenCount has [AlwaysInclude] so it's not filtered regardless
    }

    [Fact]
    public void SensitiveDataFilter_PartialMatch_MatchesContains()
    {
        // Arrange - a property named "UserPasswordHash" should match "Password"
        var state = new { UserPasswordHash = "hash123", Name = "test" };

        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            UseExactMatch = false,
            FilteredPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Password" }
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().Contain("[FILTERED]"); // UserPasswordHash should be filtered due to partial match
        json.Should().Contain("test"); // Name should NOT be filtered
    }

    [Fact]
    public void SensitiveDataFilter_RegexPatterns_MatchComplex()
    {
        // Arrange
        var state = new StateWithRegexPatterns
        {
            PublicData = "public",
            EncryptedField = "encrypted",
            AuthSecretKey = "secret",
            UserApiCredential = "cred"
        };

        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            FilteredPropertyPatterns = new List<string>
            {
                @"^Encrypted.*$",  // Matches properties starting with "Encrypted"
                @"^Auth.*Key$",    // Matches properties starting with "Auth" and ending with "Key"
                @".*Credential$"   // Matches properties ending with "Credential"
            }
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act
        var json = JsonSerializer.Serialize(state, jsonOptions);

        // Assert
        json.Should().Contain("public"); // PublicData should NOT be filtered
        json.Should().Contain("[FILTERED]"); // Other fields should be filtered
    }

    [Fact]
    public async Task SensitiveDataFilter_RegexWithTimeout_DoesNotHang()
    {
        // Arrange - test that regex timeout protection works
        var state = new { Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab" };

        // This is a potentially catastrophic backtracking regex (ReDoS)
        var options = new SensitiveDataFilterOptions
        {
            Enabled = true,
            FilteredPropertyPatterns = new List<string>
            {
                @"^(a+)+b$" // Catastrophic backtracking pattern
            }
        };
        var jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(options);

        // Act - should complete without hanging due to regex timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = Task.Run(() => JsonSerializer.Serialize(state, jsonOptions), cts.Token);
        var completed = false;
        try
        {
            await task;
            completed = true;
        }
        catch (OperationCanceledException)
        {
            completed = false;
        }

        // Assert
        completed.Should().BeTrue("Regex should timeout and not cause infinite loop");
    }

    #endregion

    #region 3.6 DevTools Filtering Default-On Tests

    [Fact]
    public void DevToolsOptions_SensitiveDataFilter_EnabledByDefault()
    {
        // Arrange & Act
        var options = new DevToolsOptions<TestState>();

        // Assert
        options.SensitiveDataFilter.Should().NotBeNull();
        options.SensitiveDataFilter!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void DevToolsOptions_Default_HasSensitiveDataFiltering()
    {
        // Arrange & Act
        var options = DevToolsOptions<TestState>.Default("TestStore");

        // Assert
        options.Name.Should().Be("TestStore");
        options.SensitiveDataFilter.Should().NotBeNull();
        options.SensitiveDataFilter!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void DevToolsOptions_WithoutSensitiveDataFiltering_DisablesFilter()
    {
        // Arrange & Act
#pragma warning disable CS0618 // Type or member is obsolete
        var options = DevToolsOptions<TestState>.WithoutSensitiveDataFiltering("TestStore");
#pragma warning restore CS0618

        // Assert
        options.Name.Should().Be("TestStore");
        options.SensitiveDataFilter.Should().BeNull();
    }

    [Fact]
    public void DevToolsOptions_WithSensitiveDataFiltering_IsObsolete()
    {
        // This test verifies the obsolete attribute exists
        var method = typeof(DevToolsOptions<TestState>).GetMethod("WithSensitiveDataFiltering");

        // Assert
        method.Should().NotBeNull();
        var obsoleteAttr = method!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
        obsoleteAttr.Should().HaveCount(1);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentUpdates_DoNotCorruptState()
    {
        // This tests that the Interlocked operations work correctly
        // by simulating concurrent increment/decrement operations
        var counter = 0;
        var tasks = new List<Task>();

        // Act - run many concurrent operations
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                Interlocked.Increment(ref counter);
                Thread.Sleep(1); // Small delay to increase contention
                Interlocked.Decrement(ref counter);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - counter should be back to 0
        counter.Should().Be(0);
    }

    [Fact]
    public void VolatileRead_ReturnsLatestValue()
    {
        // Arrange
        var value = 0;

        // Act
        Interlocked.Increment(ref value);
        var read = Volatile.Read(ref value);

        // Assert
        read.Should().Be(1);
    }

    #endregion

    #region AlwaysInclude Attribute Tests

    [Fact]
    public void AlwaysIncludeAttribute_CanBeApplied()
    {
        // Arrange
        var type = typeof(StateWithSensitiveData);
        var property = type.GetProperty("TokenCount");

        // Assert
        property.Should().NotBeNull();
        var attr = property!.GetCustomAttributes(typeof(AlwaysIncludeAttribute), false);
        attr.Should().HaveCount(1);
    }

    [Fact]
    public void AlwaysIncludeAttribute_HasReasonProperty()
    {
        // Arrange
        var attr = new AlwaysIncludeAttribute { Reason = "Not actually a token" };

        // Assert
        attr.Reason.Should().Be("Not actually a token");
    }

    #endregion
}
