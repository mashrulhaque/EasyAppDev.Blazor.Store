using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for the comprehensive demo showcasing all async helpers working together.
/// </summary>
public record ComprehensiveDemoState(
    string SearchQuery = "",
    int SearchCount = 0,
    AsyncData<List<Product>> SearchResults = default!,
    int ScrollEvents = 0,
    ImmutableDictionary<int, ProductDetails> ProductDetails = default!,
    ImmutableHashSet<int> LoadingDetails = default!,
    ProductDetails? SelectedProductDetails = null,
    int CacheHits = 0)
{
    /// <summary>
    /// Initial state for the comprehensive demo.
    /// </summary>
    public static ComprehensiveDemoState Initial => new(
        SearchQuery: "",
        SearchCount: 0,
        SearchResults: AsyncData<List<Product>>.NotAsked(),
        ScrollEvents: 0,
        ProductDetails: ImmutableDictionary<int, ProductDetails>.Empty,
        LoadingDetails: ImmutableHashSet<int>.Empty,
        SelectedProductDetails: null,
        CacheHits: 0);

    /// <summary>
    /// Sets the search query (used by debounced search).
    /// </summary>
    public ComprehensiveDemoState SetQuery(string query) => this with
    {
        SearchQuery = query,
        SearchCount = SearchCount + 1
    };

    /// <summary>
    /// Increments scroll event counter (used by throttled scroll).
    /// </summary>
    public ComprehensiveDemoState IncrementScroll() => this with
    {
        ScrollEvents = ScrollEvents + 1
    };
}
