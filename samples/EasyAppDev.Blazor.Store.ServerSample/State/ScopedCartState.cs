using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.ServerSample.State;

/// <summary>
/// Scoped shopping cart - demonstrates cross-store updates with async patterns.
/// </summary>
public record ScopedCartState(
    ImmutableList<string> Items,
    string? LastAction = null)
{
    public static ScopedCartState Empty => new(ImmutableList<string>.Empty);

    public ScopedCartState AddItem(string item) => this with
    {
        Items = Items.Add(item),
        LastAction = $"ADD:{item}"
    };

    public ScopedCartState RemoveItem(string item) => this with
    {
        Items = Items.Remove(item),
        LastAction = $"REMOVE:{item}"
    };

    public ScopedCartState Clear() => this with
    {
        Items = ImmutableList<string>.Empty,
        LastAction = "CLEAR"
    };

    public int ItemCount => Items.Count;
}
