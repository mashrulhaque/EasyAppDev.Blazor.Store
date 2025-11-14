using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a user entity.
/// </summary>
/// <param name="Id">User ID.</param>
/// <param name="Name">User's full name.</param>
/// <param name="Email">User's email address.</param>
public record User(int Id, string Name, string Email);

/// <summary>
/// State for user management operations - demonstrates ExecuteAsync helper.
/// Shows how ExecuteAsync simplifies async operations from 10-12 lines to 3-5 lines.
/// </summary>
/// <param name="User">Async state wrapper containing user data.</param>
/// <param name="IsSaving">Whether a save operation is in progress.</param>
/// <param name="SaveSuccess">Whether the last save operation succeeded.</param>
/// <param name="SaveError">Error message from save operation, if any.</param>
/// <param name="IsDeleting">Whether a delete operation is in progress.</param>
/// <param name="DeleteSuccess">Whether the last delete operation succeeded.</param>
/// <param name="DeleteError">Error message from delete operation, if any.</param>
/// <remarks>
/// <para>
/// <strong>Traditional Approach (12+ lines per operation):</strong>
/// </para>
/// <code>
/// async Task LoadUser()
/// {
///     Update(s => s with { User = s.User.ToLoading() });
///     try
///     {
///         var user = await UserService.GetUserAsync(userId);
///         Update(s => s with { User = AsyncData.Success(user) });
///     }
///     catch (Exception ex)
///     {
///         Update(s => s with { User = AsyncData.Failure(ex.Message) });
///     }
/// }
/// </code>
/// <para>
/// <strong>ExecuteAsync Approach (5 lines!):</strong>
/// </para>
/// <code>
/// async Task LoadUser()
/// {
///     await ExecuteAsync(
///         () => UserService.GetUserAsync(userId),
///         loading: s => s with { User = s.User.ToLoading() },
///         success: (s, user) => s with { User = AsyncData.Success(user) }
///     );
/// }
/// </code>
/// </remarks>
public record UserManagementState(
    AsyncData<User> User,
    bool IsSaving = false,
    bool SaveSuccess = false,
    string? SaveError = null,
    bool IsDeleting = false,
    bool DeleteSuccess = false,
    string? DeleteError = null)
{
    /// <summary>
    /// Creates an initial state with no user loaded.
    /// </summary>
    public static UserManagementState Initial => new(AsyncData<User>.NotAsked());
}
