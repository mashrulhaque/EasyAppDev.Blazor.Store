using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Base component for Blazor components that need bidirectional URL-state synchronization.
/// Automatically syncs configured component parameters with store state.
/// </summary>
/// <typeparam name="TState">The state type managed by the store</typeparam>
/// <remarks>
/// <para><b>INCOMPATIBLE WITH:</b></para>
/// <list type="bullet">
/// <item>TabSync middleware (tabs must maintain independent URLs)</item>
/// <item>Multiple UrlSyncStoreComponent instances per store</item>
/// </list>
/// <para><b>SECURITY WARNING:</b> URL parameters are user-controlled input.
/// Always validate state using IStateValidator when syncing security-sensitive properties.</para>
/// <para><b>Phase 3 Features:</b></para>
/// <list type="bullet">
/// <item>Convention-based auto-sync with [AutoSyncWithQuery] attribute</item>
/// <item>Configurable debounce (WithDebounce)</item>
/// <item>Configurable navigation mode (Replace/Push)</item>
/// <item>Action filtering with ExcludeActions</item>
/// <item>Error callbacks (OnConversionError, OnError)</item>
/// </list>
/// <para><b>Future Enhancements:</b></para>
/// <list type="bullet">
/// <item>Route parameters ([AutoSyncWithRoute] - Phase 2)</item>
/// <item>Source generator for automatic component generation</item>
/// <item>Custom value converters (Phase 2)</item>
/// </list>
/// </remarks>
[Experimental("EASB001")]
public abstract class UrlSyncStoreComponent<TState> : StoreComponent<TState>
    where TState : notnull
{
    private static readonly ConcurrentDictionary<object, WeakReference<UrlSyncStoreComponent<TState>>>
        _activeComponents = new();

    private UrlSyncManager<TState>? _urlSyncManager;
    private bool _isFirstParameterSet = true;

    /// <summary>
    /// Navigation manager for URL manipulation.
    /// Injected automatically by Blazor.
    /// </summary>
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// Logger for URL sync diagnostics.
    /// </summary>
    [Inject]
    protected new ILogger<UrlSyncStoreComponent<TState>>? Logger { get; set; }

    /// <summary>
    /// Configure which component parameters should sync with state properties.
    /// Called once during component initialization.
    /// </summary>
    /// <param name="builder">Fluent builder for URL sync configuration</param>
    /// <remarks>
    /// <para><b>Phase 3: Convention-Based Auto-Sync</b></para>
    /// <para>You can now use attributes instead of manual configuration:</para>
    /// <code>
    /// // Option 1: Attribute-based (Phase 3 - zero boilerplate)
    /// [SupplyParameterFromQuery]
    /// [AutoSyncWithQuery]
    /// public int Page { get; set; } = 1;
    ///
    /// // Option 2: Manual configuration (still supported)
    /// protected override void ConfigureUrlSync(IUrlSyncBuilder&lt;MyState&gt; builder)
    /// {
    ///     builder.SyncQueryParam(() => Page, s => s.CurrentPage);
    /// }
    /// </code>
    /// <para>If you use attributes, you don't need to override this method.
    /// If you override this method, auto-discovery still runs first, then your manual config.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// protected override void ConfigureUrlSync(IUrlSyncBuilder&lt;MyState&gt; builder)
    /// {
    ///     builder
    ///         .SyncQueryParam(() => Page, s => s.CurrentPage)
    ///         .SyncQueryParam(() => Search, s => s.SearchQuery)
    ///         .WithDebounce(TimeSpan.FromMilliseconds(500));
    /// }
    /// </code>
    /// </example>
    protected virtual void ConfigureUrlSync(IUrlSyncBuilder<TState> builder)
    {
        // Default: no manual URL sync configuration
        // Users can:
        // 1. Use [AutoSyncWithQuery] attributes (convention-based, zero boilerplate)
        // 2. Override this method for manual configuration
        // 3. Combine both approaches (auto-discovery + manual config)
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Safety guardrail 1: Detect TabSync
        DetectTabSyncConflict();

        // Safety guardrail 2: Detect multiple components
        DetectMultipleComponents();

        // Build URL sync configuration
        var builder = new UrlSyncBuilder<TState>();

        // Phase 3: Auto-discover mappings from attributes
        var autoMappingsCount = ConventionBasedSyncHelper.DiscoverAndConfigureMappings(
            this,
            builder,
            Logger);

        // Allow manual configuration (can augment or override auto-discovered mappings)
        ConfigureUrlSync(builder);

        var config = builder.Build();

        if (autoMappingsCount > 0)
        {
            Logger?.LogDebug(
                "UrlSync initialized with {AutoCount} auto-discovered mappings for {ComponentType}",
                autoMappingsCount,
                GetType().Name);
        }

        // Create and start URL sync manager
        _urlSyncManager = new UrlSyncManager<TState>(Store, Navigation, config, Logger);
        _urlSyncManager.Start();

        Logger?.LogDebug("UrlSyncStoreComponent initialized for {ComponentType}", GetType().Name);
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (_urlSyncManager == null) return;

        // On first render, sync URL → State
        // On subsequent renders, check if URL changed (browser back/forward)
        if (_isFirstParameterSet)
        {
            _isFirstParameterSet = false;
            Logger?.LogDebug("First parameter set - syncing URL to state");
        }

        await _urlSyncManager.SyncFromComponentParametersAsync();
    }

    /// <summary>
    /// Detects if the store has TabSync middleware enabled.
    /// Throws InvalidOperationException if TabSync is detected.
    /// </summary>
    private void DetectTabSyncConflict()
    {
        // Check if store has any middleware with "TabSync" in the type name
        var storeType = Store.GetType();
        var hasTabSync = storeType
            .GetInterfaces()
            .Any(i => i.Name.Contains("TabSync", StringComparison.OrdinalIgnoreCase)) ||
            storeType.FullName?.Contains("TabSync", StringComparison.OrdinalIgnoreCase) == true;

        // Also check for TabSync middleware via type inspection
        // Look for fields/properties that might indicate TabSync
        var fields = storeType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var hasTabSyncField = fields.Any(f => f.FieldType.Name.Contains("TabSync", StringComparison.OrdinalIgnoreCase));

        if (hasTabSync || hasTabSyncField)
        {
            throw new InvalidOperationException(
                $"UrlSyncStoreComponent cannot be used with TabSync middleware.\n\n" +
                $"Reason: TabSync synchronizes state across browser tabs, but each tab " +
                $"must maintain an independent URL for proper browser behavior.\n\n" +
                $"Solution: Choose ONE of the following:\n" +
                $"  1. Use TabSync (tabs share state, maintain separate URLs)\n" +
                $"  2. Use UrlSync (URL drives state, no cross-tab sync)"
            );
        }
    }

    /// <summary>
    /// Detects if another UrlSyncStoreComponent is already using this store.
    /// Throws InvalidOperationException if a duplicate is detected.
    /// </summary>
    private void DetectMultipleComponents()
    {
        var weakRef = new WeakReference<UrlSyncStoreComponent<TState>>(this);

        if (!_activeComponents.TryAdd(Store, weakRef))
        {
            // Another component already exists
            if (_activeComponents.TryGetValue(Store, out var existingRef) &&
                existingRef.TryGetTarget(out var existingComponent))
            {
                throw new InvalidOperationException(
                    $"Only ONE UrlSyncStoreComponent per store is allowed.\n\n" +
                    $"Another component ({existingComponent.GetType().Name}) is already " +
                    $"syncing {typeof(TState).Name} to the URL.\n\n" +
                    $"Solution: Use UrlSync in only one component per page " +
                    $"(typically the page component, not child components).\n\n" +
                    $"If you need different components to manage different URL params, " +
                    $"consider using separate stores or manual URL management."
                );
            }
            else
            {
                // Existing component was GC'd, replace with new one
                if (existingRef != null)
                {
                    _activeComponents.TryUpdate(Store, weakRef, existingRef);
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _urlSyncManager?.Dispose();
            _urlSyncManager = null;

            _activeComponents.TryRemove(Store, out _);

            Logger?.LogDebug("UrlSyncStoreComponent disposed for {ComponentType}", GetType().Name);
        }

        base.Dispose(disposing);
    }
}
