// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.Security;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Security;

/// <summary>
/// Tests that the sensitive data filter produces JSON that always round-trips
/// back into the original state type (dictionaries as objects, type-aware
/// replacement values) and that keyword matching respects token boundaries.
/// </summary>
public class SensitiveDataFilterRoundTripTests
{
    public record StateWithDictionary
    {
        public Dictionary<string, int> Scores { get; init; } = new();
        public string? Name { get; init; }
    }

    public record StateWithNonStringSensitiveProps
    {
        public int Pin { get; init; }
        public string? Password { get; init; }
        public string? ShippingAddress { get; init; }
        public int TokenCount { get; init; }
        public string? Username { get; init; }
    }

    #region Dictionary serialization (fix: dictionaries were written as JSON arrays)

    [Fact]
    public void Filter_SerializesDictionaryAsJsonObject_NotArray()
    {
        // Arrange
        var state = new StateWithDictionary
        {
            Scores = new Dictionary<string, int> { ["alice"] = 3, ["bob"] = 7 },
            Name = "test"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("scores").ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("scores").GetProperty("alice").GetInt32().Should().Be(3);
        doc.RootElement.GetProperty("scores").GetProperty("bob").GetInt32().Should().Be(7);
    }

    [Fact]
    public void Filter_DictionaryState_RoundTripsThroughSerialization()
    {
        // Arrange
        var state = new StateWithDictionary
        {
            Scores = new Dictionary<string, int> { ["alice"] = 3, ["bob"] = 7 },
            Name = "round-trip"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);
        var restored = JsonSerializer.Deserialize<StateWithDictionary>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        restored.Should().NotBeNull();
        restored!.Name.Should().Be("round-trip");
        restored.Scores.Should().HaveCount(2);
        restored.Scores["alice"].Should().Be(3);
        restored.Scores["bob"].Should().Be(7);
    }

    [Fact]
    public void Filter_DictionaryWithSensitiveStringKey_ReplacesValue()
    {
        // Arrange
        var state = new StateWithStringDictionary
        {
            Settings = new Dictionary<string, string>
            {
                ["password"] = "secret123",
                ["theme"] = "dark"
            }
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        json.Should().NotContain("secret123");
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("settings").GetProperty("password").GetString().Should().Be("[FILTERED]");
        doc.RootElement.GetProperty("settings").GetProperty("theme").GetString().Should().Be("dark");
    }

    public record StateWithStringDictionary
    {
        public Dictionary<string, string> Settings { get; init; } = new();
    }

    [Fact]
    public async Task PersistenceMiddleware_StateWithDictionary_SurvivesSaveThenLoad()
    {
        // Arrange - full save -> load round trip through the persistence middleware
        var provider = new InMemoryPersistenceProvider();
        var options = new PersistenceOptions<StateWithDictionary>
        {
            Key = "dict-state",
            EnableIntegrityCheck = false,
            FilterSensitiveData = true
        };

        var middleware = new PersistenceMiddleware<StateWithDictionary>(provider, options);
        var state = new StateWithDictionary
        {
            Scores = new Dictionary<string, int> { ["alice"] = 3, ["bob"] = 7 },
            Name = "persisted"
        };

        // Act
        await middleware.OnAfterUpdateAsync(state, state, "SAVE");
        var loaded = await middleware.LoadStateAsync();

        // Assert - hydration must not fail and the dictionary must survive intact
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("persisted");
        loaded.Scores.Should().HaveCount(2);
        loaded.Scores["alice"].Should().Be(3);
        loaded.Scores["bob"].Should().Be(7);
    }

    #endregion

    #region Type-aware replacement (fix: "[FILTERED]" was written into non-string properties)

    [Fact]
    public void Filter_IntPropertyNamedPin_IsFilteredToDefaultZero_AndRoundTrips()
    {
        // Arrange
        var state = new StateWithNonStringSensitiveProps
        {
            Pin = 1234,
            Password = "secret",
            Username = "john"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);
        var restored = JsonSerializer.Deserialize<StateWithNonStringSensitiveProps>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - the int Pin is replaced with its type default (0), not "[FILTERED]",
        // so deserialization never fails
        restored.Should().NotBeNull();
        restored!.Pin.Should().Be(0);
        restored.Password.Should().Be("[FILTERED]");
        restored.Username.Should().Be("john");
        json.Should().NotContain("1234");
    }

    [Fact]
    public async Task PersistenceMiddleware_StateWithIntPin_SurvivesSaveThenLoad()
    {
        // Arrange
        var provider = new InMemoryPersistenceProvider();
        var options = new PersistenceOptions<StateWithNonStringSensitiveProps>
        {
            Key = "pin-state",
            EnableIntegrityCheck = false,
            FilterSensitiveData = true
        };

        var middleware = new PersistenceMiddleware<StateWithNonStringSensitiveProps>(provider, options);
        var state = new StateWithNonStringSensitiveProps
        {
            Pin = 9876,
            Username = "jane",
            ShippingAddress = "42 Main Street"
        };

        // Act
        await middleware.OnAfterUpdateAsync(state, state, "SAVE");
        var loaded = await middleware.LoadStateAsync();

        // Assert - previously the entire state was lost because "[FILTERED]"
        // could not deserialize into the int Pin property
        loaded.Should().NotBeNull();
        loaded!.Username.Should().Be("jane");
        loaded.Pin.Should().Be(0);
        loaded.ShippingAddress.Should().Be("42 Main Street");
    }

    #endregion

    #region Token-boundary matching (fix: substring matching corrupted innocent properties)

    [Fact]
    public void Filter_ShippingAddress_IsNotFiltered()
    {
        // Arrange - "ShippingAddress" contains "pin" as a raw substring ("shipPINg"),
        // but must NOT match the "Pin" keyword with token-boundary matching
        var state = new StateWithNonStringSensitiveProps
        {
            ShippingAddress = "42 Main Street",
            Username = "john"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("shippingAddress").GetString().Should().Be("42 Main Street");
    }

    [Fact]
    public void Filter_PinAndUserPin_AreFiltered()
    {
        // Arrange
        var state = new { Pin = "1234", UserPin = "5678", Address = "home" };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("pin").GetString().Should().Be("[FILTERED]");
        doc.RootElement.GetProperty("userPin").GetString().Should().Be("[FILTERED]");
        doc.RootElement.GetProperty("address").GetString().Should().Be("home");
    }

    [Fact]
    public void Filter_CardNumber_MatchesConsecutiveTokenSequences()
    {
        // Arrange - multi-token keyword "CardNumber" must match consecutive tokens
        var state = new { CardNumber = "4111", CreditCardNumber = "4242", CardHolder = "john" };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("cardNumber").GetString().Should().Be("[FILTERED]");
        doc.RootElement.GetProperty("creditCardNumber").GetString().Should().Be("[FILTERED]");
        doc.RootElement.GetProperty("cardHolder").GetString().Should().Be("john");
    }

    [Fact]
    public void Filter_TokenCount_IsFilteredConservatively()
    {
        // Deliberate decision: "TokenCount" splits into the tokens ["Token", "Count"]
        // and the whole token "Token" matches the sensitive keyword "Token", so the
        // property IS filtered. This is conservative by design - a token-named
        // property is more likely to be sensitive than not. Properties like this
        // can opt out with [AlwaysInclude].
        var state = new StateWithNonStringSensitiveProps
        {
            TokenCount = 5,
            Username = "john"
        };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);
        var restored = JsonSerializer.Deserialize<StateWithNonStringSensitiveProps>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - filtered to the int default so it still round-trips
        restored.Should().NotBeNull();
        restored!.TokenCount.Should().Be(0);
        restored.Username.Should().Be("john");
    }

    [Fact]
    public void Filter_SnakeCaseNames_MatchOnTokenBoundaries()
    {
        // Arrange
        var state = new { user_password = "secret", shipping_address = "42 Main Street" };

        // Act
        var json = SensitiveDataFilterExtensions.SerializeFiltered(state);

        // Assert
        json.Should().NotContain("secret");
        json.Should().Contain("42 Main Street");
    }

    #endregion
}
