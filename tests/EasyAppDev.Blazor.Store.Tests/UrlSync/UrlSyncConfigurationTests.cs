using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

/// <summary>
/// Tests for UrlSyncConfiguration.ApplyComponentParamsToState - the URL → State direction.
/// Covers the rewrite that prevents total state wipes (parameterless-ctor classes),
/// silent resets of non-primary-ctor properties on records, DBNull ctor arguments,
/// and clobbering of state by component defaults when the query param is absent.
/// </summary>
public class UrlSyncConfigurationTests
{
    // Component-parameter stand-ins (the builder expressions read these)
    public int Page { get; set; }
    public string? Search { get; set; }

    private record RecordState(int CurrentPage, string Query)
    {
        // Settable property NOT covered by the primary constructor
        public string Extra { get; set; } = string.Empty;
    }

    private record InitOnlyExtraState(int CurrentPage)
    {
        // Init-only property NOT covered by the primary constructor
        public string Tag { get; init; } = string.Empty;
    }

    private class MutableState
    {
        public int CurrentPage { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class OptionalCtorParamState
    {
        public OptionalCtorParamState(int currentPage, string label = "default-label")
        {
            CurrentPage = currentPage;
            Name = label;
        }

        public int CurrentPage { get; }
        public string Name { get; set; }
    }

    private static IReadOnlySet<string> Present(params string[] names)
        => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    private UrlSyncConfiguration<TState> BuildPageConfig<TState>(
        System.Linq.Expressions.Expression<Func<TState, int>> stateProperty)
        where TState : notnull
    {
        var builder = new UrlSyncBuilder<TState>();
        builder.SyncQueryParam(() => Page, stateProperty, "page");
        return builder.Build();
    }

    [Fact]
    public void Apply_MutableClassWithParameterlessCtor_PreservesUnmappedPropertiesAndReturnsNewInstance()
    {
        // Arrange
        Page = 2;
        var config = BuildPageConfig<MutableState>(s => s.CurrentPage);
        var current = new MutableState { CurrentPage = 5, Name = "must-survive" };

        // Act
        var result = config.ApplyComponentParamsToState(current, Present("page"));

        // Assert - previously this returned a default-initialized object (TOTAL state wipe)
        result.Should().NotBeSameAs(current, "mutable state must never be mutated in place");
        result.CurrentPage.Should().Be(2);
        result.Name.Should().Be("must-survive");
        current.CurrentPage.Should().Be(5, "the original instance must not be mutated");
    }

    [Fact]
    public void Apply_AbsentQueryParam_DoesNotClobberStateWithComponentDefault()
    {
        // Arrange - hydrated state has Page=5; URL has NO page param; component default is 0
        Page = 0;
        var config = BuildPageConfig<MutableState>(s => s.CurrentPage);
        var current = new MutableState { CurrentPage = 5, Name = "hydrated" };

        // Act
        var result = config.ApplyComponentParamsToState(current, Present(/* nothing in URL */));

        // Assert
        result.Should().BeSameAs(current, "no mapped query param is present, so state is unchanged");
        result.CurrentPage.Should().Be(5);
    }

    [Fact]
    public void Apply_Record_PreservesNonPrimaryCtorSettableProperty()
    {
        // Arrange
        Page = 3;
        var config = BuildPageConfig<RecordState>(s => s.CurrentPage);
        var current = new RecordState(1, "hello") { Extra = "must-survive" };

        // Act
        var result = config.ApplyComponentParamsToState(current, Present("page"));

        // Assert - previously Extra was silently reset to its default
        result.CurrentPage.Should().Be(3);
        result.Query.Should().Be("hello");
        result.Extra.Should().Be("must-survive");
    }

    [Fact]
    public void Apply_OverlaidValueEqualsCurrentValue_ReturnsSameInstance()
    {
        // Arrange
        Page = 5;
        var config = BuildPageConfig<MutableState>(s => s.CurrentPage);
        var current = new MutableState { CurrentPage = 5, Name = "same" };

        // Act
        var result = config.ApplyComponentParamsToState(current, Present("page"));

        // Assert - no rebuild when nothing would change (avoids spurious store updates)
        result.Should().BeSameAs(current);
    }

    [Fact]
    public void Apply_CtorWithUnmatchedOptionalParameter_UsesDefaultInsteadOfDBNull()
    {
        // Arrange - "label" ctor param matches no property; it is optional
        Page = 7;
        var config = BuildPageConfig<OptionalCtorParamState>(s => s.CurrentPage);
        var current = new OptionalCtorParamState(1) { Name = "kept-by-setter" };

        // Act - previously DBNull.Value was passed for unmatched params (crash)
        var act = () => config.ApplyComponentParamsToState(current, Present("page"));

        // Assert
        var result = act.Should().NotThrow().Subject;
        result.CurrentPage.Should().Be(7);
        result.Name.Should().Be("kept-by-setter", "settable properties are re-applied after construction");
    }

    [Fact]
    public void Apply_InitOnlyNonCtorProperty_DoesNotThrow()
    {
        // Arrange - Tag is init-only and not covered by the primary ctor; it cannot be
        // restored safely and is skipped (with a debug log)
        Page = 9;
        var config = BuildPageConfig<InitOnlyExtraState>(s => s.CurrentPage);
        var current = new InitOnlyExtraState(1) { Tag = "cannot-restore" };

        // Act
        var act = () => config.ApplyComponentParamsToState(current, Present("page"));

        // Assert
        var result = act.Should().NotThrow().Subject;
        result.CurrentPage.Should().Be(9);
    }

    [Fact]
    public void BuildUrlQueryParams_NullValue_IsEmittedAsNullSoTheParamIsRemoved()
    {
        // Arrange
        var builder = new UrlSyncBuilder<RecordState>();
        builder.SyncQueryParam(() => Page, s => s.CurrentPage, "page");
        builder.SyncQueryParam(() => Search, s => s.Query, "q");
        var config = builder.Build();

        // Act - Query is null so "q" must be present in the dictionary with a null
        // value (GetUriWithQueryParameters removes null-valued parameters)
        var queryParams = config.BuildUrlQueryParams(new RecordState(4, null!));

        // Assert
        queryParams.Should().ContainKey("page").WhoseValue.Should().Be("4");
        queryParams.Should().ContainKey("q").WhoseValue.Should().BeNull();
    }
}
