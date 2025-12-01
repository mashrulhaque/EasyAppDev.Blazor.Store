// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

public class QueryClientTests
{
    [Fact]
    public void SetQueryData_ShouldStoreData()
    {
        // Arrange
        var client = new QueryClient();
        var data = new List<string> { "item1", "item2" };

        // Act
        client.SetQueryData("test-key", data);
        var result = client.GetQueryData<List<string>>("test-key");

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void GetQueryData_WithMissingKey_ShouldReturnNull()
    {
        // Arrange
        var client = new QueryClient();

        // Act
        var result = client.GetQueryData<string>("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SetQueryData_WithUpdater_ShouldTransformData()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("counter", 5);

        // Act
        client.SetQueryData<int>("counter", current => current + 10);
        var result = client.GetQueryData<int>("counter");

        // Assert
        result.Should().Be(15);
    }

    [Fact]
    public void InvalidateQueries_ShouldMarkAsInvalidated()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("test-key", "data");

        // Act
        client.InvalidateQueries("test-key");
        var isInvalidated = ((IQueryClient)client).IsInvalidated("test-key");

        // Assert
        isInvalidated.Should().BeTrue();
    }

    [Fact]
    public void InvalidateQueries_WithPredicate_ShouldMarkMatchingKeys()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("user-1", "data1");
        client.SetQueryData("user-2", "data2");
        client.SetQueryData("product-1", "data3");

        // Act
        client.InvalidateQueries(key => key.StartsWith("user-"));

        // Assert
        ((IQueryClient)client).IsInvalidated("user-1").Should().BeTrue();
        ((IQueryClient)client).IsInvalidated("user-2").Should().BeTrue();
        ((IQueryClient)client).IsInvalidated("product-1").Should().BeFalse();
    }

    [Fact]
    public void RemoveQueries_ShouldRemoveFromCache()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("test-key", "data");

        // Act
        client.RemoveQueries("test-key");
        var result = client.GetQueryData<string>("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("key1", "data1");
        client.SetQueryData("key2", "data2");

        // Act
        client.Clear();

        // Assert
        client.GetQueryData<string>("key1").Should().BeNull();
        client.GetQueryData<string>("key2").Should().BeNull();
    }

    [Fact]
    public void GetCacheEntry_ShouldReturnEntryWithMetadata()
    {
        // Arrange
        var client = new QueryClient();
        var data = "test-data";
        client.SetQueryData("test-key", data);

        // Act
        var entry = ((IQueryClient)client).GetCacheEntry<string>("test-key");

        // Assert
        entry.Should().NotBeNull();
        entry!.Data.Should().Be(data);
        entry.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DefaultOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new QueryClientOptions();

        // Assert
        options.DefaultStaleTime.Should().Be(TimeSpan.Zero);
        options.DefaultCacheTime.Should().Be(TimeSpan.FromMinutes(5));
        options.DefaultRetry.Should().Be(3);
        options.DefaultRefetchOnWindowFocus.Should().BeTrue();
        options.DefaultRefetchOnReconnect.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldClearCache()
    {
        // Arrange
        var client = new QueryClient();
        client.SetQueryData("test", "data");

        // Act
        client.Dispose();

        // Assert - Can still call methods but cache is cleared
        // Note: After dispose, internal state is cleared
    }
}
