using Bunit;
using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

/// <summary>
/// Tests for UrlSyncStoreComponent guardrails. Multiple components sharing a store
/// must log a warning instead of throwing: during normal navigation Blazor initializes
/// the new page BEFORE disposing the old one, so a transient overlap happens on every
/// navigation between pages that sync the same store.
/// </summary>
public class UrlSyncStoreComponentTests : TestContext
{
    private record PageState(int CurrentPage);

    [Fact]
    public void TwoComponentsSharingAStore_DoesNotThrow()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new PageState(1));
        Services.AddSingleton<IStore<PageState>>(store);

        // Act - simulates Blazor's navigation order (new page initialized while the
        // old page is still alive). Previously this threw InvalidOperationException.
        var cut1 = RenderComponent<TestUrlSyncComponent>();
        var act = () => RenderComponent<TestUrlSyncComponent>();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DisposingOldComponent_AfterNewComponentRegistered_KeepsNewRegistration()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new PageState(1));
        Services.AddSingleton<IStore<PageState>>(store);

        var cut1 = RenderComponent<TestUrlSyncComponent>();
        var cut2 = RenderComponent<TestUrlSyncComponent>();

        // Act - navigation completes: the OLD page is disposed after the new one
        // registered. A third component must also not throw (registration intact).
        cut1.Instance.Dispose();
        var act = () => RenderComponent<TestUrlSyncComponent>();

        // Assert
        act.Should().NotThrow();
    }

#pragma warning disable EASB001 // Experimental feature - test usage
    private class TestUrlSyncComponent : UrlSyncStoreComponent<PageState>
    {
        [Parameter]
        public int Page { get; set; }

        protected override void ConfigureUrlSync(IUrlSyncBuilder<PageState> builder)
        {
            builder.SyncQueryParam(() => Page, s => s.CurrentPage, "page");
        }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, State.CurrentPage);
            builder.CloseElement();
        }
    }
#pragma warning restore EASB001
}
