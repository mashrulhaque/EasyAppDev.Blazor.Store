using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a user profile with additional metadata.
/// </summary>
public record UserProfileData(
    int Id,
    string Name,
    string Email,
    string Avatar,
    string Bio,
    DateTime LoadedAt);

/// <summary>
/// State for AsyncData demo - demonstrates the AsyncData&lt;T&gt; wrapper pattern.
/// This is a dramatic simplification compared to the traditional approach.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Traditional Approach (20+ lines):</strong>
/// </para>
/// <code>
/// public record UserProfileState(
///     UserProfile? User = null,
///     bool IsLoading = false,
///     string? Error = null,
///     DateTime? LastUpdated = null)
/// {
///     public UserProfileState StartLoading() => this with { IsLoading = true, Error = null };
///     public UserProfileState LoadSuccess(UserProfile user) => this with { User = user, IsLoading = false };
///     public UserProfileState LoadFailure(string error) => this with { IsLoading = false, Error = error };
/// }
/// </code>
/// <para>
/// <strong>AsyncData Approach (1 property!):</strong>
/// </para>
/// <code>
/// public record AsyncDataDemoState(AsyncData&lt;UserProfileData&gt; User);
/// </code>
/// </remarks>
public record AsyncDataDemoState(AsyncData<UserProfileData> User, int CurrentUserId = 1)
{
    public static AsyncDataDemoState Initial => new(AsyncData<UserProfileData>.NotAsked());
}
