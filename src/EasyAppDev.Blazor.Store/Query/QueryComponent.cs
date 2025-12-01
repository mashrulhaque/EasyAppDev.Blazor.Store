// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Components;

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Base component for using queries and mutations with automatic lifecycle management.
/// </summary>
public abstract class QueryComponent : ComponentBase, IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private bool _disposed;

    /// <summary>
    /// Gets the query client for cache operations.
    /// </summary>
    [Inject]
    protected IQueryClient QueryClient { get; set; } = default!;

    /// <summary>
    /// Creates and manages a query with the specified options.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the query.</typeparam>
    /// <param name="options">The query configuration options.</param>
    /// <returns>A managed query instance.</returns>
    protected Query<T> UseQuery<T>(QueryOptions<T> options)
    {
        var query = new Query<T>(options, QueryClient, () => InvokeAsync(StateHasChanged));
        _disposables.Add(query);
        return query;
    }

    /// <summary>
    /// Creates and manages a query with a key and query function.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the query.</typeparam>
    /// <param name="key">The unique query key.</param>
    /// <param name="queryFn">The function that fetches the data.</param>
    /// <returns>A managed query instance.</returns>
    protected Query<T> UseQuery<T>(string key, Func<Task<T?>> queryFn)
    {
        return UseQuery(QueryOptions<T>.Create(key, queryFn));
    }

    /// <summary>
    /// Creates and manages a query with a key and cancellable query function.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the query.</typeparam>
    /// <param name="key">The unique query key.</param>
    /// <param name="queryFn">The cancellable function that fetches the data.</param>
    /// <returns>A managed query instance.</returns>
    protected Query<T> UseQuery<T>(string key, Func<CancellationToken, Task<T?>> queryFn)
    {
        return UseQuery(QueryOptions<T>.Create(key, queryFn));
    }

    /// <summary>
    /// Creates and manages a mutation with the specified options.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the mutation.</typeparam>
    /// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
    /// <param name="options">The mutation configuration options.</param>
    /// <returns>A managed mutation instance.</returns>
    protected Mutation<TResult, TVariables> UseMutation<TResult, TVariables>(
        MutationOptions<TResult, TVariables> options)
    {
        var mutation = new Mutation<TResult, TVariables>(
            options,
            QueryClient,
            () => InvokeAsync(StateHasChanged));
        _disposables.Add(mutation);
        return mutation;
    }

    /// <summary>
    /// Creates and manages a mutation with a simple mutation function.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the mutation.</typeparam>
    /// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
    /// <param name="mutationFn">The function that performs the mutation.</param>
    /// <returns>A managed mutation instance.</returns>
    protected Mutation<TResult, TVariables> UseMutation<TResult, TVariables>(
        Func<TVariables, Task<TResult?>> mutationFn)
    {
        return UseMutation(MutationOptions<TResult, TVariables>.Create(mutationFn));
    }

    /// <summary>
    /// Creates and manages a void mutation with the specified options.
    /// </summary>
    /// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
    /// <param name="options">The mutation configuration options.</param>
    /// <returns>A managed mutation instance.</returns>
    protected Mutation<TVariables> UseMutation<TVariables>(MutationOptions<TVariables> options)
    {
        var mutation = new Mutation<TVariables>(
            options,
            QueryClient,
            () => InvokeAsync(StateHasChanged));
        _disposables.Add(mutation);
        return mutation;
    }

    /// <summary>
    /// Creates and manages a void mutation with a simple mutation function.
    /// </summary>
    /// <typeparam name="TVariables">The type of variables passed to the mutation.</typeparam>
    /// <param name="mutationFn">The function that performs the mutation.</param>
    /// <returns>A managed mutation instance.</returns>
    protected Mutation<TVariables> UseMutation<TVariables>(Func<TVariables, Task> mutationFn)
    {
        return UseMutation(MutationOptions<TVariables>.Create(mutationFn));
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Initialize all queries
        foreach (var disposable in _disposables)
        {
            if (disposable is IQueryInitializable initializable)
            {
                await initializable.InitializeAsync();
            }
        }
    }

    /// <summary>
    /// Disposes all managed queries and mutations.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();
        }
    }
}

/// <summary>
/// Internal interface for query initialization.
/// </summary>
internal interface IQueryInitializable
{
    Task InitializeAsync();
}
