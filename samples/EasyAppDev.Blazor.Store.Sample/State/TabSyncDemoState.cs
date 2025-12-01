using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating cross-tab synchronization.
/// Changes in one browser tab are reflected in all other tabs.
/// </summary>
public record TabSyncDemoState(
    ImmutableList<ChatMessage> Messages,
    string CurrentUser,
    int OnlineCount,
    DateTime LastSync)
{
    public static TabSyncDemoState Initial => new(
        ImmutableList<ChatMessage>.Empty,
        $"User-{Random.Shared.Next(1000, 9999)}",
        1,
        DateTime.UtcNow);

    public TabSyncDemoState AddMessage(string text) =>
        this with
        {
            Messages = Messages.Add(new ChatMessage(
                Guid.NewGuid(),
                CurrentUser,
                text,
                DateTime.UtcNow)),
            LastSync = DateTime.UtcNow
        };

    public TabSyncDemoState ClearMessages() =>
        this with { Messages = ImmutableList<ChatMessage>.Empty, LastSync = DateTime.UtcNow };

    public TabSyncDemoState UpdateOnlineCount(int count) =>
        this with { OnlineCount = count };
}

public record ChatMessage(Guid Id, string User, string Text, DateTime Timestamp);
