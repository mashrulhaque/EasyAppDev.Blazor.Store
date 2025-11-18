namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for authentication demo showcasing cross-store updates.
/// </summary>
public record AuthDemoState(
    string? Username = null,
    bool IsAuthenticated = false,
    string? LastAction = null)
{
    public static AuthDemoState Initial => new();

    /// <summary>
    /// Logs in a user.
    /// </summary>
    public AuthDemoState Login(string username) => this with
    {
        Username = username,
        IsAuthenticated = true,
        LastAction = $"LOGIN:{username}"
    };

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    public AuthDemoState Logout() => this with
    {
        Username = null,
        IsAuthenticated = false,
        LastAction = "LOGOUT"
    };
}
