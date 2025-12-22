using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a user profile.
/// </summary>
public record UserProfile(
    int Id,
    string Name,
    string Email,
    string Bio,
    string AvatarUrl);

/// <summary>
/// State for the user profile example - demonstrates async actions and loading states.
/// Uses JSONPlaceholder API (https://jsonplaceholder.typicode.com) for real data fetching.
/// </summary>
public record UserProfileState(
    UserProfile? User = null,
    bool IsLoading = false,
    string? Error = null,
    DateTime? LastUpdated = null)
{
    public static UserProfileState Empty => new();

    public UserProfileState StartLoading() => this with
    {
        IsLoading = true,
        Error = null
    };

    public UserProfileState LoadSuccess(UserProfile user) => this with
    {
        User = user,
        IsLoading = false,
        Error = null,
        LastUpdated = DateTime.Now
    };

    public UserProfileState LoadFailure(string error) => this with
    {
        IsLoading = false,
        Error = error
    };

    public UserProfileState UpdateName(string name)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Name = name },
            LastUpdated = DateTime.Now
        };
    }

    public UserProfileState UpdateEmail(string email)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Email = email },
            LastUpdated = DateTime.Now
        };
    }

    public UserProfileState UpdateBio(string bio)
    {
        if (User is null) return this;
        return this with
        {
            User = User with { Bio = bio },
            LastUpdated = DateTime.Now
        };
    }

    public UserProfileState Clear() => Empty;
}

/// <summary>
/// JSONPlaceholder API user model.
/// See https://jsonplaceholder.typicode.com for API documentation.
/// </summary>
public record JsonPlaceholderUser(
    int Id,
    string Name,
    string Username,
    string Email,
    string? Phone,
    string? Website);

/// <summary>
/// API service for fetching real user data.
/// Uses https://jsonplaceholder.typicode.com - a free REST API for testing.
/// </summary>
public class ReqResApi
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jsonplaceholder.typicode.com";

    public ReqResApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets a single user by ID (1-10 are valid).
    /// </summary>
    public async Task<UserProfile?> GetUserAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            var user = await _httpClient.GetFromJsonAsync<JsonPlaceholderUser>($"{BaseUrl}/users/{userId}", ct);
            if (user == null) return null;

            return new UserProfile(
                Id: user.Id,
                Name: user.Name,
                Email: user.Email,
                Bio: $"Hello! I'm {user.Name} (@{user.Username}). This bio is editable - try changing it!",
                AvatarUrl: $"https://i.pravatar.cc/150?u={user.Email}"
            );
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a list of users.
    /// </summary>
    public async Task<List<UserProfile>> GetUsersAsync(int page = 1, CancellationToken ct = default)
    {
        var users = await _httpClient.GetFromJsonAsync<List<JsonPlaceholderUser>>($"{BaseUrl}/users", ct);
        if (users == null) return new List<UserProfile>();

        // Simulate pagination (JSONPlaceholder returns all 10 users)
        var skip = (page - 1) * 6;
        return users.Skip(skip).Take(6).Select(user => new UserProfile(
            Id: user.Id,
            Name: user.Name,
            Email: user.Email,
            Bio: $"Hello! I'm {user.Name}.",
            AvatarUrl: $"https://i.pravatar.cc/150?u={user.Email}"
        )).ToList();
    }

    /// <summary>
    /// Gets total number of available users.
    /// </summary>
    public async Task<int> GetTotalUsersAsync(CancellationToken ct = default)
    {
        var users = await _httpClient.GetFromJsonAsync<List<JsonPlaceholderUser>>($"{BaseUrl}/users", ct);
        return users?.Count ?? 0;
    }
}
