// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Represents the state of an asynchronous operation (NotAsked, Loading, Success, or Failure).
/// </summary>
/// <typeparam name="T">The type of data when the operation succeeds.</typeparam>
/// <example>
/// <code>
/// public record UserState(AsyncData&lt;User&gt; User);
///
/// @if (State.User.IsLoading)
/// {
///     &lt;p&gt;Loading...&lt;/p&gt;
/// }
/// else if (State.User.HasData)
/// {
///     &lt;p&gt;Welcome, @State.User.Data.Name!&lt;/p&gt;
/// }
/// </code>
/// </example>
/// <remarks>
/// This is a record type providing value equality semantics. Two AsyncData instances
/// are equal if all their properties have the same values. Use <c>with</c> expressions
/// for creating modified copies.
/// </remarks>
public record AsyncData<T>
{
    /// <summary>
    /// Gets the data if the operation succeeded, otherwise null.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation is currently loading.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has not been requested yet.
    /// </summary>
    public bool IsNotAsked { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded and has data.
    /// </summary>
    public bool HasData { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation failed with an error.
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed, otherwise null.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether a fetch operation is in progress.
    /// Unlike <see cref="IsLoading"/>, this is true during both initial loads and background refetches.
    /// Use this to show loading indicators while preserving existing data.
    /// </summary>
    public bool IsFetching { get; init; }

    /// <summary>
    /// Gets a value indicating whether a background refetch is in progress while data is available.
    /// True when both <see cref="HasData"/> and <see cref="IsFetching"/> are true.
    /// Use this to show a loading overlay on existing content.
    /// </summary>
    public bool IsRefetching => HasData && IsFetching;

    private AsyncData() { }

    /// <summary>
    /// Creates an AsyncData in the NotAsked state.
    /// </summary>
    /// <returns>A new AsyncData representing no request has been made.</returns>
    public static AsyncData<T> NotAsked() => new()
    {
        IsNotAsked = true,
        IsLoading = false,
        HasData = false,
        HasError = false,
        Data = default,
        Error = null
    };

    /// <summary>
    /// Creates an AsyncData in the Loading state.
    /// </summary>
    /// <returns>A new AsyncData representing a request in progress.</returns>
    public static AsyncData<T> Loading() => new()
    {
        IsNotAsked = false,
        IsLoading = true,
        IsFetching = true,
        HasData = false,
        HasError = false,
        Data = default,
        Error = null
    };

    /// <summary>
    /// Creates an AsyncData in the Success state with data.
    /// </summary>
    /// <param name="data">The data from the successful operation.</param>
    /// <returns>A new AsyncData representing successful completion.</returns>
    public static AsyncData<T> Success(T data) => new()
    {
        IsNotAsked = false,
        IsLoading = false,
        HasData = true,
        HasError = false,
        Data = data,
        Error = null
    };

    /// <summary>
    /// Creates an AsyncData in the Failure state with an error message.
    /// </summary>
    /// <param name="error">The error message from the failed operation.</param>
    /// <returns>A new AsyncData representing a failed operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
    public static AsyncData<T> Failure(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new()
        {
            IsNotAsked = false,
            IsLoading = false,
            HasData = false,
            HasError = true,
            Data = default,
            Error = error
        };
    }

    /// <summary>
    /// Transitions this AsyncData to the Loading state.
    /// </summary>
    /// <returns>A new AsyncData in the Loading state.</returns>
    public AsyncData<T> ToLoading() => Loading();

    /// <summary>
    /// Transitions to a loading state while preserving existing data.
    /// Use this for background refetches where you want to show a loading indicator
    /// without clearing the current content (stale-while-revalidate pattern).
    /// </summary>
    /// <returns>
    /// A new AsyncData with <see cref="IsFetching"/> set to true.
    /// If data exists, <see cref="HasData"/> and <see cref="Data"/> are preserved,
    /// and <see cref="IsRefetching"/> will be true.
    /// If no data exists, behaves like <see cref="ToLoading"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// // In a component updating existing data:
    /// await Update(s => s with { User = s.User.ToLoadingPreserved() });
    /// // User.IsRefetching is true, User.Data still available for display
    /// </code>
    /// </example>
    public AsyncData<T> ToLoadingPreserved() => new()
    {
        IsNotAsked = false,
        IsLoading = !HasData,    // Only "loading" if no prior data
        IsFetching = true,       // Always fetching
        HasData = HasData,       // Preserve
        HasError = false,
        Data = Data,             // Preserve
        Error = null
    };

    /// <summary>
    /// Transitions this AsyncData to the Success state with data.
    /// </summary>
    /// <param name="data">The data from the successful operation.</param>
    /// <returns>A new AsyncData in the Success state.</returns>
    public AsyncData<T> ToSuccess(T data) => Success(data);

    /// <summary>
    /// Transitions this AsyncData to the Failure state with an error.
    /// </summary>
    /// <param name="error">The error message from the failed operation.</param>
    /// <returns>A new AsyncData in the Failure state.</returns>
    public AsyncData<T> ToFailure(string error) => Failure(error);
}
