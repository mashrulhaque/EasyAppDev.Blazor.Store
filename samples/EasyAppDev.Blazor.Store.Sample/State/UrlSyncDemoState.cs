using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

public enum SortOrder
{
    None,
    Ascending,
    Descending
}

public enum ProductCategory
{
    All,
    Electronics,
    Clothing,
    Books,
    Sports
}

public record ProductFilter(
    string SearchTerm = "",
    ProductCategory Category = ProductCategory.All,
    decimal MinPrice = 0,
    decimal MaxPrice = 1000,
    bool InStock = false
);

public record UrlSyncDemoState(
    int CurrentPage = 1,
    int PageSize = 10,
    SortOrder SortOrder = SortOrder.None,
    string SortBy = "name",
    ProductFilter Filter = null!,
    bool ShowAdvancedFilters = false,
    ImmutableList<string> RecentSearches = null!
)
{
    public static UrlSyncDemoState Initial => new(
        CurrentPage: 1,
        PageSize: 10,
        SortOrder: SortOrder.None,
        SortBy: "name",
        Filter: new ProductFilter(),
        ShowAdvancedFilters: false,
        RecentSearches: ImmutableList<string>.Empty
    );

    public UrlSyncDemoState SetPage(int page) =>
        this with { CurrentPage = Math.Max(1, page) };

    public UrlSyncDemoState SetPageSize(int size) =>
        this with { PageSize = Math.Clamp(size, 5, 100), CurrentPage = 1 };

    public UrlSyncDemoState SetSort(string sortBy, SortOrder order) =>
        this with { SortBy = sortBy, SortOrder = order };

    public UrlSyncDemoState SetSearchTerm(string term)
    {
        var newFilter = Filter with { SearchTerm = term };
        var newSearches = string.IsNullOrWhiteSpace(term)
            ? RecentSearches
            : RecentSearches.Contains(term)
                ? RecentSearches
                : RecentSearches.Insert(0, term).Take(5).ToImmutableList();

        return this with
        {
            Filter = newFilter,
            CurrentPage = 1,
            RecentSearches = newSearches
        };
    }

    public UrlSyncDemoState SetCategory(ProductCategory category) =>
        this with
        {
            Filter = Filter with { Category = category },
            CurrentPage = 1
        };

    public UrlSyncDemoState SetPriceRange(decimal min, decimal max) =>
        this with
        {
            Filter = Filter with { MinPrice = min, MaxPrice = max },
            CurrentPage = 1
        };

    public UrlSyncDemoState SetInStockOnly(bool inStock) =>
        this with
        {
            Filter = Filter with { InStock = inStock },
            CurrentPage = 1
        };

    public UrlSyncDemoState ToggleAdvancedFilters() =>
        this with { ShowAdvancedFilters = !ShowAdvancedFilters };

    public UrlSyncDemoState ResetFilters() =>
        this with
        {
            Filter = new ProductFilter(),
            CurrentPage = 1,
            SortOrder = SortOrder.None,
            SortBy = "name"
        };

    public int TotalPages => 10;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Filter.SearchTerm) ||
        Filter.Category != ProductCategory.All ||
        Filter.MinPrice > 0 ||
        Filter.MaxPrice < 1000 ||
        Filter.InStock;
}
