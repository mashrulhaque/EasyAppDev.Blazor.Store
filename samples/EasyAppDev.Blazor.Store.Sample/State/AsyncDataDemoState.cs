using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a user profile with additional metadata.
/// </summary>
/// <param name="Name">User's full name.</param>
/// <param name="Email">User's email address.</param>
/// <param name="Bio">User's biography.</param>
/// <param name="LoadedAt">Timestamp when the profile was loaded.</param>
public record UserProfileData(
    string Name,
    string Email,
    string Bio,
    DateTime LoadedAt);

/// <summary>
/// State for AsyncData demo - demonstrates the AsyncData&lt;T&gt; wrapper pattern.
/// This is a dramatic simplification compared to the traditional approach.
/// </summary>
/// <param name="User">Async state wrapper containing user profile data.</param>
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
public record AsyncDataDemoState(AsyncData<UserProfileData> User)
{
    /// <summary>
    /// Creates an initial state with no data requested yet.
    /// </summary>
    public static AsyncDataDemoState Initial => new(AsyncData<UserProfileData>.NotAsked());

    /// <summary>
    /// Simulates loading a user profile from an API (async operation).
    /// In a real app, this would call an actual API service.
    /// </summary>
    public static async Task<UserProfileData> SimulateApiLoad(int userId)
    {
        // Simulate network delay
        await Task.Delay(1500);

        // Simulate occasional errors
        if (userId < 0)
        {
            throw new Exception("Invalid user ID");
        }

        // Return mock data
        return new UserProfileData(
            Name: $"John Doe {userId}",
            Email: $"john.doe{userId}@example.com",
            Bio: "Software developer passionate about clean code and great UX. " +
                 "I love using state management patterns that make development a joy!",
            LoadedAt: DateTime.Now
        );
    }
}
