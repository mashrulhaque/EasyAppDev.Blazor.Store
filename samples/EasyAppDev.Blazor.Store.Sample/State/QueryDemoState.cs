using System.Net.Http.Json;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Models for demonstrating the Query/Mutation system.
/// Uses JSONPlaceholder (https://jsonplaceholder.typicode.com) - a free REST API for testing.
/// </summary>
public record QueryUser(
    int Id,
    string Name,
    string Username,
    string Email,
    QueryAddress? Address = null,
    string? Phone = null,
    string? Website = null,
    QueryCompany? Company = null)
{
    // Generate avatar URL from user ID
    public string Avatar => $"https://i.pravatar.cc/150?u={Id}";
}

public record QueryAddress(
    string Street,
    string Suite,
    string City,
    string Zipcode,
    QueryGeo? Geo = null);

public record QueryGeo(string Lat, string Lng);

public record QueryCompany(string Name, string CatchPhrase, string Bs);

public record QueryPost(int Id, int UserId, string Title, string Body);

public record CreatePostRequest(int UserId, string Title, string Body);

/// <summary>
/// JSONPlaceholder API service for Query demo.
/// Uses the real JSONPlaceholder REST API (https://jsonplaceholder.typicode.com).
/// </summary>
public class JsonPlaceholderApi
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jsonplaceholder.typicode.com";

    public JsonPlaceholderApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<QueryUser?> GetUserAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<QueryUser>($"{BaseUrl}/users/{id}", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<QueryUser>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _httpClient.GetFromJsonAsync<List<QueryUser>>($"{BaseUrl}/users", ct);
        return users ?? new List<QueryUser>();
    }

    public async Task<List<QueryPost>> GetPostsByUserAsync(int userId, CancellationToken ct = default)
    {
        var posts = await _httpClient.GetFromJsonAsync<List<QueryPost>>($"{BaseUrl}/posts?userId={userId}", ct);
        return posts ?? new List<QueryPost>();
    }

    public async Task<List<QueryPost>> GetAllPostsAsync(CancellationToken ct = default)
    {
        var posts = await _httpClient.GetFromJsonAsync<List<QueryPost>>($"{BaseUrl}/posts", ct);
        return posts ?? new List<QueryPost>();
    }

    public async Task<QueryPost?> CreatePostAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/posts", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryPost>(ct);
    }

    public async Task DeletePostAsync(int postId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/posts/{postId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
