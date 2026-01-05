using System.Diagnostics.CodeAnalysis;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Marks a component parameter for automatic synchronization with a URL query parameter.
/// The parameter name is used as the query parameter name by default.
/// </summary>
/// <remarks>
/// <para><b>Convention-Based Sync (Phase 3):</b></para>
/// <para>Instead of manually calling builder.SyncQueryParam(), use this attribute
/// on component properties with [SupplyParameterFromQuery] to enable auto-sync.</para>
/// <para><b>Example:</b></para>
/// <code>
/// [SupplyParameterFromQuery]
/// [AutoSyncWithQuery]
/// public int Page { get; set; } = 1;  // Auto-syncs with state.Page
///
/// [SupplyParameterFromQuery]
/// [AutoSyncWithQuery("q")]  // Custom query param name
/// public string? Search { get; set; }  // Auto-syncs with state.SearchQuery
/// </code>
/// <para><b>Matching Rules:</b></para>
/// <list type="number">
/// <item>Exact match: Component property "Page" → State property "Page"</item>
/// <item>With prefix: Component "Page" → State "CurrentPage" (if exact match fails)</item>
/// <item>Case-insensitive: "page" matches "Page"</item>
/// </list>
/// <para><b>Supported Types:</b> Same as manual sync - primitives, string, Guid, DateTime, enum, and nullable variants</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
[Experimental("EASB001")]
public sealed class AutoSyncWithQueryAttribute : Attribute
{
    /// <summary>
    /// Custom query parameter name.
    /// If not specified, uses the property name.
    /// </summary>
    public string? QueryParamName { get; }

    /// <summary>
    /// State property name to sync with.
    /// If not specified, uses convention-based matching.
    /// </summary>
    public string? StatePropertyName { get; set; }

    /// <summary>
    /// Creates an attribute for auto-sync with query parameters.
    /// </summary>
    public AutoSyncWithQueryAttribute()
    {
    }

    /// <summary>
    /// Creates an attribute with a custom query parameter name.
    /// </summary>
    /// <param name="queryParamName">Custom query parameter name (e.g., "p" for Page)</param>
    public AutoSyncWithQueryAttribute(string queryParamName)
    {
        QueryParamName = queryParamName;
    }
}
