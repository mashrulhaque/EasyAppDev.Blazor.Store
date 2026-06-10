// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

/// <summary>
/// Regression tests for QueryComponent lifecycle fixes:
/// non-blocking dispose and safe initialization when queries are added
/// while initialization is in progress.
/// </summary>
public class QueryComponentTests
{
    private sealed class TestComponent : QueryComponent
    {
        public TestComponent(IQueryClient client)
        {
            QueryClient = client;
        }

        public Query<T> AddQuery<T>(QueryOptions<T> options) => UseQuery(options);

        public Task RunInitAsync() => OnInitializedAsync();
    }

    [Fact]
    public async Task OnInitializedAsync_QueryAddedDuringInitialization_ShouldAlsoBeInitialized()
    {
        var client = new QueryClient();
        var component = new TestComponent(client);
        var lateFetchCount = 0;

        var lateOptions = new QueryOptions<string>
        {
            Key = "late-query",
            QueryFn = _ =>
            {
                Interlocked.Increment(ref lateFetchCount);
                return Task.FromResult<string?>("late");
            },
            Retry = 0
        };

        var firstOptions = new QueryOptions<string>
        {
            Key = "first-query",
            QueryFn = _ =>
            {
                // Simulates UseQuery being called while OnInitializedAsync is
                // enumerating _disposables (e.g. from a render during an await).
                component.AddQuery(lateOptions);
                return Task.FromResult<string?>("first");
            },
            Retry = 0
        };

        component.AddQuery(firstOptions);

        // Old code threw InvalidOperationException (collection modified during foreach)
        await component.RunInitAsync();

        lateFetchCount.Should().Be(1, "queries added during initialization must also be initialized");
    }

    [Fact]
    public async Task OnInitializedAsync_WhenAnInitThrows_ComponentShouldStillBecomeInitialized()
    {
        var client = new QueryClient();
        var component = new TestComponent(client);

        var throwingOptions = new QueryOptions<string>
        {
            Key = "throwing-query",
            QueryFn = _ => Task.FromResult<string?>("data"),
            // Enabled() throws inside InitializeAsync, escaping the init loop
            Enabled = () => throw new InvalidOperationException("enabled bug"),
            Retry = 0
        };

        component.AddQuery(throwingOptions);

        await Assert.ThrowsAsync<InvalidOperationException>(() => component.RunInitAsync());

        // The component must not be permanently broken: a later UseQuery should
        // still get its query initialized (proving _initialized was set).
        var lateFetchCount = 0;
        component.AddQuery(new QueryOptions<string>
        {
            Key = "after-failure",
            QueryFn = _ =>
            {
                Interlocked.Increment(ref lateFetchCount);
                return Task.FromResult<string?>("ok");
            },
            Retry = 0
        });

        var sw = Stopwatch.StartNew();
        while (Volatile.Read(ref lateFetchCount) == 0 && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        lateFetchCount.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_WithPendingInitialization_ShouldNotBlock()
    {
        var client = new QueryClient();
        var component = new TestComponent(client);

        await component.RunInitAsync();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Pending initialization that never completes (ignores cancellation)
        component.AddQuery(new QueryOptions<string>
        {
            Key = "never-completes",
            QueryFn = _ =>
            {
                started.TrySetResult();
                return never.Task;
            },
            Retry = 0
        });

        await started.Task;

        var sw = Stopwatch.StartNew();
        component.Dispose();
        sw.Stop();

        // Old code blocked for up to 1 second (Task.WhenAll(...).Wait(1s)),
        // freezing the Blazor Server dispatcher.
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(800),
            "Dispose must not synchronously wait on pending initializations");
    }
}
