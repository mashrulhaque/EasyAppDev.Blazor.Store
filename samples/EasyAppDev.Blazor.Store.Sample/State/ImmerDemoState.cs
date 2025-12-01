using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating Immer-style draft updates.
/// Shows how to use ProduceAsync for clean nested state modifications.
/// </summary>
public record ImmerDemoState(
    ImmerUserProfile Profile,
    ImmutableList<ImmerShopItem> ShopItems,
    ImmutableDictionary<string, string> Settings,
    int ActionCount)
{
    public static ImmerDemoState Initial => new(
        new ImmerUserProfile("John Doe", new ImmerAddress("123 Main St", "NYC", "10001"), 0),
        ImmutableList.Create(
            new ImmerShopItem(1, "Laptop", 999.99m, 1),
            new ImmerShopItem(2, "Mouse", 29.99m, 2),
            new ImmerShopItem(3, "Keyboard", 79.99m, 1)
        ),
        ImmutableDictionary<string, string>.Empty
            .Add("theme", "light")
            .Add("language", "en"),
        0);
}

public record ImmerUserProfile(string Name, ImmerAddress Address, int Points);

public record ImmerAddress(string Street, string City, string ZipCode);

public record ImmerShopItem(int Id, string Name, decimal Price, int Quantity);
