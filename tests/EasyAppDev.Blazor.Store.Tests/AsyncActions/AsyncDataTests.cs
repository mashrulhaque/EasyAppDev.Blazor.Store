using FluentAssertions;
using Xunit;
using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Tests.AsyncActions;

public class AsyncDataTests
{
    #region NotAsked State Tests

    [Fact]
    public void NotAsked_HasCorrectProperties()
    {
        // Act
        var data = AsyncData<string>.NotAsked();

        // Assert
        data.IsNotAsked.Should().BeTrue();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeFalse();
        data.Data.Should().BeNull();
        data.Error.Should().BeNull();
    }

    #endregion

    #region Loading State Tests

    [Fact]
    public void Loading_HasCorrectProperties()
    {
        // Act
        var data = AsyncData<string>.Loading();

        // Assert
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeTrue();
        data.IsFetching.Should().BeTrue();
        data.IsRefetching.Should().BeFalse();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeFalse();
        data.Data.Should().BeNull();
        data.Error.Should().BeNull();
    }

    #endregion

    #region Success State Tests

    [Fact]
    public void Success_WithData_HasCorrectProperties()
    {
        // Arrange
        const string testData = "test data";

        // Act
        var data = AsyncData<string>.Success(testData);

        // Assert
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeTrue();
        data.HasError.Should().BeFalse();
        data.Data.Should().Be(testData);
        data.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithNullData_AllowsNull()
    {
        // Act
        var data = AsyncData<string?>.Success(null);

        // Assert
        data.HasData.Should().BeTrue();
        data.Data.Should().BeNull();
    }

    [Fact]
    public void Success_WithComplexType_StoresCorrectly()
    {
        // Arrange
        var user = new User(1, "John Doe", "john@example.com");

        // Act
        var data = AsyncData<User>.Success(user);

        // Assert
        data.HasData.Should().BeTrue();
        data.Data.Should().Be(user);
        data.Data!.Id.Should().Be(1);
        data.Data.Name.Should().Be("John Doe");
    }

    #endregion

    #region Failure State Tests

    [Fact]
    public void Failure_WithError_HasCorrectProperties()
    {
        // Arrange
        const string errorMessage = "Something went wrong";

        // Act
        var data = AsyncData<string>.Failure(errorMessage);

        // Assert
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeTrue();
        data.Data.Should().BeNull();
        data.Error.Should().Be(errorMessage);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            AsyncData<string>.Failure(null!));
    }

    #endregion

    #region Transition Tests

    [Fact]
    public void ToLoading_FromNotAsked_TransitionsCorrectly()
    {
        // Arrange
        var data = AsyncData<string>.NotAsked();

        // Act
        var loading = data.ToLoading();

        // Assert
        loading.IsLoading.Should().BeTrue();
        loading.IsNotAsked.Should().BeFalse();
        loading.HasData.Should().BeFalse();
        loading.HasError.Should().BeFalse();
    }

    [Fact]
    public void ToSuccess_FromLoading_TransitionsCorrectly()
    {
        // Arrange
        var data = AsyncData<string>.Loading();
        const string result = "success data";

        // Act
        var success = data.ToSuccess(result);

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be(result);
        success.IsLoading.Should().BeFalse();
        success.HasError.Should().BeFalse();
    }

    [Fact]
    public void ToFailure_FromLoading_TransitionsCorrectly()
    {
        // Arrange
        var data = AsyncData<string>.Loading();
        const string error = "failed";

        // Act
        var failure = data.ToFailure(error);

        // Assert
        failure.HasError.Should().BeTrue();
        failure.Error.Should().Be(error);
        failure.IsLoading.Should().BeFalse();
        failure.HasData.Should().BeFalse();
    }

    [Fact]
    public void TransitionChain_NotAskedToLoadingToSuccess_Works()
    {
        // Arrange
        var data = AsyncData<int>.NotAsked();

        // Act
        var loading = data.ToLoading();
        var success = loading.ToSuccess(42);

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be(42);
    }

