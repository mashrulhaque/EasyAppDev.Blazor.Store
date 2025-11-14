namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a user profile.
/// </summary>
/// <param name="Id">User ID.</param>
/// <param name="Name">User's full name.</param>
/// <param name="Email">User's email address.</param>
/// <param name="Bio">User's biography.</param>
/// <param name="AvatarUrl">URL to user's avatar image.</param>
public record UserProfile(
    int Id,
    string Name,
    string Email,
    string Bio,
    string AvatarUrl);

/// <summary>
/// State for the user profile example - demonstrates async actions and loading states.
/// </summary>
/// <param name="User">The current user profile (null if not loaded).</param>
/// <param name="IsLoading">Whether data is currently being loaded.</param>
/// <param name="Error">Error message if loading failed.</param>
/// <param name="LastUpdated">When the profile was last updated.</param>
public record UserProfileState(
    UserProfile? User = null,
    bool IsLoading = false,
    string? Error = null,
    DateTime? LastUpdated = null)
{
    /// <summary>
    /// Creates an empty user profile state.
    /// </summary>
    public static UserProfileState Empty => new();

    /// <summary>
    /// Sets the loading state to true and clears any previous errors.
    /// </summary>
    public UserProfileState StartLoading() => this with
    {
        IsLoading = true,
        Error = null
    };

    /// <summary>
    /// Sets the user profile after successful load.
    /// </summary>
    public UserProfileState LoadSuccess(UserProfile user) => this with
    {
        User = user,
        IsLoading = false,
        Error = null,
        LastUpdated = DateTime.Now
    };

    /// <summary>
    /// Sets an error message after failed load.
    /// </summary>
    public UserProfileState LoadFailure(string error) => this with
    {
        IsLoading = false,
        Error = error
    };

    /// <summary>
    /// Updates the user's name.
    /// </summary>
    public UserProfileState UpdateName(string name)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Name = name },
            LastUpdated = DateTime.Now
        };
    }

    /// <summary>
    /// Updates the user's email.
    /// </summary>
    public UserProfileState UpdateEmail(string email)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Email = email },
            LastUpdated = DateTime.Now
        };
    }

    /// <summary>
    /// Updates the user's bio.
    /// </summary>
    public UserProfileState UpdateBio(string bio)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Bio = bio },
            LastUpdated = DateTime.Now
        };
    }

    /// <summary>
    /// Clears the current user profile.
    /// </summary>
    public UserProfileState Clear() => Empty;

    /// <summary>
    /// Simulates loading a user profile from an API (async operation).
    /// In a real app, this would call an actual API service.
    /// </summary>
    public static async Task<UserProfile> SimulateApiLoad(int userId)
    {
        // Simulate network delay
        await Task.Delay(1500);

        // Simulate occasional errors
        if (userId < 0)
        {
            throw new Exception("Invalid user ID");
        }

        // Return mock data
        return new UserProfile(
            Id: userId,
            Name: $"User {userId}",
            Email: $"user{userId}@example.com",
            Bio: $"This is the biography for user {userId}. I love using state management!",
            AvatarUrl: $"https://i.pravatar.cc/150?img={userId}"
        );
    }
}
