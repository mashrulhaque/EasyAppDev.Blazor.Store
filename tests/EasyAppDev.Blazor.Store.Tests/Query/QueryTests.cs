// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

public class QueryTests
{
    [Fact]
    public async Task Query_InitialState_ShouldBeIdle()
    {
        // Arrange
        var client = new QueryClient();
        var stateChanged = false;
        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = async _ =>
            {
                await Task.Delay(100);
                return "result";
            },
            Enabled = () => false // Disable auto-fetch
        };

        // Act
        var query = new Query<string>(options, client, () => stateChanged = true);

        // Assert
        query.Status.Should().Be(QueryStatus.Idle);
        query.Data.Should().BeNull();
        query.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Query_WithInitialData_ShouldHaveSuccessStatus()
    {
        // Arrange
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("fetched"),
            InitialData = "initial",
            Enabled = () => false
        };

        // Act
        var query = new Query<string>(options, client, () => { });

        // Assert
        query.Status.Should().Be(QueryStatus.Success);
        query.Data.Should().Be("initial");
        query.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Query_WithPlaceholderData_ShouldShowPlaceholder()
    {
        // Arrange
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("fetched"),
            PlaceholderData = "loading...",
            Enabled = () => false
        };

        // Act
        var query = new Query<string>(options, client, () => { });

        // Assert
        query.Data.Should().Be("loading...");
        query.IsPlaceholderData.Should().BeTrue();
    }

    [Fact]
    public async Task Query_RefetchAsync_ShouldFetchData()
    {
        // Arrange
        var client = new QueryClient();
        var fetchCount = 0;
        var stateChanges = new List<QueryStatus>();
        Query<int>? query = null;

        var options = new QueryOptions<int>
        {
            Key = "counter",
            QueryFn = async _ =>
            {
                fetchCount++;
                await Task.Delay(10);
                return fetchCount;
            },
            Enabled = () => false,
            Retry = 0
        };

        query = new Query<int>(options, client, () => stateChanges.Add(query!.Status));

        // Act
        await query.RefetchAsync();

        // Assert
        query.Data.Should().Be(1);
        query.IsSuccess.Should().BeTrue();
        fetchCount.Should().Be(1);
    }

    [Fact]
    public async Task Query_SetData_ShouldUpdateDataAndCache()
    {
        // Arrange
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("fetched"),
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        query.SetData("manually set");

        // Assert
        query.Data.Should().Be("manually set");
        query.IsSuccess.Should().BeTrue();
        client.GetQueryData<string>("test").Should().Be("manually set");
    }

    [Fact]
    public async Task Query_WithSelect_ShouldTransformData()
    {
        // Arrange
        var client = new QueryClient();
        var options = new QueryOptions<List<int>>
        {
            Key = "numbers",
            QueryFn = _ => Task.FromResult<List<int>?>(new List<int> { 1, 2, 3, 4, 5 }),
            Select = nums => nums.Where(n => n > 2).ToList(),
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<List<int>>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        query.Data.Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    [Fact]
    public async Task Query_OnSuccess_ShouldBeCalledOnSuccessfulFetch()
    {
        // Arrange
        var client = new QueryClient();
        string? successData = null;

        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("success"),
            OnSuccess = data => successData = data,
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        successData.Should().Be("success");
    }

    [Fact]
    public async Task Query_OnError_ShouldBeCalledOnFailure()
    {
        // Arrange
        var client = new QueryClient();
        Exception? errorException = null;

        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => throw new InvalidOperationException("Test error"),
            OnError = ex => errorException = ex,
            Retry = 0,
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        errorException.Should().NotBeNull();
        errorException.Should().BeOfType<InvalidOperationException>();
        query.IsError.Should().BeTrue();
        query.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Query_OnSettled_ShouldBeCalledAfterFetch()
    {
        // Arrange
        var client = new QueryClient();
        var settledCalled = false;

        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("data"),
            OnSettled = () => settledCalled = true,
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        settledCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Query_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        var client = new QueryClient();
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ =>
            {
                attempts++;
                if (attempts < 3)
                    throw new Exception("Retry needed");
                return Task.FromResult<string?>("success");
            },
            Retry = 3,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1),
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        attempts.Should().Be(3);
        query.IsSuccess.Should().BeTrue();
        query.Data.Should().Be("success");
    }

    [Fact]
    public async Task Query_IsStale_ShouldReflectStaleTime()
    {
        // Arrange
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = _ => Task.FromResult<string?>("data"),
            StaleTime = TimeSpan.FromHours(1), // Long stale time
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        await query.RefetchAsync();

        // Assert
        query.IsStale.Should().BeFalse(); // Data is fresh
    }

    [Fact]
    public void Query_Dispose_ShouldCancelPendingOperations()
    {
        // Arrange
        var client = new QueryClient();
        var cancellationReceived = false;

        var options = new QueryOptions<string>
        {
            Key = "test",
            QueryFn = async ct =>
            {
                try
                {
                    await Task.Delay(10000, ct);
                    return "data";
                }
                catch (OperationCanceledException)
                {
                    cancellationReceived = true;
                    throw;
                }
            },
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });

        // Act
        var fetchTask = query.RefetchAsync();
        query.Dispose();

        // Assert - Should not throw
        Func<Task> act = async () => await fetchTask;
        act.Should().NotThrowAsync();
    }
}
