using System.Linq.Expressions;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Fluent builder for configuring URL-state synchronization.
/// Provides type-safe mapping between URL parameters and state properties.
/// </summary>
/// <typeparam name="TState">The state type managed by the store</typeparam>
public interface IUrlSyncBuilder<TState> where TState : notnull
{
    /// <summary>
    /// Synchronize a query parameter with a state property.
    /// Query parameters are extracted from the URL query string (e.g., ?page=5).
    /// </summary>
    /// <typeparam name="TParam">Parameter type (must be primitive, string, Guid, DateTime, or enum)</typeparam>
    /// <param name="componentProperty">Expression pointing to the component's [SupplyParameterFromQuery] property</param>
    /// <param name="stateProperty">Expression pointing to the corresponding state property</param>
    /// <param name="queryParamName">Optional custom query parameter name (defaults to property name)</param>
    /// <returns>Builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">If TParam is a reference type (other than string)</exception>
    /// <example>
    /// <code>
    /// // Syncs ?page=5 to state.CurrentPage
    /// builder.SyncQueryParam(() => Page, s => s.CurrentPage);
    ///
    /// // Custom query param name: ?p=5 to state.CurrentPage
    /// builder.SyncQueryParam(() => Page, s => s.CurrentPage, "p");
    /// </code>
    /// </example>
    IUrlSyncBuilder<TState> SyncQueryParam<TParam>(
        Expression<Func<TParam>> componentProperty,
        Expression<Func<TState, TParam>> stateProperty,
        string? queryParamName = null);

    /// <summary>
    /// Set debounce duration for State → URL synchronization.
    /// Prevents history pollution from rapid state updates.
    /// </summary>
    /// <param name="duration">Debounce duration (default: 300ms)</param>
    /// <returns>Builder for method chaining</returns>
    /// <example>
    /// <code>
    /// // For high-frequency updates like sliders
    /// builder.WithDebounce(TimeSpan.FromMilliseconds(500));
    /// </code>
    /// </example>
    IUrlSyncBuilder<TState> WithDebounce(TimeSpan duration);

    /// <summary>
    /// Set navigation mode for State → URL synchronization.
    /// </summary>
    /// <param name="mode">Navigation mode (default: Replace)</param>
    /// <returns>Builder for method chaining</returns>
    /// <remarks>
    /// <list type="table">
    /// <item>
    /// <term>Replace</term>
    /// <description>Replaces current history entry (recommended, prevents back button pollution)</description>
    /// </item>
    /// <item>
    /// <term>Push</term>
    /// <description>Adds new history entry (rarely needed, use for intentional navigation)</description>
    /// </item>
    /// </list>
    /// </remarks>
    IUrlSyncBuilder<TState> WithNavigationMode(UrlSyncNavigationMode mode);

    /// <summary>
    /// Exclude specific actions from triggering State → URL synchronization.
    /// Useful for filtering out server sync, cursor updates, etc.
    /// </summary>
    /// <param name="actions">Action names to exclude</param>
    /// <returns>Builder for method chaining</returns>
    /// <example>
    /// <code>
    /// builder.ExcludeActions("SERVER_SYNC", "CURSOR_UPDATE", "PRESENCE_UPDATE");
    /// </code>
    /// </example>
    IUrlSyncBuilder<TState> ExcludeActions(params string[] actions);

    /// <summary>
    /// Register a callback for URL parameter conversion errors.
    /// Called when a URL parameter cannot be converted to the target type.
    /// </summary>
    /// <param name="handler">Error handler (paramName, exception)</param>
    /// <returns>Builder for method chaining</returns>
    /// <example>
    /// <code>
    /// builder.OnConversionError((paramName, ex) =>
    ///     Logger.LogWarning("Invalid URL param {Param}: {Error}", paramName, ex.Message));
    /// </code>
    /// </example>
    IUrlSyncBuilder<TState> OnConversionError(Action<string, Exception> handler);

    /// <summary>
    /// Register a callback for general URL sync errors.
    /// Called for navigation failures, validation errors, etc.
    /// </summary>
    /// <param name="handler">Error handler</param>
    /// <returns>Builder for method chaining</returns>
    IUrlSyncBuilder<TState> OnError(Action<Exception> handler);
}
