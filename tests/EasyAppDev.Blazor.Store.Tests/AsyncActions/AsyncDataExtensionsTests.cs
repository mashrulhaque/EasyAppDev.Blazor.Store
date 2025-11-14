using FluentAssertions;
using Xunit;
using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Tests.AsyncActions;

public class AsyncDataExtensionsTests
{
    [Fact]
    public void ToLoading_Extension_CreatesLoadingState()
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
    public void ToSuccess_Extension_CreatesSuccessState()
    {
        // Arrange
        var data = AsyncData<string>.Loading();

        // Act
        var success = data.ToSuccess("result");

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be("result");
        success.IsLoading.Should().BeFalse();
        success.IsNotAsked.Should().BeFalse();
        success.HasError.Should().BeFalse();
    }

    [Fact]
    public void ToFailure_Extension_CreatesFailureState()
    {
        // Arrange
        var data = AsyncData<string>.Loading();

        // Act
        var failure = data.ToFailure("error");

        // Assert
        failure.HasError.Should().BeTrue();
        failure.Error.Should().Be("error");
        failure.IsLoading.Should().BeFalse();
        failure.IsNotAsked.Should().BeFalse();
        failure.HasData.Should().BeFalse();
    }

    [Fact]
    public void Extensions_WorkInWithExpression()
    {
        // Arrange
        var state = new TestState(AsyncData<string>.NotAsked());

        // Act
        var loadingState = state with { Data = state.Data.ToLoading() };
        var successState = loadingState with { Data = loadingState.Data.ToSuccess("test") };

        // Assert
        loadingState.Data.IsLoading.Should().BeTrue();
        successState.Data.HasData.Should().BeTrue();
        successState.Data.Data.Should().Be("test");
    }

    [Fact]
    public void Extensions_CanChainTransitions()
    {
        // Arrange
        var initialData = AsyncData<int>.NotAsked();

        // Act - Chain multiple transitions
        var loading = initialData.ToLoading();
        var success = loading.ToSuccess(42);
        var reloading = success.ToLoading();
        var newSuccess = reloading.ToSuccess(100);

        // Assert
        newSuccess.HasData.Should().BeTrue();
        newSuccess.Data.Should().Be(100);
        newSuccess.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Extensions_WorkWithValueTypes()
    {
        // Arrange
        var data = AsyncData<int>.Loading();

        // Act
        var success = data.ToSuccess(42);

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be(42);
    }

    [Fact]
    public void Extensions_WorkWithNullableTypes()
    {
        // Arrange
        var data = AsyncData<string?>.Loading();

        // Act
        var success = data.ToSuccess(null);

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().BeNull();
    }

    [Fact]
    public void Extensions_WorkWithComplexTypes()
    {
        // Arrange
        var data = AsyncData<User>.Loading();
        var user = new User(1, "Jane", "jane@example.com");

        // Act
        var success = data.ToSuccess(user);

        // Assert
        success.HasData.Should().BeTrue();
        success.Data.Should().Be(user);
        success.Data!.Name.Should().Be("Jane");
    }

    [Fact]
    public void ToLoading_FromAnyState_CreatesLoadingState()
    {
        // Arrange
        var notAsked = AsyncData<string>.NotAsked();
        var success = AsyncData<string>.Success("data");
        var failure = AsyncData<string>.Failure("error");

        // Act
        var loadingFromNotAsked = notAsked.ToLoading();
        var loadingFromSuccess = success.ToLoading();
        var loadingFromFailure = failure.ToLoading();

        // Assert
        loadingFromNotAsked.IsLoading.Should().BeTrue();
        loadingFromSuccess.IsLoading.Should().BeTrue();
        loadingFromFailure.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void ToSuccess_FromAnyState_CreatesSuccessState()
    {
        // Arrange
        var notAsked = AsyncData<string>.NotAsked();
        var loading = AsyncData<string>.Loading();
        var failure = AsyncData<string>.Failure("error");

        // Act
        var successFromNotAsked = notAsked.ToSuccess("data");
        var successFromLoading = loading.ToSuccess("data");
        var successFromFailure = failure.ToSuccess("data");

        // Assert
        successFromNotAsked.HasData.Should().BeTrue();
        successFromLoading.HasData.Should().BeTrue();
        successFromFailure.HasData.Should().BeTrue();
    }

    [Fact]
    public void ToFailure_FromAnyState_CreatesFailureState()
    {
        // Arrange
        var notAsked = AsyncData<string>.NotAsked();
        var loading = AsyncData<string>.Loading();
        var success = AsyncData<string>.Success("data");

        // Act
        var failureFromNotAsked = notAsked.ToFailure("error");
        var failureFromLoading = loading.ToFailure("error");
        var failureFromSuccess = success.ToFailure("error");

        // Assert
        failureFromNotAsked.HasError.Should().BeTrue();
        failureFromLoading.HasError.Should().BeTrue();
        failureFromSuccess.HasError.Should().BeTrue();
    }

    [Fact]
    public void Extensions_PreserveImmutability()
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
        loading.IsLoading.Should().BeTrue("because loading should be unchanged");
    }

    private record TestState(AsyncData<string> Data);
    private record User(int Id, string Name, string Email);
}
