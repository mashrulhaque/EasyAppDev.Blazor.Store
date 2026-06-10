using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

/// <summary>
/// Tests for UrlSyncManager: State → URL synchronization (preserving unrelated query
/// params and fragments, removing stale params), action filtering via the action-aware
/// subscription, dispatcher marshaling, and presence-checked URL → State sync.
/// </summary>
public class UrlSyncManagerTests
{
    private record TestState(int CurrentPage, string? Query);

    // Component-parameter stand-ins (the builder expressions read these)
    public int Page { get; set; }
    public string? Search { get; set; }

    private sealed class TestNavigationManager : NavigationManager
    {
        public readonly List<string> Navigations = new();

        public TestNavigationManager(string uri)
        {
            Initialize("http://localhost/", uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
            => HandleNavigation(uri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
            => HandleNavigation(uri);

        private void HandleNavigation(string uri)
        {
            var absolute = ToAbsoluteUri(uri).ToString();
            Navigations.Add(absolute);
            Uri = absolute;
        }
    }

    private UrlSyncConfiguration<TestState> BuildConfig(Action<UrlSyncBuilder<TestState>>? extra = null)
    {
        var builder = new UrlSyncBuilder<TestState>();
        builder.SyncQueryParam(() => Page, s => s.CurrentPage, "page");
        builder.SyncQueryParam(() => Search, s => s.Query, "q");
        builder.WithDebounce(TimeSpan.FromMilliseconds(1));
        extra?.Invoke(builder);
        return builder.Build();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var start = Environment.TickCount;
        while (!condition() && Environment.TickCount - start < timeoutMs)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task SyncToUrl_PreservesUnrelatedQueryParamsAndFragment()
    {
        // Arrange
        var nav = new TestNavigationManager("http://localhost/products?utm_source=newsletter&page=1#section-2");
        var store = StoreTestHelpers.CreateStore(new TestState(1, null));
        using var manager = new UrlSyncManager<TestState>(store, nav, BuildConfig());
        manager.Start();

        // Act
        await store.UpdateAsync(s => s with { CurrentPage = 3 }, "SET_PAGE");
        await WaitUntilAsync(() => nav.Navigations.Count > 0);

        // Assert - unrelated query params and the fragment must survive
        nav.Navigations.Should().NotBeEmpty();
        var url = nav.Navigations[^1];
        url.Should().Contain("utm_source=newsletter");
        url.Should().Contain("page=3");
        url.Should().EndWith("#section-2");
    }

    [Fact]
    public async Task SyncToUrl_NullValue_RemovesStaleQueryParam_EvenWhenAllValuesAreNull()
    {
        // Arrange - only a single string mapping; clearing it produces an all-null
        // dictionary which must STILL navigate to remove the stale param
        var nav = new TestNavigationManager("http://localhost/products?q=old-search");
        var store = StoreTestHelpers.CreateStore(new TestState(0, "old-search"));

        var builder = new UrlSyncBuilder<TestState>();
        builder.SyncQueryParam(() => Search, s => s.Query, "q");
        builder.WithDebounce(TimeSpan.FromMilliseconds(1));
        using var manager = new UrlSyncManager<TestState>(store, nav, builder.Build());
        manager.Start();

        // Act
        await store.UpdateAsync(s => s with { Query = null }, "CLEAR_SEARCH");
        await WaitUntilAsync(() => nav.Navigations.Count > 0);

        // Assert
        nav.Navigations.Should().NotBeEmpty("an all-null query dictionary must still navigate to clear stale params");
        nav.Navigations[^1].Should().NotContain("q=");
    }

    [Fact]
    public async Task SyncToUrl_DefaultExcludedActions_DoNotTriggerNavigation()
    {
        // Arrange
        var nav = new TestNavigationManager("http://localhost/products");
        var store = StoreTestHelpers.CreateStore(new TestState(1, null));
        using var manager = new UrlSyncManager<TestState>(store, nav, BuildConfig());
        manager.Start();

        // Act - "@@SYNC" / "@@SYNC_FULL" are the actions ServerSync actually dispatches,
        // and "SYNC_*" is the prefix TabSyncMiddleware dispatches
        await store.UpdateAsync(s => s with { CurrentPage = 2 }, "@@SYNC");
        await store.UpdateAsync(s => s with { CurrentPage = 3 }, "@@SYNC_FULL");
        await store.UpdateAsync(s => s with { CurrentPage = 4 }, "SYNC_UPDATE");
        await Task.Delay(200);

        // Assert
        nav.Navigations.Should().BeEmpty("excluded actions must never sync to the URL");
    }

    [Fact]
    public async Task SyncToUrl_UserConfiguredExcludedActions_AreHonored()
    {
        // Arrange - before the fix Start() used the action-less Subscribe overload,
        // so ExcludeActions was dead code
        var nav = new TestNavigationManager("http://localhost/products");
        var store = StoreTestHelpers.CreateStore(new TestState(1, null));
        using var manager = new UrlSyncManager<TestState>(
            store, nav, BuildConfig(b => b.ExcludeActions("INTERNAL_UPDATE")));
        manager.Start();

        // Act
        await store.UpdateAsync(s => s with { CurrentPage = 2 }, "INTERNAL_UPDATE");
        await Task.Delay(200);

        // Assert
        nav.Navigations.Should().BeEmpty();

        // Sanity check: a non-excluded action still syncs
        await store.UpdateAsync(s => s with { CurrentPage = 5 }, "USER_ACTION");
        await WaitUntilAsync(() => nav.Navigations.Count > 0);
        nav.Navigations.Should().NotBeEmpty();
        nav.Navigations[^1].Should().Contain("page=5");
    }

    [Fact]
    public async Task SyncToUrl_RunsThroughTheProvidedInvokeAsyncDelegate()
    {
        // Arrange - NavigateTo must run on the Blazor dispatcher; the manager must
        // marshal the debounced sync through the supplied delegate
        var nav = new TestNavigationManager("http://localhost/products");
        var store = StoreTestHelpers.CreateStore(new TestState(1, null));
        var marshaled = 0;

        using var manager = new UrlSyncManager<TestState>(
            store,
            nav,
            BuildConfig(),
            logger: null,
            invokeAsync: async work =>
            {
                Interlocked.Increment(ref marshaled);
                await work();
            });
        manager.Start();

        // Act
        await store.UpdateAsync(s => s with { CurrentPage = 9 }, "SET_PAGE");
        await WaitUntilAsync(() => nav.Navigations.Count > 0);

        // Assert
        nav.Navigations.Should().NotBeEmpty();
        marshaled.Should().BeGreaterThan(0, "the navigation must be marshaled through the invokeAsync delegate");
    }

    [Fact]
    public async Task SyncFromComponentParameters_AbsentQueryParam_DoesNotClobberHydratedState()
    {
        // Arrange - clean URL (no query params), hydrated state Page=5, component default 0
        var nav = new TestNavigationManager("http://localhost/products");
        var store = StoreTestHelpers.CreateStore(new TestState(5, "hydrated"));
        Page = 0;
        Search = null;
        using var manager = new UrlSyncManager<TestState>(store, nav, BuildConfig());
        manager.Start();

        // Act
        await manager.SyncFromComponentParametersAsync();

        // Assert - state must not be reset by the component-parameter defaults
        store.GetState().Should().Be(new TestState(5, "hydrated"));
    }

    [Fact]
    public async Task SyncFromComponentParameters_PresentQueryParam_IsAppliedAndOthersPreserved()
    {
        // Arrange - URL contains ?page=2; Search/q is absent
        var nav = new TestNavigationManager("http://localhost/products?page=2");
        var store = StoreTestHelpers.CreateStore(new TestState(5, "hydrated"));
        Page = 2;       // Blazor supplied this from the query string
        Search = null;  // component default; q is NOT in the URL
        using var manager = new UrlSyncManager<TestState>(store, nav, BuildConfig());
        manager.Start();

        // Act
        await manager.SyncFromComponentParametersAsync();

        // Assert - page applied, query preserved
        store.GetState().Should().Be(new TestState(2, "hydrated"));
    }
}
