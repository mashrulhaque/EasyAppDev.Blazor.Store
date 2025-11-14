using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
/// <param name="Id">Product ID.</param>
/// <param name="Name">Product name.</param>
/// <param name="Price">Product price.</param>
/// <param name="ImageUrl">URL to product image.</param>
public record Product(
    int Id,
    string Name,
    decimal Price,
    string ImageUrl);

/// <summary>
/// Represents an item in the shopping cart.
/// </summary>
/// <param name="Product">The product.</param>
/// <param name="Quantity">Quantity in cart.</param>
public record CartItem(
    Product Product,
    int Quantity)
{
    /// <summary>
    /// Gets the total price for this cart item.
    /// </summary>
    public decimal Total => Product.Price * Quantity;
}

/// <summary>
/// State for the shopping cart example - demonstrates persistence with LocalStorage.
/// This state will be automatically saved to and restored from browser localStorage.
/// </summary>
/// <param name="Items">The items in the cart.</param>
/// <param name="DiscountCode">Applied discount code.</param>
/// <param name="DiscountPercent">Discount percentage (0-100).</param>
public record ShoppingCartState(
    ImmutableList<CartItem> Items,
    string? DiscountCode = null,
    decimal DiscountPercent = 0)
{
    /// <summary>
    /// Creates an empty shopping cart state.
    /// </summary>
    public static ShoppingCartState Empty => new(ImmutableList<CartItem>.Empty);

    /// <summary>
    /// Adds a product to the cart or increases quantity if already present.
    /// </summary>
    public ShoppingCartState AddItem(Product product, int quantity = 1)
    {
        var existingIndex = Items.FindIndex(item => item.Product.Id == product.Id);

        if (existingIndex >= 0)
        {
            var existingItem = Items[existingIndex];
            var updatedItem = existingItem with { Quantity = existingItem.Quantity + quantity };
            return this with { Items = Items.SetItem(existingIndex, updatedItem) };
        }

        return this with { Items = Items.Add(new CartItem(product, quantity)) };
    }

    /// <summary>
    /// Removes a product from the cart.
    /// </summary>
    public ShoppingCartState RemoveItem(int productId) => this with
    {
        Items = Items.RemoveAll(item => item.Product.Id == productId)
    };

    /// <summary>
    /// Updates the quantity of a cart item.
    /// </summary>
    public ShoppingCartState UpdateQuantity(int productId, int quantity)
    {
        if (quantity <= 0)
        {
            return RemoveItem(productId);
        }

        var index = Items.FindIndex(item => item.Product.Id == productId);
        if (index < 0) return this;

        var item = Items[index];
        return this with
        {
            Items = Items.SetItem(index, item with { Quantity = quantity })
        };
    }

    /// <summary>
    /// Applies a discount code.
    /// </summary>
    public ShoppingCartState ApplyDiscount(string code, decimal percent) => this with
    {
        DiscountCode = code,
        DiscountPercent = Math.Clamp(percent, 0, 100)
    };

    /// <summary>
    /// Removes the discount code.
    /// </summary>
    public ShoppingCartState RemoveDiscount() => this with
    {
        DiscountCode = null,
        DiscountPercent = 0
    };

    /// <summary>
    /// Clears the entire cart.
    /// </summary>
    public ShoppingCartState Clear() => Empty;

    /// <summary>
    /// Gets the subtotal (before discount).
    /// </summary>
    public decimal Subtotal => Items.Sum(item => item.Total);

    /// <summary>
    /// Gets the discount amount.
    /// </summary>
    public decimal DiscountAmount => Subtotal * (DiscountPercent / 100);

    /// <summary>
    /// Gets the total (after discount).
    /// </summary>
    public decimal Total => Subtotal - DiscountAmount;

    /// <summary>
    /// Gets the total number of items in the cart.
    /// </summary>
    public int ItemCount => Items.Sum(item => item.Quantity);

    /// <summary>
    /// Sample products for the demo.
    /// </summary>
    public static readonly ImmutableList<Product> SampleProducts = ImmutableList.Create(
        new Product(1, "Wireless Mouse", 29.99m, "🖱️"),
        new Product(2, "Mechanical Keyboard", 89.99m, "⌨️"),
        new Product(3, "USB-C Cable", 12.99m, "🔌"),
        new Product(4, "Laptop Stand", 49.99m, "💻"),
        new Product(5, "Desk Lamp", 34.99m, "💡"),
        new Product(6, "Monitor", 299.99m, "🖥️")
    );
}
