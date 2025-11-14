// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Extension methods for AsyncData&lt;T&gt; to provide fluent transitions.
/// </summary>
public static class AsyncDataExtensions
{
    /// <summary>
    /// Transitions the AsyncData to the Loading state.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="self">The current AsyncData.</param>
    /// <returns>A new AsyncData in the Loading state.</returns>
    /// <example>
    /// <code>
    /// // In state method
    /// return this with { User = this.User.ToLoading() };
    /// </code>
    /// </example>
    public static AsyncData<T> ToLoading<T>(this AsyncData<T> self)
        => AsyncData<T>.Loading();

    /// <summary>
    /// Transitions the AsyncData to the Success state with data.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="self">The current AsyncData.</param>
    /// <param name="data">The data from the successful operation.</param>
    /// <returns>A new AsyncData in the Success state.</returns>
    /// <example>
    /// <code>
    /// // In state method
    /// return this with { User = this.User.ToSuccess(userData) };
    /// </code>
    /// </example>
    public static AsyncData<T> ToSuccess<T>(this AsyncData<T> self, T data)
        => AsyncData<T>.Success(data);

    /// <summary>
    /// Transitions the AsyncData to the Failure state with an error.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="self">The current AsyncData.</param>
    /// <param name="error">The error message from the failed operation.</param>
    /// <returns>A new AsyncData in the Failure state.</returns>
    /// <example>
    /// <code>
    /// // In state method
    /// return this with { User = this.User.ToFailure("User not found") };
    /// </code>
    /// </example>
    public static AsyncData<T> ToFailure<T>(this AsyncData<T> self, string error)
        => AsyncData<T>.Failure(error);
}
