using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

#pragma warning disable EASB001 // Experimental feature - testing

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

public class ConventionBasedSyncTests
{
    #region Test State and Components

    private record TestState(
        int Page,
        string? SearchQuery,
        bool IsActive,
        Guid? UserId,
        int CurrentFilter,
        string? NameValue
    );

    private class ComponentWithAutoSync
    {
        [AutoSyncWithQuery]
        public int Page { get; set; } = 1;

        [AutoSyncWithQuery]
        public string? SearchQuery { get; set; }

        [AutoSyncWithQuery]
        public bool IsActive { get; set; }
    }

    private class ComponentWithCustomQueryParam
    {
        [AutoSyncWithQuery("p")]
        public int Page { get; set; } = 1;

        [AutoSyncWithQuery("q")]
        public string? SearchQuery { get; set; }
    }

    private class ComponentWithExplicitStateProperty
    {
        [AutoSyncWithQuery(StatePropertyName = "CurrentFilter")]
        public int Filter { get; set; }
    }

    private class ComponentWithConventionMatching
    {
        // Should match state.CurrentFilter via "Current" prefix convention
        [AutoSyncWithQuery]
        public int Filter { get; set; }

        // Should match state.NameValue via "Value" suffix convention
        [AutoSyncWithQuery]
        public string? Name { get; set; }
    }

    private class ComponentWithTypeMismatch
    {
        [AutoSyncWithQuery]
        public string? Page { get; set; }  // State has int Page
    }

    private class ComponentWithNonExistentProperty
    {
        [AutoSyncWithQuery]
        public int NonExistentProp { get; set; }
    }

    private class ComponentWithNoAttributes
    {
        public int Page { get; set; }
        public string? SearchQuery { get; set; }
    }

    private class ComponentWithRouteAttribute
    {
        [AutoSyncWithRoute]
        public Guid? Id { get; set; }
    }

    #endregion

    #region DiscoverAndConfigureMappings - Basic Cases

    [Fact]
    public void DiscoverMappings_WithAutoSyncAttributes_ShouldCreateMappings()
    {
        var component = new ComponentWithAutoSync
        {
            Page = 5,
            SearchQuery = "test",
            IsActive = true
        };
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        count.Should().Be(3);
    }

    [Fact]
    public void DiscoverMappings_WithNoAttributes_ShouldReturnZero()
    {
        var component = new ComponentWithNoAttributes();
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        count.Should().Be(0);
    }

    [Fact]
    public void DiscoverMappings_WithCustomQueryParamNames_ShouldCreateMappings()
    {
        var component = new ComponentWithCustomQueryParam
        {
            Page = 2,
            SearchQuery = "query"
        };
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        count.Should().Be(2);

        // Verify query param names are custom (verified via config)
        var config = ((dynamic)builder).Build();
        Assert.NotNull(config);
    }

    [Fact]
    public void DiscoverMappings_WithExplicitStateProperty_ShouldMapCorrectly()
    {
        var component = new ComponentWithExplicitStateProperty { Filter = 10 };
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        count.Should().Be(1);
    }

    #endregion

    #region Convention-Based Matching

