namespace EasyAppDev.Blazor.Store.ServerSample.State;

/// <summary>
/// User session state - demonstrates per-user data isolation.
/// </summary>
public record UserSessionState(
    string SessionId,
    DateTime ConnectedAt,
    int ActionCount = 0,
    string? LastAction = null)
{
    public static UserSessionState Create() => new(
        SessionId: Guid.NewGuid().ToString("N")[..8],
        ConnectedAt: DateTime.UtcNow
    );

    public UserSessionState IncrementActionCount(string action) => this with
    {
        ActionCount = ActionCount + 1,
        LastAction = action
    };
}
