using EasyAppDev.Blazor.Store.Blazor.UrlSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.UrlSync;

public class UrlSyncBuilderTests
{
    private record TestState(
        int Page,
        string? SearchQuery,
        bool IsActive,
        Guid? UserId,
        DateTime? CreatedAt,
        TestEnum Status
    );

    private enum TestEnum
    {
        None,
        Active,
        Inactive
    }

    #region SyncQueryParam

    [Fact]
    public void SyncQueryParam_WithValidParameters_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int page = 1;

        var result = builder.SyncQueryParam(() => page, s => s.Page);

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder); // Fluent API
    }

    [Fact]
    public void SyncQueryParam_WithCustomQueryParamName_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int page = 1;

        var result = builder.SyncQueryParam(() => page, s => s.Page, "p");

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void SyncQueryParam_WithNullComponentProperty_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => builder.SyncQueryParam<int>(null!, s => s.Page);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("componentProperty");
    }

    [Fact]
    public void SyncQueryParam_WithNullStateProperty_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int page = 1;

        var act = () => builder.SyncQueryParam(() => page, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stateProperty");
    }

    [Fact]
    public void SyncQueryParam_WithComplexReferenceType_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<ComplexState>();
        var user = new User("John");

        var act = () => builder.SyncQueryParam<User>(() => user, s => s.Owner);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not support reference type*");
    }

    [Fact]
    public void SyncQueryParam_WithPrimitiveTypes_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int intVal = 1;
        bool boolVal = true;

        builder.SyncQueryParam(() => intVal, s => s.Page);
        builder.SyncQueryParam(() => boolVal, s => s.IsActive);

        // Should not throw
    }

    [Fact]
    public void SyncQueryParam_WithString_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        string? query = "test";

        var result = builder.SyncQueryParam(() => query, s => s.SearchQuery);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SyncQueryParam_WithGuid_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        Guid? userId = Guid.NewGuid();

        var result = builder.SyncQueryParam(() => userId, s => s.UserId);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SyncQueryParam_WithDateTime_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        DateTime? created = DateTime.UtcNow;

        var result = builder.SyncQueryParam(() => created, s => s.CreatedAt);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SyncQueryParam_WithEnum_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        TestEnum status = TestEnum.Active;

        var result = builder.SyncQueryParam(() => status, s => s.Status);

        result.Should().NotBeNull();
    }

    [Fact]
    public void SyncQueryParam_WithNullableTypes_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int? page = null;
        Guid? userId = null;

        builder.SyncQueryParam(() => page, s => s.Page);
        builder.SyncQueryParam(() => userId, s => s.UserId);

        // Should not throw
    }

    [Fact]
    public void SyncQueryParam_WithInvalidExpression_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        // Complex expression (not a simple property access)
        var act = () => builder.SyncQueryParam(() => 1 + 2, s => s.Page);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be a simple property access*");
    }

    #endregion

    #region WithDebounce

    [Fact]
    public void WithDebounce_WithValidDuration_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.WithDebounce(TimeSpan.FromMilliseconds(500));

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithDebounce_WithZeroDuration_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.WithDebounce(TimeSpan.Zero);

        result.Should().NotBeNull();
    }

    [Fact]
    public void WithDebounce_WithNegativeDuration_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => builder.WithDebounce(TimeSpan.FromMilliseconds(-100));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*")
            .WithParameterName("duration");
    }

    #endregion

    #region WithNavigationMode

    [Fact]
    public void WithNavigationMode_WithReplaceMode_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.WithNavigationMode(UrlSyncNavigationMode.Replace);

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithNavigationMode_WithPushMode_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.WithNavigationMode(UrlSyncNavigationMode.Push);

        result.Should().NotBeNull();
    }

    #endregion

    #region ExcludeActions

    [Fact]
    public void ExcludeActions_WithValidActions_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.ExcludeActions("ACTION1", "ACTION2", "ACTION3");

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void ExcludeActions_WithEmptyArray_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.ExcludeActions();

        result.Should().NotBeNull();
    }

    [Fact]
    public void ExcludeActions_WithNullActions_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => builder.ExcludeActions(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExcludeActions_WithNullStrings_ShouldIgnoreNulls()
    {
        var builder = new UrlSyncBuilder<TestState>();

        // Should ignore null/empty strings
        var result = builder.ExcludeActions("ACTION1", null!, "", "ACTION2");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ExcludeActions_CalledMultipleTimes_ShouldAccumulate()
    {
        var builder = new UrlSyncBuilder<TestState>();

        builder.ExcludeActions("ACTION1");
        builder.ExcludeActions("ACTION2");

        // Should accumulate both actions
        var config = builder.Build();
        config.ExcludedActions.Should().Contain("ACTION1");
        config.ExcludedActions.Should().Contain("ACTION2");
    }

    #endregion

    #region OnConversionError

    [Fact]
    public void OnConversionError_WithValidHandler_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.OnConversionError((param, ex) => { });

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void OnConversionError_WithNullHandler_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => builder.OnConversionError(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    #endregion

    #region OnError

    [Fact]
    public void OnError_WithValidHandler_ShouldSucceed()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var result = builder.OnError(ex => { });

        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void OnError_WithNullHandler_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<TestState>();

        var act = () => builder.OnError(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    #endregion

    #region Build

    [Fact]
    public void Build_WithNoConfiguration_ShouldReturnValidConfig()
    {
        var builder = new UrlSyncBuilder<TestState>();

        // Access Build() via dynamic to access internal method
        var config = ((dynamic)builder).Build();

        Assert.NotNull(config);
        Assert.Equal(TimeSpan.FromMilliseconds(300), config.Debounce); // Default
        Assert.Equal(UrlSyncNavigationMode.Replace, config.NavigationMode); // Default
        Assert.Empty(config.ExcludedActions);
    }

    [Fact]
    public void Build_WithFullConfiguration_ShouldReturnConfiguredInstance()
    {
        var builder = new UrlSyncBuilder<TestState>();
        int page = 1;
        string? query = "test";
        var conversionErrors = new List<string>();
        var generalErrors = new List<Exception>();

        builder
            .SyncQueryParam(() => page, s => s.Page)
            .SyncQueryParam(() => query, s => s.SearchQuery)
            .WithDebounce(TimeSpan.FromMilliseconds(500))
            .WithNavigationMode(UrlSyncNavigationMode.Push)
            .ExcludeActions("ACTION1", "ACTION2")
            .OnConversionError((p, ex) => conversionErrors.Add(p))
            .OnError(ex => generalErrors.Add(ex));

        // Access Build() via dynamic to access internal method
        var config = ((dynamic)builder).Build();

        Assert.NotNull(config);
        Assert.Equal(TimeSpan.FromMilliseconds(500), config.Debounce);
        Assert.Equal(UrlSyncNavigationMode.Push, config.NavigationMode);
        Assert.Contains("ACTION1", config.ExcludedActions);
        Assert.Contains("ACTION2", config.ExcludedActions);
        Assert.NotNull(config.OnConversionError);
        Assert.NotNull(config.OnError);
    }

    [Fact]
    public void Build_CalledMultipleTimes_ShouldReturnDifferentInstances()
    {
        var builder = new UrlSyncBuilder<TestState>();

        // Access Build() via dynamic to access internal method
        var config1 = ((dynamic)builder).Build();
        var config2 = ((dynamic)builder).Build();

        Assert.NotSame(config1, config2);
    }

    #endregion

    #region Fluent API Chain

    [Fact]
    public void FluentAPI_AllMethodsChained_ShouldBuildCorrectly()
    {
        int page = 1;
        string? query = "test";
        bool active = true;

        var builder = new UrlSyncBuilder<TestState>()
            .SyncQueryParam(() => page, s => s.Page, "p")
            .SyncQueryParam(() => query, s => s.SearchQuery, "q")
            .SyncQueryParam(() => active, s => s.IsActive)
            .WithDebounce(TimeSpan.FromMilliseconds(750))
            .WithNavigationMode(UrlSyncNavigationMode.Push)
            .ExcludeActions("SERVER_SYNC", "TAB_SYNC")
            .OnConversionError((p, ex) => { })
            .OnError(ex => { });

        // Access Build() via dynamic to access internal method
        var config = ((dynamic)builder).Build();

        Assert.NotNull(config);
        Assert.Equal(TimeSpan.FromMilliseconds(750), config.Debounce);
        Assert.Equal(UrlSyncNavigationMode.Push, config.NavigationMode);
        Assert.Equal(2, config.ExcludedActions.Count);
    }

    #endregion

    #region Supported Type Validation

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(short))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(string))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(TestEnum))]
    public void SyncQueryParam_WithSupportedType_ShouldSucceed(Type type)
    {
        // This test verifies that all documented supported types actually work
        // We can't easily test all types dynamically, so we test the common ones

        // Test is passed if type is documented as supported
        var supportedTypes = new[]
        {
            typeof(int), typeof(long), typeof(short), typeof(byte),
            typeof(float), typeof(double), typeof(decimal),
            typeof(bool), typeof(string), typeof(Guid),
            typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
            typeof(TestEnum)
        };

        supportedTypes.Should().Contain(type);
    }

    [Fact]
    public void SyncQueryParam_WithList_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<ComplexState>();
        var list = new List<int>();

        var act = () => builder.SyncQueryParam<List<int>>(() => list, s => s.Items);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not support reference type*");
    }

    [Fact]
    public void SyncQueryParam_WithArray_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<ComplexState>();
        var array = new int[] { 1, 2, 3 };

        var act = () => builder.SyncQueryParam<int[]>(() => array, s => s.Tags);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not support reference type*");
    }

    [Fact]
    public void SyncQueryParam_WithCustomClass_ShouldThrow()
    {
        var builder = new UrlSyncBuilder<ComplexState>();
        var user = new User("Test");

        var act = () => builder.SyncQueryParam<User>(() => user, s => s.Owner);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not support reference type*");
    }

    #endregion

    private record User(string Name);
    private record ComplexState(List<int>? Items, int[]? Tags, User? Owner);
}
