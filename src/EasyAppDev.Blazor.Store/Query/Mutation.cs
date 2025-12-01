// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Represents a mutation that can modify server state.
/// Inspired by TanStack Query's mutation pattern.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the mutation.</typeparam>
/// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
public sealed class Mutation<TResult, TVariables> : IDisposable
{
    private readonly MutationOptions<TResult, TVariables> _options;
    private readonly IQueryClient _queryClient;
    private readonly Action _onStateChange;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _mutationCts;
    private bool _disposed;

    private TResult? _data;
    private Exception? _error;
    private TVariables? _variables;
    private MutationStatus _status = MutationStatus.Idle;
    private int _failureCount;
    private DateTime? _submittedAt;

    internal Mutation(
        MutationOptions<TResult, TVariables> options,
        IQueryClient queryClient,
        Action onStateChange)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _onStateChange = onStateChange ?? throw new ArgumentNullException(nameof(onStateChange));
    }

    /// <summary>
    /// Gets the result data if the mutation succeeded.
    /// </summary>
    public TResult? Data => _data;

    /// <summary>
    /// Gets the error if the mutation failed.
    /// </summary>
    public Exception? Error => _error;

    /// <summary>
    /// Gets the variables from the last mutation call.
    /// </summary>
    public TVariables? Variables => _variables;

    /// <summary>
    /// Gets the current status of the mutation.
    /// </summary>
    public MutationStatus Status => _status;

    /// <summary>
    /// Gets whether the mutation is currently loading.
    /// </summary>
    public bool IsLoading => _status == MutationStatus.Loading;

    /// <summary>
    /// Gets whether the mutation has an error.
    /// </summary>
    public bool IsError => _status == MutationStatus.Error;

    /// <summary>
    /// Gets whether the mutation was successful.
    /// </summary>
    public bool IsSuccess => _status == MutationStatus.Success;

    /// <summary>
    /// Gets whether the mutation is idle (not started or reset).
    /// </summary>
    public bool IsIdle => _status == MutationStatus.Idle;

    /// <summary>
    /// Gets the number of consecutive failures.
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// Gets when the mutation was last submitted.
    /// </summary>
    public DateTime? SubmittedAt => _submittedAt;

    /// <summary>
    /// Gets the query client for cache operations.
    /// </summary>
    public IQueryClient QueryClient => _queryClient;

    /// <summary>
    /// Executes the mutation with the given variables.
    /// </summary>
    /// <param name="variables">The variables to pass to the mutation function.</param>
    /// <returns>The result of the mutation.</returns>
    public async Task<TResult?> MutateAsync(TVariables variables)
    {
        if (_disposed) return default;

        // Cancel any existing mutation
        _mutationCts?.Cancel();
        _mutationCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

        _variables = variables;
        _submittedAt = DateTime.UtcNow;
        _status = MutationStatus.Loading;
        _error = null;
        _failureCount = 0;

        _options.OnMutate?.Invoke(variables);
        NotifyStateChange();

        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _options.Retry)
        {
            try
            {
                var result = await _options.MutationFn(variables, _mutationCts.Token);

                if (_disposed || _mutationCts.Token.IsCancellationRequested)
                    return default;

                _data = result;
                _status = MutationStatus.Success;
                _error = null;

                _options.OnSuccess?.Invoke(result!, variables);
                _options.OnSettled?.Invoke(variables);
                NotifyStateChange();
                return result;
            }
            catch (OperationCanceledException) when (_mutationCts.Token.IsCancellationRequested)
            {
                return default;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                _failureCount++;

                if (attempt <= _options.Retry)
                {
                    var delay = _options.RetryDelay(attempt - 1);
                    try
                    {
                        await Task.Delay(delay, _mutationCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return default;
                    }
                }
            }
        }

        // All retries exhausted
        if (_disposed) return default;

        _error = lastException;
        _status = MutationStatus.Error;

        _options.OnError?.Invoke(lastException!, variables);
        _options.OnSettled?.Invoke(variables);
        NotifyStateChange();

        throw lastException!;
    }

    /// <summary>
    /// Executes the mutation, ignoring exceptions.
    /// Returns the result if successful, default if failed.
    /// </summary>
    public async Task<TResult?> MutateSafeAsync(TVariables variables)
    {
        try
        {
            return await MutateAsync(variables);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Resets the mutation to its initial state.
    /// </summary>
    public void Reset()
    {
        if (_disposed) return;

        _mutationCts?.Cancel();
        _data = default;
        _error = null;
        _variables = default;
        _status = MutationStatus.Idle;
        _failureCount = 0;
        _submittedAt = null;

        NotifyStateChange();
    }

    private void NotifyStateChange()
    {
        if (!_disposed)
        {
            _onStateChange();
        }
    }

    /// <summary>
    /// Disposes the mutation and cancels any pending operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mutationCts?.Cancel();
        _mutationCts?.Dispose();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}

/// <summary>
/// Represents a mutation that doesn't return a result.
/// </summary>
/// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
public sealed class Mutation<TVariables> : IDisposable
{
    private readonly MutationOptions<TVariables> _options;
    private readonly IQueryClient _queryClient;
    private readonly Action _onStateChange;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _mutationCts;
    private bool _disposed;

    private Exception? _error;
    private TVariables? _variables;
    private MutationStatus _status = MutationStatus.Idle;
    private int _failureCount;
    private DateTime? _submittedAt;

    internal Mutation(
        MutationOptions<TVariables> options,
        IQueryClient queryClient,
        Action onStateChange)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _onStateChange = onStateChange ?? throw new ArgumentNullException(nameof(onStateChange));
    }

    /// <summary>
    /// Gets the error if the mutation failed.
    /// </summary>
    public Exception? Error => _error;

    /// <summary>
    /// Gets the variables from the last mutation call.
    /// </summary>
    public TVariables? Variables => _variables;

    /// <summary>
    /// Gets the current status of the mutation.
    /// </summary>
    public MutationStatus Status => _status;

    /// <summary>
    /// Gets whether the mutation is currently loading.
    /// </summary>
    public bool IsLoading => _status == MutationStatus.Loading;

    /// <summary>
    /// Gets whether the mutation has an error.
    /// </summary>
    public bool IsError => _status == MutationStatus.Error;

    /// <summary>
    /// Gets whether the mutation was successful.
    /// </summary>
    public bool IsSuccess => _status == MutationStatus.Success;

    /// <summary>
    /// Gets whether the mutation is idle.
    /// </summary>
    public bool IsIdle => _status == MutationStatus.Idle;

    /// <summary>
    /// Gets the number of consecutive failures.
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// Gets when the mutation was last submitted.
    /// </summary>
    public DateTime? SubmittedAt => _submittedAt;

    /// <summary>
    /// Gets the query client for cache operations.
    /// </summary>
    public IQueryClient QueryClient => _queryClient;

    /// <summary>
    /// Executes the mutation with the given variables.
    /// </summary>
    public async Task MutateAsync(TVariables variables)
    {
        if (_disposed) return;

        _mutationCts?.Cancel();
        _mutationCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

        _variables = variables;
        _submittedAt = DateTime.UtcNow;
        _status = MutationStatus.Loading;
        _error = null;
        _failureCount = 0;

        _options.OnMutate?.Invoke(variables);
        NotifyStateChange();

        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _options.Retry)
        {
            try
            {
                await _options.MutationFn(variables, _mutationCts.Token);

                if (_disposed || _mutationCts.Token.IsCancellationRequested)
                    return;

                _status = MutationStatus.Success;
                _error = null;

                _options.OnSuccess?.Invoke(variables);
                _options.OnSettled?.Invoke(variables);
                NotifyStateChange();
                return;
            }
            catch (OperationCanceledException) when (_mutationCts.Token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                _failureCount++;

                if (attempt <= _options.Retry)
                {
                    var delay = _options.RetryDelay(attempt - 1);
                    try
                    {
                        await Task.Delay(delay, _mutationCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        if (_disposed) return;

        _error = lastException;
        _status = MutationStatus.Error;

        _options.OnError?.Invoke(lastException!, variables);
        _options.OnSettled?.Invoke(variables);
        NotifyStateChange();

        throw lastException!;
    }

    /// <summary>
    /// Executes the mutation, ignoring exceptions.
    /// </summary>
    public async Task MutateSafeAsync(TVariables variables)
    {
        try
        {
            await MutateAsync(variables);
        }
        catch
        {
            // Swallowed intentionally
        }
    }

    /// <summary>
    /// Resets the mutation to its initial state.
    /// </summary>
    public void Reset()
    {
        if (_disposed) return;

        _mutationCts?.Cancel();
        _error = null;
        _variables = default;
        _status = MutationStatus.Idle;
        _failureCount = 0;
        _submittedAt = null;

        NotifyStateChange();
    }

    private void NotifyStateChange()
    {
        if (!_disposed)
        {
            _onStateChange();
        }
    }

    /// <summary>
    /// Disposes the mutation and cancels any pending operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mutationCts?.Cancel();
        _mutationCts?.Dispose();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
