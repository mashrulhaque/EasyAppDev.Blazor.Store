namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Models for demonstrating the Query/Mutation system.
/// </summary>
public record QueryUser(int Id, string Name, string Email, string Avatar);

public record QueryPost(int Id, int UserId, string Title, string Body);

public record CreatePostRequest(int UserId, string Title, string Body);

/// <summary>
/// Simulated API service for Query demo.
/// </summary>
public static class FakeApi
{
    private static readonly List<QueryUser> Users = new()
    {
        new(1, "John Doe", "john@example.com", "https://i.pravatar.cc/150?u=john"),
        new(2, "Jane Smith", "jane@example.com", "https://i.pravatar.cc/150?u=jane"),
        new(3, "Bob Wilson", "bob@example.com", "https://i.pravatar.cc/150?u=bob"),
    };

    private static readonly List<QueryPost> Posts = new()
    {
        new(1, 1, "Getting Started with Blazor", "Blazor is a framework for building interactive web UIs..."),
        new(2, 1, "State Management Patterns", "Managing state in web applications can be challenging..."),
        new(3, 2, "API Design Best Practices", "When designing APIs, consider versioning..."),
    };

    private static int _nextPostId = 4;

    public static async Task<QueryUser?> GetUserAsync(int id, CancellationToken ct = default)
    {
        await Task.Delay(800, ct); // Simulate network delay
        return Users.FirstOrDefault(u => u.Id == id);
    }

    public static async Task<List<QueryUser>> GetUsersAsync(CancellationToken ct = default)
    {
        await Task.Delay(600, ct);
        return Users.ToList();
    }

    public static async Task<List<QueryPost>> GetPostsByUserAsync(int userId, CancellationToken ct = default)
    {
        await Task.Delay(500, ct);
        return Posts.Where(p => p.UserId == userId).ToList();
    }

    public static async Task<QueryPost> CreatePostAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        await Task.Delay(1000, ct); // Simulate slower write operation
        var post = new QueryPost(_nextPostId++, request.UserId, request.Title, request.Body);
        Posts.Add(post);
        return post;
    }

    public static async Task DeletePostAsync(int postId, CancellationToken ct = default)
    {
        await Task.Delay(500, ct);
        Posts.RemoveAll(p => p.Id == postId);
    }
}
