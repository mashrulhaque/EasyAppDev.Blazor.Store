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

    // Test helper record
    private record User(int Id, string Name, string Email);
}
