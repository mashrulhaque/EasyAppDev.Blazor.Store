using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating optimistic updates with automatic rollback.
/// </summary>
public record OptimisticDemoState(
    ImmutableList<OptimisticTodoItem> Items,
    string? LastError,
    bool IsProcessing)
{
    public static OptimisticDemoState Initial => new(
        ImmutableList<OptimisticTodoItem>.Empty,
        null,
        false);

    public OptimisticDemoState AddItem(OptimisticTodoItem item) =>
        this with { Items = Items.Add(item), LastError = null };

    public OptimisticDemoState RemoveItem(Guid id) =>
        this with { Items = Items.RemoveAll(i => i.Id == id), LastError = null };

    public OptimisticDemoState ToggleItem(Guid id) =>
        this with
        {
            Items = Items.Select(i =>
                i.Id == id ? i with { IsCompleted = !i.IsCompleted } : i
            ).ToImmutableList(),
            LastError = null
        };

    public OptimisticDemoState UpdateItemWithServerId(Guid tempId, Guid serverId) =>
        this with
        {
            Items = Items.Select(i =>
                i.Id == tempId ? i with { Id = serverId, IsPending = false } : i
            ).ToImmutableList()
        };

    public OptimisticDemoState MarkItemConfirmed(Guid id) =>
        this with
        {
            Items = Items.Select(i =>
                i.Id == id ? i with { IsPending = false } : i
            ).ToImmutableList()
        };

    public OptimisticDemoState SetError(string error) =>
        this with { LastError = error, IsProcessing = false };

    public OptimisticDemoState SetProcessing(bool processing) =>
        this with { IsProcessing = processing };

    public OptimisticDemoState RestoreItem(OptimisticTodoItem item) =>
        this with { Items = Items.Add(item) };
}

public record OptimisticTodoItem(Guid Id, string Text, bool IsCompleted, bool IsPending = false);
