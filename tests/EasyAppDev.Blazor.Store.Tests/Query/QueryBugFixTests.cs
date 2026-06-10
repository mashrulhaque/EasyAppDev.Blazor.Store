// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

/// <summary>
/// Regression tests for query system caching, cancellation, lifecycle,
/// and registration bug fixes.
/// </summary>
public class QueryBugFixTests
{
    private static TaskCompletionSource NewTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // ---------------------------------------------------------------
    // Fix 1: IQueryClient must be registered as scoped (per-circuit on Server)
    // ---------------------------------------------------------------

    [Fact]
    public void AddQueryClient_ShouldRegisterAsScoped()
    {
        var services = new ServiceCollection();
        services.AddQueryClient();

        var descriptor = services.Single(d => d.ServiceType == typeof(IQueryClient));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddQueryClient_WithOptionsInstance_ShouldRegisterAsScoped()
    {
        var services = new ServiceCollection();
        services.AddQueryClient(new QueryClientOptions());

        var descriptor = services.Single(d => d.ServiceType == typeof(IQueryClient));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // ---------------------------------------------------------------
    // Fix 2: a superseded fetch must not overwrite fresh data
    // ---------------------------------------------------------------

    [Fact]
    public async Task Query_SupersededFetch_ShouldNotOverwriteFreshData()
    {
        var client = new QueryClient();
        var firstStarted = NewTcs();
        var firstRelease = NewTcs();
        var calls = 0;

        var options = new QueryOptions<string>
        {
            Key = "supersede",
            QueryFn = async _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstStarted.TrySetResult();
                    await firstRelease.Task;
                    return "stale";
                }

                return "fresh";
            },
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });

        var firstFetch = query.RefetchAsync();
        await firstStarted.Task;

        // Force refetch supersedes the in-flight fetch
        await query.RefetchAsync();
        query.Data.Should().Be("fresh");

        // Let the stale fetch complete - it must NOT commit its result
        firstRelease.TrySetResult();
        await firstFetch;

        query.Data.Should().Be("fresh");
        client.GetQueryData<string>("supersede").Should().Be("fresh");
        query.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Query_CancelledFetch_ShouldNotBeRetriedAsFailure()
    {
        var client = new QueryClient();
        var firstStarted = NewTcs();
        var calls = 0;

        var options = new QueryOptions<string>
        {
            Key = "cancelled-retry",
            QueryFn = async ct =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstStarted.TrySetResult();
                    await Task.Delay(10_000, ct); // cancelled by the superseding refetch
                    return "stale";
                }

                return "fresh";
            },
            Enabled = () => false,
            Retry = 3,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var query = new Query<string>(options, client, () => { });

        var firstFetch = query.RefetchAsync();
        await firstStarted.Task;

        await query.RefetchAsync();
        await firstFetch;

        // The cancelled first fetch must exit silently - no retries, no error
        calls.Should().Be(2);
        query.IsSuccess.Should().BeTrue();
        query.Data.Should().Be("fresh");
        query.FailureCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Fix 4: per-fetch CTS is disposed and the field cleared after the fetch
    // ---------------------------------------------------------------