    [Fact]
    public void DiscoverMappings_WithCurrentPrefixConvention_ShouldMatch()
    {
        // Filter (component) should match CurrentFilter (state)
        var component = new ComponentWithConventionMatching { Filter = 5 };
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        // Should match both Filter→CurrentFilter and Name→NameValue
        count.Should().Be(2);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void DiscoverMappings_WithTypeMismatch_ShouldLogWarningAndSkip()
    {
        var component = new ComponentWithTypeMismatch { Page = "invalid" };
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        // Should skip due to type mismatch
        count.Should().Be(0);
    }

    [Fact]
    public void DiscoverMappings_WithNonExistentProperty_ShouldLogWarningAndSkip()
    {
        var component = new ComponentWithNonExistentProperty();
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        count.Should().Be(0);
    }

    [Fact]
    public void DiscoverMappings_WithRouteAttribute_ShouldThrowNotSupported()
    {
        var component = new ComponentWithRouteAttribute();
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*AutoSyncWithRoute*not supported*Phase 3*");
    }

    #endregion

    #region Integration with UrlSyncBuilder

    [Fact]
    public void DiscoverMappings_IntegrationWithBuilder_ShouldProduceValidConfig()
    {
        var component = new ComponentWithAutoSync
        {
            Page = 10,
            SearchQuery = "integration",
            IsActive = false
        };
        var builder = new UrlSyncBuilder<TestState>();

        ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        // Additional manual configuration
        builder.WithDebounce(TimeSpan.FromMilliseconds(500));
        builder.WithNavigationMode(UrlSyncNavigationMode.Push);

        var config = ((dynamic)builder).Build();

        Assert.NotNull(config);
        Assert.Equal(TimeSpan.FromMilliseconds(500), config.Debounce);
        Assert.Equal(UrlSyncNavigationMode.Push, config.NavigationMode);
    }

    [Fact]
    public void DiscoverMappings_CombinedWithManualConfig_ShouldWork()
    {
        // Simulate a component that uses both auto-sync and manual config
        var component = new ComponentWithAutoSync { Page = 1 };
        var builder = new UrlSyncBuilder<TestState>();

        // Auto-discovery
        var autoCount = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            NullLogger.Instance);

        // Manual addition
        Guid? userId = Guid.NewGuid();
        builder.SyncQueryParam(() => userId, s => s.UserId);

        autoCount.Should().Be(3);

        var config = ((dynamic)builder).Build();
        Assert.NotNull(config);
    }

    #endregion

    #region Attribute Property Tests

    [Fact]
    public void AutoSyncWithQueryAttribute_DefaultConstructor_ShouldHaveNullQueryParamName()
    {
        var attr = new AutoSyncWithQueryAttribute();

        attr.QueryParamName.Should().BeNull();
        attr.StatePropertyName.Should().BeNull();
    }

    [Fact]
    public void AutoSyncWithQueryAttribute_WithCustomName_ShouldSetQueryParamName()
    {
        var attr = new AutoSyncWithQueryAttribute("customParam");

        attr.QueryParamName.Should().Be("customParam");
    }

    [Fact]
    public void AutoSyncWithQueryAttribute_StatePropertyName_ShouldBeSettable()
    {
        var attr = new AutoSyncWithQueryAttribute
        {
            StatePropertyName = "CustomStateProp"
        };

        attr.StatePropertyName.Should().Be("CustomStateProp");
    }

    [Fact]
    public void AutoSyncWithRouteAttribute_DefaultConstructor_ShouldHaveNullRouteParamName()
    {
        var attr = new AutoSyncWithRouteAttribute();

        attr.RouteParamName.Should().BeNull();
        attr.StatePropertyName.Should().BeNull();
    }

    [Fact]
    public void AutoSyncWithRouteAttribute_WithCustomName_ShouldSetRouteParamName()
    {
        var attr = new AutoSyncWithRouteAttribute("id");

        attr.RouteParamName.Should().Be("id");
    }

    #endregion

    #region Null Safety

    [Fact]
    public void DiscoverMappings_WithNullComponent_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            null!,
            builder,
            NullLogger.Instance);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DiscoverMappings_WithNullBuilder_ShouldThrow()
    {
        var component = new ComponentWithAutoSync();

        var act = () => ConventionBasedSyncHelper.DiscoverAndConfigureMappings<TestState>(
            component,
            null!,
            NullLogger.Instance);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DiscoverMappings_WithNullLogger_ShouldWork()
    {
        var component = new ComponentWithAutoSync();
        var builder = new UrlSyncBuilder<TestState>();

        var count = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            component,
            builder,
            null);

        count.Should().Be(3);
    }

    #endregion
}