    [Fact]
    public void TransitionChain_NotAskedToLoadingToFailure_Works()
    {
        // Arrange
        var data = AsyncData<int>.NotAsked();

        // Act
        var loading = data.ToLoading();
        var failure = loading.ToFailure("error");

        // Assert
        failure.HasError.Should().BeTrue();
        failure.Error.Should().Be("error");
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void Transitions_CreateNewInstances()
    {
        // Arrange
        var original = AsyncData<string>.NotAsked();

        // Act
        var loading = original.ToLoading();
        var success = loading.ToSuccess("data");

        // Assert
        ReferenceEquals(original, loading).Should().BeFalse();
        ReferenceEquals(loading, success).Should().BeFalse();
        original.IsNotAsked.Should().BeTrue("because original should be unchanged");
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void AsyncData_WorksWithValueTypes()
    {
        // Act
        var data = AsyncData<int>.Success(42);

        // Assert
        data.HasData.Should().BeTrue();
        data.Data.Should().Be(42);
    }

    [Fact]
    public void AsyncData_WorksWithNullableValueTypes()
    {
        // Act
        var data = AsyncData<int?>.Success(null);

        // Assert
        data.HasData.Should().BeTrue();
        data.Data.Should().BeNull();
    }

    [Fact]
    public void AsyncData_WorksWithReferenceTypes()
    {
        // Arrange
        var user = new User(1, "Test", "test@example.com");

        // Act
        var data = AsyncData<User>.Success(user);

        // Assert
        data.HasData.Should().BeTrue();
        data.Data.Should().Be(user);
    }

    #endregion

    #region State Machine Validation Tests

    [Fact]
    public void NotAsked_OnlyNotAskedIsTrue()
    {
        // Act
        var data = AsyncData<string>.NotAsked();

        // Assert - Only one state should be active
        data.IsNotAsked.Should().BeTrue();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeFalse();
    }

    [Fact]
    public void Loading_OnlyLoadingIsTrue()
    {
        // Act
        var data = AsyncData<string>.Loading();

        // Assert - Only one state should be active
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeTrue();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeFalse();
    }

    [Fact]
    public void Success_OnlyHasDataIsTrue()
    {
        // Act
        var data = AsyncData<string>.Success("data");

        // Assert - Only one state should be active
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeTrue();
        data.HasError.Should().BeFalse();
    }

    [Fact]
    public void Failure_OnlyHasErrorIsTrue()
    {
        // Act
        var data = AsyncData<string>.Failure("error");

        // Assert - Only one state should be active
        data.IsNotAsked.Should().BeFalse();
        data.IsLoading.Should().BeFalse();
        data.HasData.Should().BeFalse();
        data.HasError.Should().BeTrue();
    }

    #endregion

    #region Value Equality Tests (Record Behavior)

    [Fact]
    public void AsyncData_ShouldHaveValueEquality_Success()
    {
        // Arrange
        var a = AsyncData<int>.Success(42);
        var b = AsyncData<int>.Success(42);

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AsyncData_ShouldHaveValueEquality_Loading()
    {
        // Arrange
        var a = AsyncData<int>.Loading();
        var b = AsyncData<int>.Loading();

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void AsyncData_ShouldHaveValueEquality_NotAsked()
    {
        // Arrange
        var a = AsyncData<int>.NotAsked();
        var b = AsyncData<int>.NotAsked();

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void AsyncData_ShouldHaveValueEquality_Failure()
    {
        // Arrange
        var a = AsyncData<int>.Failure("error");
        var b = AsyncData<int>.Failure("error");

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void AsyncData_DifferentStates_ShouldNotBeEqual()
    {
        // Arrange
        var loading = AsyncData<int>.Loading();
        var notAsked = AsyncData<int>.NotAsked();

        // Assert
        loading.Should().NotBe(notAsked);
        (loading == notAsked).Should().BeFalse();
    }

    [Fact]
    public void AsyncData_DifferentData_ShouldNotBeEqual()
    {
        // Arrange
        var a = AsyncData<int>.Success(42);
        var b = AsyncData<int>.Success(100);

        // Assert
        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }

    [Fact]
    public void AsyncData_DifferentErrors_ShouldNotBeEqual()
    {
        // Arrange
        var a = AsyncData<int>.Failure("error1");
        var b = AsyncData<int>.Failure("error2");

        // Assert
        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }

    #endregion

    #region IsFetching and IsRefetching Tests

    [Fact]
    public void IsFetching_Loading_IsTrue()
    {
        // Act
        var data = AsyncData<string>.Loading();

        // Assert
        data.IsFetching.Should().BeTrue();
    }

    [Fact]
    public void IsFetching_NotAsked_IsFalse()
    {
        // Act
        var data = AsyncData<string>.NotAsked();

        // Assert
        data.IsFetching.Should().BeFalse();
    }

    [Fact]
    public void IsFetching_Success_IsFalse()
    {
        // Act
        var data = AsyncData<string>.Success("data");

        // Assert
        data.IsFetching.Should().BeFalse();
    }

    [Fact]
    public void IsFetching_Failure_IsFalse()
    {
        // Act
        var data = AsyncData<string>.Failure("error");

        // Assert
        data.IsFetching.Should().BeFalse();
    }

    [Fact]
    public void IsRefetching_LoadingWithNoData_IsFalse()
    {
        // Act
        var data = AsyncData<string>.Loading();

        // Assert
        data.IsRefetching.Should().BeFalse();
    }

    [Fact]
    public void IsRefetching_FetchingWithData_IsTrue()
    {
        // Arrange
        var success = AsyncData<string>.Success("data");

        // Act
        var refetching = success.ToLoadingPreserved();

        // Assert
        refetching.IsRefetching.Should().BeTrue();
        refetching.IsFetching.Should().BeTrue();
        refetching.HasData.Should().BeTrue();
    }

    #endregion

    #region ToLoadingPreserved Tests

    [Fact]
    public void ToLoadingPreserved_FromSuccess_PreservesData()
    {
        // Arrange
        var success = AsyncData<string>.Success("existing data");

        // Act
        var preserved = success.ToLoadingPreserved();

        // Assert
        preserved.IsFetching.Should().BeTrue();
        preserved.IsLoading.Should().BeFalse("because data exists");
        preserved.IsRefetching.Should().BeTrue();
        preserved.HasData.Should().BeTrue();
        preserved.Data.Should().Be("existing data");
        preserved.HasError.Should().BeFalse();
        preserved.Error.Should().BeNull();
    }

    [Fact]
    public void ToLoadingPreserved_FromNotAsked_BehavesLikeLoading()
    {
        // Arrange
        var notAsked = AsyncData<string>.NotAsked();

        // Act
        var preserved = notAsked.ToLoadingPreserved();

        // Assert
        preserved.IsFetching.Should().BeTrue();
        preserved.IsLoading.Should().BeTrue("because no data exists");
        preserved.IsRefetching.Should().BeFalse();
        preserved.HasData.Should().BeFalse();
        preserved.Data.Should().BeNull();
    }

    [Fact]
    public void ToLoadingPreserved_FromFailure_ClearsError()
    {
        // Arrange
        var failure = AsyncData<string>.Failure("previous error");

        // Act
        var preserved = failure.ToLoadingPreserved();

        // Assert
        preserved.IsFetching.Should().BeTrue();
        preserved.IsLoading.Should().BeTrue("because no data exists");
        preserved.HasError.Should().BeFalse();
        preserved.Error.Should().BeNull();
    }

    [Fact]
    public void ToLoadingPreserved_FromSuccess_WithComplexType_PreservesData()
    {
        // Arrange
        var user = new User(1, "John", "john@example.com");
        var success = AsyncData<User>.Success(user);

        // Act
        var preserved = success.ToLoadingPreserved();

        // Assert
        preserved.IsRefetching.Should().BeTrue();
        preserved.Data.Should().Be(user);
        preserved.Data!.Name.Should().Be("John");
    }

    [Fact]
    public void ToLoadingPreserved_CreatesNewInstance()
    {
        // Arrange
        var original = AsyncData<string>.Success("data");

        // Act
        var preserved = original.ToLoadingPreserved();

        // Assert
        ReferenceEquals(original, preserved).Should().BeFalse();
        original.IsFetching.Should().BeFalse("because original should be unchanged");
        original.IsRefetching.Should().BeFalse();
    }

    [Fact]
    public void ToLoadingPreserved_ThenToSuccess_UpdatesDataCorrectly()
    {
        // Arrange
        var initial = AsyncData<string>.Success("old data");
        var refetching = initial.ToLoadingPreserved();

        // Act
        var updated = refetching.ToSuccess("new data");

        // Assert
        updated.Data.Should().Be("new data");
        updated.IsFetching.Should().BeFalse();
        updated.IsRefetching.Should().BeFalse();
    }

    [Fact]
    public void ToLoadingPreserved_ThenToFailure_ClearsDataAndSetsError()
    {
        // Arrange
        var initial = AsyncData<string>.Success("data");
        var refetching = initial.ToLoadingPreserved();

        // Act
        var failed = refetching.ToFailure("fetch failed");

        // Assert
        failed.HasError.Should().BeTrue();
        failed.Error.Should().Be("fetch failed");
        failed.HasData.Should().BeFalse();
        failed.Data.Should().BeNull();
    }

    #endregion

    #region With Expression Tests (Record Behavior)

    [Fact]
    public void AsyncData_WithExpression_ShouldCreateNewInstance()
    {
        // Arrange
        var loading = AsyncData<int>.Loading();

        // Act - Using with expression to modify record
        var success = loading with { HasData = true, Data = 42, IsLoading = false };

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be(42);
        success.IsLoading.Should().BeFalse();

        // Original should be unchanged
        loading.IsLoading.Should().BeTrue();
        loading.HasData.Should().BeFalse();
    }

    [Fact]
    public void AsyncData_WithExpression_ShouldPreserveUnchangedProperties()
    {
        // Arrange
        var failure = AsyncData<string>.Failure("original error");

        // Act - Only change error message
        var updated = failure with { Error = "updated error" };

        // Assert
        updated.HasError.Should().BeTrue();
        updated.Error.Should().Be("updated error");
        updated.IsLoading.Should().BeFalse();
        updated.IsNotAsked.Should().BeFalse();
        updated.HasData.Should().BeFalse();
    }

    [Fact]
    public void AsyncData_WithExpression_ShouldNotMutateOriginal()
    {
        // Arrange
        var original = AsyncData<int>.Success(100);

        // Act
        var modified = original with { Data = 200 };

        // Assert
        original.Data.Should().Be(100);
        modified.Data.Should().Be(200);
        ReferenceEquals(original, modified).Should().BeFalse();
    }

    #endregion

    // Test helper record
    private record User(int Id, string Name, string Email);
}