    [Fact]
    public async Task Query_AfterFetchCompletes_FetchCtsFieldShouldBeCleared()
    {
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "cts-cleanup",
            QueryFn = _ => Task.FromResult<string?>("data"),
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        var field = typeof(Query<string>).GetField("_fetchCts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.GetValue(query).Should().BeNull("each fetch disposes its own CTS and clears the field");
        query.IsFetching.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Fix 5: callback exceptions must not re-run a successful fetch
    // ---------------------------------------------------------------

    [Fact]
    public async Task Query_OnSuccessThrows_ShouldNotRetryOrEnterErrorState()
    {
        var client = new QueryClient();
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "onsuccess-throws",
            QueryFn = _ =>
            {
                attempts++;
                return Task.FromResult<string?>("data");
            },
            OnSuccess = _ => throw new InvalidOperationException("callback bug"),
            Enabled = () => false,
            Retry = 3,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        attempts.Should().Be(1, "a successful fetch must not be re-run because a callback threw");
        query.IsSuccess.Should().BeTrue();
        query.Data.Should().Be("data");
        client.GetQueryData<string>("onsuccess-throws").Should().Be("data");
    }

    [Fact]
    public async Task Query_OnSettledThrows_ShouldNotAffectState()
    {
        var client = new QueryClient();
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "onsettled-throws",
            QueryFn = _ =>
            {
                attempts++;
                return Task.FromResult<string?>("data");
            },
            OnSettled = () => throw new InvalidOperationException("callback bug"),
            Enabled = () => false,
            Retry = 2,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        attempts.Should().Be(1);
        query.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Query_SelectThrows_ShouldKeepRawResultAndSucceed()
    {
        var client = new QueryClient();
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "select-throws",
            QueryFn = _ =>
            {
                attempts++;
                return Task.FromResult<string?>("raw");
            },
            Select = _ => throw new InvalidOperationException("select bug"),
            Enabled = () => false,
            Retry = 2,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        attempts.Should().Be(1);
        query.IsSuccess.Should().BeTrue();
        query.Data.Should().Be("raw");
    }

    // ---------------------------------------------------------------
    // Fix 6: predicate invalidation must include active-but-uncached queries
    // ---------------------------------------------------------------

    [Fact]
    public async Task InvalidateQueries_WithPredicate_ShouldIncludeActiveUncachedQueries()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        var options = new QueryOptions<int>
        {
            Key = "active-uncached",
            QueryFn = _ =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<int>(42);
            },
            Retry = 0
        };

        // Registered (active) query, but nothing in the cache for its key
        using var query = new Query<int>(options, client, () => { });
        client.GetQueryData<int>("active-uncached").Should().Be(0);

        client.InvalidateQueries(key => key.StartsWith("active-"));

        // The (fire-and-forget) refetch should reach the active query even though
        // its key has no cache entry. (Old code iterated only _cache.Keys, so the
        // query was never refetched.) Once the refetch completes it re-populates
        // the cache and clears the invalidation flag.
        var sw = Stopwatch.StartNew();
        while (Volatile.Read(ref fetchCount) == 0 && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        fetchCount.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------
    // Fix 7: invalidation must not fetch disabled queries
    // ---------------------------------------------------------------

    [Fact]
    public async Task InvalidateQueries_ShouldNotFetchDisabledQuery()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        var options = new QueryOptions<string>
        {
            Key = "disabled",
            QueryFn = _ =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<string?>("data");
            },
            Enabled = () => false,
            Retry = 0
        };

        using var query = new Query<string>(options, client, () => { });

        await client.InvalidateQueriesAsync("disabled");
        fetchCount.Should().Be(0, "invalidation must not fetch disabled queries");

        // Manual user-called refetch still forces a fetch
        await query.RefetchAsync();
        fetchCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Fix 10: superseded mutations must not overwrite the newer result
    // ---------------------------------------------------------------

    [Fact]
    public async Task Mutation_SupersededMutation_ShouldNotOverwriteNewerResult()
    {
        var client = new QueryClient();
        var firstStarted = NewTcs();
        var firstRelease = NewTcs();
        var calls = 0;
        var successResults = new List<string>();

        var options = new MutationOptions<string, string>
        {
            MutationFn = async (v, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstStarted.TrySetResult();
                    await firstRelease.Task;
                    return "first";
                }

                return "second";
            },
            OnSuccess = (result, _) => successResults.Add(result!)
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        var firstTask = mutation.MutateAsync("a");
        await firstStarted.Task;

        var secondResult = await mutation.MutateAsync("b");
        secondResult.Should().Be("second");

        firstRelease.TrySetResult();
        var firstResult = await firstTask;

        firstResult.Should().BeNull("the superseded mutation must not return its stale result");
        mutation.Data.Should().Be("second");
        mutation.IsSuccess.Should().BeTrue();
        successResults.Should().Equal("second");
    }

    [Fact]
    public async Task Mutation_CancelledMutation_ShouldNotBeRetried()
    {
        var client = new QueryClient();
        var firstStarted = NewTcs();
        var calls = 0;

        var options = new MutationOptions<string, string>
        {
            MutationFn = async (v, ct) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstStarted.TrySetResult();
                    await Task.Delay(10_000, ct);
                    return "first";
                }

                return "second";
            },
            Retry = 3,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        var firstTask = mutation.MutateAsync("a");
        await firstStarted.Task;

        await mutation.MutateAsync("b");
        await firstTask;

        calls.Should().Be(2, "the cancelled mutation must not be retried (duplicate server writes)");
        mutation.IsSuccess.Should().BeTrue();
        mutation.Data.Should().Be("second");
    }

    [Fact]
    public async Task VoidMutation_SupersededMutation_ShouldNotFireLateCallbacks()
    {
        var client = new QueryClient();
        var firstStarted = NewTcs();
        var firstRelease = NewTcs();
        var calls = 0;
        var successVariables = new List<string>();

        var options = new MutationOptions<string>
        {
            MutationFn = async (v, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstStarted.TrySetResult();
                    await firstRelease.Task;
                }
            },
            OnSuccess = v => successVariables.Add(v)
        };

        var mutation = new Mutation<string>(options, client, () => { });

        var firstTask = mutation.MutateAsync("a");
        await firstStarted.Task;

        await mutation.MutateAsync("b");

        firstRelease.TrySetResult();
        await firstTask;

        successVariables.Should().Equal("b");
        mutation.Variables.Should().Be("b");
        mutation.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Mutation_OnSuccessThrows_ShouldNotRetryOrFail()
    {
        var client = new QueryClient();
        var calls = 0;

        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) =>
            {
                calls++;
                return Task.FromResult<string?>(v.ToUpperInvariant());
            },
            OnSuccess = (_, _) => throw new InvalidOperationException("callback bug"),
            Retry = 2,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var mutation = new Mutation<string, string>(options, client, () => { });
        var result = await mutation.MutateAsync("x");

        calls.Should().Be(1);
        result.Should().Be("X");
        mutation.IsSuccess.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Fix 11: SetData must honor the configured CacheTime
    // ---------------------------------------------------------------

    [Fact]
    public void Query_SetData_ShouldUseConfiguredCacheTime()
    {
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "setdata-cachetime",
            QueryFn = _ => Task.FromResult<string?>("fetched"),
            CacheTime = TimeSpan.FromHours(2), // far beyond the 5 minute default
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });
        query.SetData("value");

