// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Query;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Query;

public class MutationTests
{
    [Fact]
    public void Mutation_InitialState_ShouldBeIdle()
    {
        // Arrange
        var client = new QueryClient();
        var options = MutationOptions<string, string>.Create(v => Task.FromResult<string?>(v.ToUpper()));

        // Act
        var mutation = new Mutation<string, string>(options, client, () => { });

        // Assert
        mutation.Status.Should().Be(MutationStatus.Idle);
        mutation.IsIdle.Should().BeTrue();
        mutation.Data.Should().BeNull();
    }

    [Fact]
    public async Task Mutation_MutateAsync_ShouldExecuteAndReturnResult()
    {
        // Arrange
        var client = new QueryClient();
        var options = MutationOptions<string, string>.Create(v => Task.FromResult<string?>(v.ToUpper()));
        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        var result = await mutation.MutateAsync("hello");

        // Assert
        result.Should().Be("HELLO");
        mutation.Data.Should().Be("HELLO");
        mutation.IsSuccess.Should().BeTrue();
        mutation.Variables.Should().Be("hello");
    }

    [Fact]
    public async Task Mutation_MutateAsync_ShouldSetLoadingState()
    {
        // Arrange
        var client = new QueryClient();
        var loadingObserved = false;
        Mutation<string, string>? mutation = null;

        var options = new MutationOptions<string, string>
        {
            MutationFn = async (v, _) =>
            {
                await Task.Delay(50);
                return v.ToUpper();
            }
        };

        mutation = new Mutation<string, string>(options, client, () =>
        {
            if (mutation!.IsLoading) loadingObserved = true;
        });

        // Act
        await mutation.MutateAsync("test");

        // Assert
        loadingObserved.Should().BeTrue();
        mutation.IsLoading.Should().BeFalse(); // After completion
    }

    [Fact]
    public async Task Mutation_OnMutate_ShouldBeCalledBeforeExecution()
    {
        // Arrange
        var client = new QueryClient();
        string? mutateVariable = null;

        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) => Task.FromResult<string?>(v),
            OnMutate = v => mutateVariable = v
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        await mutation.MutateAsync("input");

        // Assert
        mutateVariable.Should().Be("input");
    }

    [Fact]
    public async Task Mutation_OnSuccess_ShouldBeCalledWithResultAndVariables()
    {
        // Arrange
        var client = new QueryClient();
        string? successResult = null;
        string? successVariable = null;

        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) => Task.FromResult<string?>(v.ToUpper()),
            OnSuccess = (result, variable) =>
            {
                successResult = result;
                successVariable = variable;
            }
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        await mutation.MutateAsync("test");

        // Assert
        successResult.Should().Be("TEST");
        successVariable.Should().Be("test");
    }

    [Fact]
    public async Task Mutation_OnError_ShouldBeCalledOnFailure()
    {
        // Arrange
        var client = new QueryClient();
        Exception? errorException = null;
        string? errorVariable = null;

        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) => throw new InvalidOperationException("Test error"),
            OnError = (ex, v) =>
            {
                errorException = ex;
                errorVariable = v;
            }
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.MutateAsync("input"));

        // Assert
        errorException.Should().NotBeNull();
        errorException.Should().BeOfType<InvalidOperationException>();
        errorVariable.Should().Be("input");
        mutation.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Mutation_MutateSafeAsync_ShouldNotThrowOnError()
    {
        // Arrange
        var client = new QueryClient();
        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) => throw new InvalidOperationException("Test error")
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        var result = await mutation.MutateSafeAsync("input");

        // Assert
        result.Should().BeNull();
        mutation.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Mutation_Reset_ShouldReturnToIdleState()
    {
        // Arrange
        var client = new QueryClient();
        var options = MutationOptions<string, string>.Create(v => Task.FromResult<string?>(v));
        var mutation = new Mutation<string, string>(options, client, () => { });

        // Set up state
        await mutation.MutateAsync("test");

        // Act
        mutation.Reset();

        // Assert
        mutation.Status.Should().Be(MutationStatus.Idle);
        mutation.Data.Should().BeNull();
        mutation.Error.Should().BeNull();
        mutation.Variables.Should().BeNull();
    }

    [Fact]
    public async Task Mutation_SubmittedAt_ShouldBeSetOnMutate()
    {
        // Arrange
        var client = new QueryClient();
        var beforeMutate = DateTime.UtcNow;
        var options = MutationOptions<string, string>.Create(v => Task.FromResult<string?>(v));
        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        await mutation.MutateAsync("test");
        var afterMutate = DateTime.UtcNow;

        // Assert
        mutation.SubmittedAt.Should().NotBeNull();
        mutation.SubmittedAt.Should().BeOnOrAfter(beforeMutate);
        mutation.SubmittedAt.Should().BeOnOrBefore(afterMutate);
    }

    [Fact]
    public async Task Mutation_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        var client = new QueryClient();
        var attempts = 0;

        var options = new MutationOptions<string, string>
        {
            MutationFn = (v, _) =>
            {
                attempts++;
                if (attempts < 2)
                    throw new Exception("Retry needed");
                return Task.FromResult<string?>(v.ToUpper());
            },
            Retry = 2,
            RetryDelay = _ => TimeSpan.FromMilliseconds(1)
        };

        var mutation = new Mutation<string, string>(options, client, () => { });

        // Act
        var result = await mutation.MutateAsync("test");

        // Assert
        attempts.Should().Be(2);
        result.Should().Be("TEST");
    }

    [Fact]
    public async Task VoidMutation_MutateAsync_ShouldExecute()
    {
        // Arrange
        var client = new QueryClient();
        var executed = false;

        var options = new MutationOptions<string>
        {
            MutationFn = (v, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            }
        };

        var mutation = new Mutation<string>(options, client, () => { });

        // Act
        await mutation.MutateAsync("input");

        // Assert
        executed.Should().BeTrue();
        mutation.IsSuccess.Should().BeTrue();
    }
}
