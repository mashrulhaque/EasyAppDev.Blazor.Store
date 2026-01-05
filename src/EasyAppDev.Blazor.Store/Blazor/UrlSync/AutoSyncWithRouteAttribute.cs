using System.Diagnostics.CodeAnalysis;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Marks a component parameter for automatic synchronization with a URL route parameter.
/// </summary>
/// <remarks>
/// <para><b>Phase 2 Feature (Not Yet Implemented):</b></para>
/// <para>This attribute is a placeholder for Phase 2 route parameter support.
/// Using it in Phase 3 will throw NotSupportedException at runtime.</para>
/// <para><b>Planned Example (Phase 2):</b></para>
/// <code>
/// @page "/products/{productId:guid}"
///
/// [Parameter]
/// [AutoSyncWithRoute]
/// public Guid? ProductId { get; set; }  // Auto-syncs with state.ProductId
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
[Experimental("EASB001")]
public sealed class AutoSyncWithRouteAttribute : Attribute
{
    /// <summary>
    /// Custom route parameter name.
    /// If not specified, uses the property name.
    /// </summary>
    public string? RouteParamName { get; }

    /// <summary>
    /// State property name to sync with.
    /// If not specified, uses convention-based matching.
    /// </summary>
    public string? StatePropertyName { get; set; }

    /// <summary>
    /// Creates an attribute for auto-sync with route parameters.
    /// </summary>
    public AutoSyncWithRouteAttribute()
    {
    }

    /// <summary>
    /// Creates an attribute with a custom route parameter name.
    /// </summary>
    /// <param name="routeParamName">Route parameter name from @page directive</param>
    public AutoSyncWithRouteAttribute(string routeParamName)
    {
        RouteParamName = routeParamName;
    }
}