        var entry = ((IQueryClient)client).GetCacheEntry<string>("setdata-cachetime");
        entry.Should().NotBeNull();
        entry!.ExpiresAt.Should().BeAfter(DateTime.UtcNow + TimeSpan.FromMinutes(30),
            "SetData must pass the query's CacheTime, not the client default");
    }

    // ---------------------------------------------------------------
    // Fix 12: UnregisterQuery removes empty refetcher entries
    // ---------------------------------------------------------------

    [Fact]
    public void QueryClient_UnregisterLastQuery_ShouldRemoveRefetcherEntry()
    {
        var client = new QueryClient();
        var options = new QueryOptions<string>
        {
            Key = "refetcher-cleanup",
            QueryFn = _ => Task.FromResult<string?>("data"),
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });

        var field = typeof(QueryClient).GetField("_queryRefetchers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var refetchers = (ConcurrentDictionary<string, List<QueryClient.QueryRegistration>>)field.GetValue(client)!;

        refetchers.ContainsKey("refetcher-cleanup").Should().BeTrue();

        query.Dispose();

        refetchers.ContainsKey("refetcher-cleanup").Should().BeFalse(
            "the per-key list entry must be removed when the last query unregisters");
    }

    // ---------------------------------------------------------------
    // Fix 14: client-level DefaultStaleTime / DefaultRetry are applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task Query_WithoutExplicitRetry_ShouldUseClientDefaultRetry()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions { DefaultRetry = 0 }));
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "default-retry",
            QueryFn = _ =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            },
            RetryDelay = _ => TimeSpan.FromMilliseconds(1),
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        attempts.Should().Be(1, "client DefaultRetry = 0 means a single attempt");
        query.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Query_WithExplicitRetry_ShouldOverrideClientDefault()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions { DefaultRetry = 0 }));
        var attempts = 0;

        var options = new QueryOptions<string>
        {
            Key = "explicit-retry",
            QueryFn = _ =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            },
            Retry = 2,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1),
            Enabled = () => false
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        attempts.Should().Be(3, "explicit per-query Retry wins over the client default");
    }

    [Fact]
    public async Task Query_WithoutExplicitStaleTime_ShouldUseClientDefaultStaleTime()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultStaleTime = TimeSpan.FromHours(1)
        }));

        var options = new QueryOptions<string>
        {
            Key = "default-staletime",
            QueryFn = _ => Task.FromResult<string?>("data"),
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();

        query.IsStale.Should().BeFalse("client DefaultStaleTime of 1 hour applies");
    }

    [Fact]
    public async Task Query_WithExplicitStaleTime_ShouldOverrideClientDefault()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultStaleTime = TimeSpan.FromHours(1)
        }));

        var options = new QueryOptions<string>
        {
            Key = "explicit-staletime",
            QueryFn = _ => Task.FromResult<string?>("data"),
            StaleTime = TimeSpan.Zero,
            Enabled = () => false,
            Retry = 0
        };

        var query = new Query<string>(options, client, () => { });
        await query.RefetchAsync();
        await Task.Delay(20);

        query.IsStale.Should().BeTrue("explicit StaleTime of zero wins over the client default");
    }

    // ---------------------------------------------------------------
    // Fix 16: callbacks are posted to the captured synchronization context
    // ---------------------------------------------------------------

    private sealed class RecordingSyncContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            d(state);
        }
    }

    [Fact]
    public async Task Query_OnSuccess_ShouldBePostedToCapturedSyncContext()
    {
        var client = new QueryClient();
        var ctx = new RecordingSyncContext();
        string? observed = null;

        var options = new QueryOptions<string>
        {
            Key = "synccontext",
            QueryFn = _ => Task.FromResult<string?>("data"),
            OnSuccess = data => observed = data,
            Enabled = () => false,
            Retry = 0
        };

        Query<string> query;
        var original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ctx);
        try
        {
            query = new Query<string>(options, client, () => { });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }

        // Run the fetch from a thread without the captured context
        await Task.Run(() => query.RefetchAsync());

        observed.Should().Be("data");
        ctx.PostCount.Should().BeGreaterThan(0, "callbacks must be marshalled to the captured context");
    }
}
