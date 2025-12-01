// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Configuration options for a mutation operation.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the mutation.</typeparam>
/// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
public class MutationOptions<TResult, TVariables>
{
    /// <summary>
    /// Gets or sets the function that performs the mutation.
    /// </summary>
    public required Func<TVariables, CancellationToken, Task<TResult?>> MutationFn { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation succeeds.
    /// Receives the result and the variables used.
    /// </summary>
    public Action<TResult, TVariables>? OnSuccess { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation fails.
    /// Receives the error and the variables used.
    /// </summary>
    public Action<Exception, TVariables>? OnError { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation settles (success or error).
    /// </summary>
    public Action<TVariables>? OnSettled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked before the mutation starts.
    /// Can be used for optimistic updates.
    /// </summary>
    public Action<TVariables>? OnMutate { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts for failed mutations.
    /// Default is 0 (no retries).
    /// </summary>
    public int Retry { get; set; } = 0;

    /// <summary>
    /// Gets or sets the delay function between retries.
    /// Takes the attempt number (0-based) and returns the delay.
    /// </summary>
    public Func<int, TimeSpan> RetryDelay { get; set; } = attempt =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>
    /// Creates mutation options with a simple mutation function.
    /// </summary>
    public static MutationOptions<TResult, TVariables> Create(
        Func<TVariables, Task<TResult?>> mutationFn) => new()
    {
        MutationFn = (vars, _) => mutationFn(vars)
    };

    /// <summary>
    /// Creates mutation options with a cancellable mutation function.
    /// </summary>
    public static MutationOptions<TResult, TVariables> Create(
        Func<TVariables, CancellationToken, Task<TResult?>> mutationFn) => new()
    {
        MutationFn = mutationFn
    };
}

/// <summary>
/// Configuration options for a mutation that doesn't return a result.
/// </summary>
/// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
public class MutationOptions<TVariables>
{
    /// <summary>
    /// Gets or sets the function that performs the mutation.
    /// </summary>
    public required Func<TVariables, CancellationToken, Task> MutationFn { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation succeeds.
    /// </summary>
    public Action<TVariables>? OnSuccess { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation fails.
    /// </summary>
    public Action<Exception, TVariables>? OnError { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the mutation settles.
    /// </summary>
    public Action<TVariables>? OnSettled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked before the mutation starts.
    /// </summary>
    public Action<TVariables>? OnMutate { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// Default is 0.
    /// </summary>
    public int Retry { get; set; } = 0;

    /// <summary>
    /// Gets or sets the retry delay function.
    /// </summary>
    public Func<int, TimeSpan> RetryDelay { get; set; } = attempt =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>
    /// Creates mutation options with a simple mutation function.
    /// </summary>
    public static MutationOptions<TVariables> Create(
        Func<TVariables, Task> mutationFn) => new()
    {
        MutationFn = (vars, _) => mutationFn(vars)
    };
}
