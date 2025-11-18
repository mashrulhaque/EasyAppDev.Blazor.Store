using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for shopping cart demo showcasing cross-store updates.
/// </summary>
public record CartDemoState(
    ImmutableList<string> Items,
    string? LastAction = null)
{
    public static CartDemoState Empty => new(ImmutableList<string>.Empty);

    /// <summary>
    /// Adds an item to the cart.
    /// </summary>
    public CartDemoState AddItem(string item) => this with
    {
        Items = Items.Add(item),
        LastAction = $"ADD_ITEM:{item}"
    };

    /// <summary>
    /// Removes an item from the cart.
    /// </summary>
    public CartDemoState RemoveItem(string item) => this with
    {
        Items = Items.Remove(item),
        LastAction = $"REMOVE_ITEM:{item}"
    };

    /// <summary>
    /// Clears all items from the cart.
    /// </summary>
    public CartDemoState Clear() => this with
    {
        Items = ImmutableList<string>.Empty,
        LastAction = "CLEAR"
    };

    /// <summary>
    /// Gets the total number of items in the cart.
    /// </summary>
    public int ItemCount => Items.Count;
}
