// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

/// <summary>
/// Tests for RefetchOnWindowFocus / RefetchOnReconnect. The JS-invokable
/// callbacks (OnWindowFocusAsync / OnReconnectAsync) are invoked directly on
/// the QueryClient, so no JS runtime is required.
/// </summary>
public class QueryWindowEventsTests
{
    private static QueryOptions<string> CreateOptions(
        string key,
        Action onFetch,
        Action<QueryOptions<string>>? configure = null)
    {
        var options = new QueryOptions<string>
        {
            Key = key,
            QueryFn = _ =>
            {
                onFetch();
                return Task.FromResult<string?>("data");
            }
        };

        configure?.Invoke(options);
        return options;
    }

    // ---------------------------------------------------------------
    // OnWindowFocusAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnWindowFocus_StaleEnabledQuery_ShouldRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        // Default StaleTime is 0 => immediately stale; flag defaults to true.
        var query = new Query<string>(
            CreateOptions("focus-stale", () => fetchCount++), client, () => { });

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(1, "a stale, enabled query with RefetchOnWindowFocus=true must refetch on focus");
        query.Data.Should().Be("data");
    }

    [Fact]
    public async Task OnWindowFocus_FlagDisabled_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-flag-off", () => fetchCount++,
                o => o.RefetchOnWindowFocus = false),
            client, () => { });

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "RefetchOnWindowFocus=false must opt the query out of focus refetching");
    }

    [Fact]
    public async Task OnWindowFocus_DisabledQuery_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-disabled", () => fetchCount++,
                o => o.Enabled = () => false),
            client, () => { });

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "disabled queries must not refetch on focus");
    }

    [Fact]
    public async Task OnWindowFocus_FreshData_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-fresh", () => fetchCount++, o =>
            {
                o.StaleTime = TimeSpan.FromHours(1);
                o.InitialData = "fresh";
                o.InitialDataUpdatedAt = DateTime.UtcNow;
            }),
            client, () => { });

        query.IsStale.Should().BeFalse();

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "data within StaleTime is fresh and must not refetch on focus");
    }

    [Fact]
    public async Task OnWindowFocus_StaleData_ShouldRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-stale-data", () => fetchCount++, o =>
            {
                o.StaleTime = TimeSpan.FromMilliseconds(1);
                o.InitialData = "old";
                o.InitialDataUpdatedAt = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            }),
            client, () => { });

        query.IsStale.Should().BeTrue();

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(1, "data older than StaleTime must refetch on focus");
        query.Data.Should().Be("data");
    }

    // ---------------------------------------------------------------
    // OnReconnectAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnReconnect_StaleEnabledQuery_ShouldRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("reconnect-stale", () => fetchCount++), client, () => { });

        await client.OnReconnectAsync();

        fetchCount.Should().Be(1, "a stale, enabled query with RefetchOnReconnect=true must refetch on reconnect");
    }

    [Fact]
    public async Task OnReconnect_FlagDisabled_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("reconnect-flag-off", () => fetchCount++,
                o => o.RefetchOnReconnect = false),
            client, () => { });

        await client.OnReconnectAsync();

        fetchCount.Should().Be(0, "RefetchOnReconnect=false must opt the query out of reconnect refetching");
    }

    [Fact]
    public async Task OnReconnect_FocusFlagOff_ShouldStillRefetch()
    {
        // The two flags are independent: focus off, reconnect (default) on.
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("reconnect-independent", () => fetchCount++,
                o => o.RefetchOnWindowFocus = false),
            client, () => { });

        await client.OnWindowFocusAsync();
        fetchCount.Should().Be(0);

        await client.OnReconnectAsync();
        fetchCount.Should().Be(1, "RefetchOnWindowFocus=false must not affect reconnect refetching");
    }

    // ---------------------------------------------------------------
    // Client-level default resolution
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnWindowFocus_ClientDefaultFalse_UnsetQuery_ShouldNotRefetch()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultRefetchOnWindowFocus = false
        }));
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-client-default", () => fetchCount++), client, () => { });

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "an unset per-query flag must fall back to the client default (false)");
    }

    [Fact]
    public async Task OnWindowFocus_ExplicitTrue_OverridesClientDefaultFalse()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultRefetchOnWindowFocus = false
        }));
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-explicit-true", () => fetchCount++,
                o => o.RefetchOnWindowFocus = true),
            client, () => { });

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(1, "an explicit per-query RefetchOnWindowFocus=true must override the client default");
    }

    [Fact]
    public async Task OnReconnect_ClientDefaultFalse_UnsetQuery_ShouldNotRefetch()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultRefetchOnReconnect = false
        }));
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("reconnect-client-default", () => fetchCount++), client, () => { });

        await client.OnReconnectAsync();

        fetchCount.Should().Be(0, "an unset per-query flag must fall back to the client default (false)");
    }

    [Fact]
    public async Task OnReconnect_ExplicitTrue_OverridesClientDefaultFalse()
    {
        var client = new QueryClient(Options.Create(new QueryClientOptions
        {
            DefaultRefetchOnReconnect = false
        }));
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("reconnect-explicit-true", () => fetchCount++,
                o => o.RefetchOnReconnect = true),
            client, () => { });

        await client.OnReconnectAsync();

        fetchCount.Should().Be(1, "an explicit per-query RefetchOnReconnect=true must override the client default");
    }

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnWindowFocus_DisposedQuery_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        var query = new Query<string>(
            CreateOptions("focus-disposed", () => fetchCount++), client, () => { });
        query.Dispose();

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "disposed (unregistered) queries must not refetch on focus");
    }

    [Fact]
    public async Task OnReconnect_DisposedQuery_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        var query = new Query<string>(
            CreateOptions("reconnect-disposed", () => fetchCount++), client, () => { });
        query.Dispose();

        await client.OnReconnectAsync();

        fetchCount.Should().Be(0, "disposed (unregistered) queries must not refetch on reconnect");
    }

    [Fact]
    public async Task OnWindowFocus_OnlyMatchingQueriesRefetch()
    {
        var client = new QueryClient();
        var fetchA = 0;
        var fetchB = 0;

        using var queryA = new Query<string>(
            CreateOptions("focus-multi-a", () => fetchA++), client, () => { });
        using var queryB = new Query<string>(
            CreateOptions("focus-multi-b", () => fetchB++,
                o => o.RefetchOnWindowFocus = false),
            client, () => { });

        await client.OnWindowFocusAsync();

        fetchA.Should().Be(1);
        fetchB.Should().Be(0);
    }

    [Fact]
    public async Task OnWindowFocus_QueryFnThrows_ShouldNotPropagateAndStillRefetchOthers()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var failing = new Query<string>(
            new QueryOptions<string>
            {
                Key = "focus-throwing",
                QueryFn = _ => throw new InvalidOperationException("boom"),
                Retry = 0
            },
            client, () => { });
        using var succeeding = new Query<string>(
            CreateOptions("focus-succeeding", () => fetchCount++), client, () => { });

        var act = async () => await client.OnWindowFocusAsync();

        await act.Should().NotThrowAsync("per-query failures must be contained");
        fetchCount.Should().Be(1, "other queries must still refetch when one fails");
    }

    [Fact]
    public async Task OnWindowFocus_NoRegisteredQueries_ShouldNotThrow()
    {
        var client = new QueryClient();

        var focusAct = async () => await client.OnWindowFocusAsync();
        var reconnectAct = async () => await client.OnReconnectAsync();

        await focusAct.Should().NotThrowAsync();
        await reconnectAct.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnWindowFocus_DisposedClient_ShouldNotRefetch()
    {
        var client = new QueryClient();
        var fetchCount = 0;

        using var query = new Query<string>(
            CreateOptions("focus-disposed-client", () => fetchCount++), client, () => { });

        client.Dispose();

        await client.OnWindowFocusAsync();

        fetchCount.Should().Be(0, "a disposed client must ignore window events");
    }

    [Fact]
    public async Task QueryClient_DisposeAsync_ShouldBeIdempotentAndSafeWithoutJs()
    {
        var client = new QueryClient();

        using var query = new Query<string>(
            CreateOptions("dispose-async", () => { }), client, () => { });

        await client.DisposeAsync();
        await client.DisposeAsync(); // idempotent
        client.Dispose();            // mixed sync/async disposal is safe
    }
}
