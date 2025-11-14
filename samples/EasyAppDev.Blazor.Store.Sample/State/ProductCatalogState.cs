using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

public record ProductCatalogState(
    ImmutableDictionary<int, ProductDetails> ProductDetails,
    ImmutableHashSet<int> LoadingProductIds,
    int? SelectedProduct = null,
    int LoadCount = 0,
    int CacheHits = 0)
{
    public ProductCatalogState() : this(
        ImmutableDictionary<int, ProductDetails>.Empty,
        ImmutableHashSet<int>.Empty)
    { }
}

public record CatalogProduct(int Id, string Name, decimal Price);

public record ProductDetails(
    int Id,
    string Name,
    decimal Price,
    string Description,
    string Category,
    int InStock,
    DateTime LoadedAt,
    DateTime ExpiresAt);
