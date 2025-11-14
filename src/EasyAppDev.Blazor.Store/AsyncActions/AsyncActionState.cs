namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Represents the state of an asynchronous action.
/// </summary>
/// <typeparam name="TResult">The type of data returned by the async action.</typeparam>
public record AsyncActionState<TResult>
{
    /// <summary>
    /// Gets a value indicating whether the action is currently loading.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// Gets the data result from the action.
    /// </summary>
    public TResult? Data { get; init; }

    /// <summary>
    /// Gets the error message if the action failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the timestamp of the last update.
    /// </summary>
    public DateTime? LastUpdated { get; init; }

    /// <summary>
    /// Gets a value indicating whether the action has succeeded.
    /// </summary>
    public bool IsSuccess => !IsLoading && Error == null && Data != null;

    /// <summary>
    /// Gets a value indicating whether the action has failed.
    /// </summary>
    public bool IsError => !IsLoading && Error != null;

    /// <summary>
    /// Gets a value indicating whether the action is idle (not loading, no data, no error).
    /// </summary>
    public bool IsIdle => !IsLoading && Data == null && Error == null;

    /// <summary>
    /// Creates an idle state.
    /// </summary>
    public static AsyncActionState<TResult> Idle() => new();

    /// <summary>
    /// Creates a loading state.
    /// </summary>
    public static AsyncActionState<TResult> Loading() => new() { IsLoading = true };

    /// <summary>
    /// Creates a success state with data.
    /// </summary>
    public static AsyncActionState<TResult> Success(TResult data) => new()
    {
        Data = data,
        LastUpdated = DateTime.UtcNow
    };

    /// <summary>
    /// Creates an error state with a message.
    /// </summary>
    public static AsyncActionState<TResult> Failed(string error) => new()
    {
        Error = error,
        LastUpdated = DateTime.UtcNow
    };
}
