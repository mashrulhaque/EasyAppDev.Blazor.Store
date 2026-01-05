using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Helper class for convention-based property discovery and auto-mapping.
/// Scans component properties for auto-sync attributes and generates mappings.
/// </summary>
internal static class ConventionBasedSyncHelper
{
    /// <summary>
    /// Discovers and configures auto-sync mappings for a component instance.
    /// Scans for [AutoSyncWithQuery] and [AutoSyncWithRoute] attributes.
    /// </summary>
    /// <typeparam name="TState">The state type</typeparam>
    /// <param name="component">The component instance to scan</param>
    /// <param name="builder">The builder to configure</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Number of mappings discovered</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection used for convention-based mapping")]
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Reflection used for convention-based mapping")]
    public static int DiscoverAndConfigureMappings<TState>(
        object component,
        IUrlSyncBuilder<TState> builder,
        ILogger? logger = null)
        where TState : notnull
    {
        var componentType = component.GetType();
        var stateType = typeof(TState);
        var mappingsCount = 0;

        var properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var componentProp in properties)
        {
            // Check for AutoSyncWithQuery attribute
#pragma warning disable EASB001 // Experimental feature - internal usage
            var queryAttr = componentProp.GetCustomAttribute<AutoSyncWithQueryAttribute>();
#pragma warning restore EASB001
            if (queryAttr != null)
            {
                if (TryCreateQueryMapping(component, componentProp, queryAttr, stateType, builder, logger))
                {
                    mappingsCount++;
                }
                continue;
            }

            // Check for AutoSyncWithRoute attribute (Phase 2 - not yet supported)
#pragma warning disable EASB001 // Experimental feature - internal usage
            var routeAttr = componentProp.GetCustomAttribute<AutoSyncWithRouteAttribute>();
#pragma warning restore EASB001
            if (routeAttr != null)
            {
                throw new NotSupportedException(
                    $"[AutoSyncWithRoute] on '{componentProp.Name}' is not supported in Phase 3.\n\n" +
                    $"Route parameter synchronization will be available in Phase 2.\n" +
                    $"For now, only [AutoSyncWithQuery] is supported.");
            }
        }

        logger?.LogDebug(
            "Discovered {Count} auto-sync mappings for {ComponentType}",
            mappingsCount,
            componentType.Name);

        return mappingsCount;
    }

    /// <summary>
    /// Creates a query parameter mapping using reflection and expression trees.
    /// </summary>
#pragma warning disable EASB001 // Experimental feature - internal usage
    private static bool TryCreateQueryMapping<TState>(
        object component,
        PropertyInfo componentProp,
        AutoSyncWithQueryAttribute attr,
        Type stateType,
        IUrlSyncBuilder<TState> builder,
        ILogger? logger)
        where TState : notnull
#pragma warning restore EASB001
    {
        // Find matching state property
        var statePropName = attr.StatePropertyName ?? FindMatchingStateProperty(componentProp.Name, stateType);

        if (statePropName == null)
        {
            logger?.LogWarning(
                "Could not find matching state property for component property {ComponentProp}. " +
                "Tried exact match, Current prefix, and Value suffix. " +
                "Specify StatePropertyName explicitly or ensure a matching property exists.",
                componentProp.Name);
            return false;
        }

        var stateProp = stateType.GetProperty(statePropName, BindingFlags.Public | BindingFlags.Instance);
        if (stateProp == null)
        {
            logger?.LogWarning(
                "State property '{StateProp}' not found on {StateType}",
                statePropName,
                stateType.Name);
            return false;
        }

        // Verify types match
        if (componentProp.PropertyType != stateProp.PropertyType)
        {
            logger?.LogWarning(
                "Type mismatch: Component property '{ComponentProp}' is {ComponentType}, " +
                "but state property '{StateProp}' is {StateType}",
                componentProp.Name,
                componentProp.PropertyType.Name,
                stateProp.Name,
                stateProp.PropertyType.Name);
            return false;
        }

        // Create mapping using reflection
        var paramType = componentProp.PropertyType;
        var methodInfo = typeof(ConventionBasedSyncHelper)
            .GetMethod(nameof(CreateMappingGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TState), paramType);

        methodInfo.Invoke(null, new object?[]
        {
            component,
            componentProp,
            stateProp,
            attr.QueryParamName ?? componentProp.Name,
            builder
        });

        logger?.LogDebug(
            "Auto-mapped: {ComponentProp} ↔ state.{StateProp} (query param: {QueryParam})",
            componentProp.Name,
            stateProp.Name,
            attr.QueryParamName ?? componentProp.Name);

        return true;
    }

    /// <summary>
    /// Generic method to create a typed mapping.
    /// Called via reflection with the actual parameter type.
    /// </summary>
    private static void CreateMappingGeneric<TState, TParam>(
        object component,
        PropertyInfo componentProp,
        PropertyInfo stateProp,
        string queryParamName,
        IUrlSyncBuilder<TState> builder)
        where TState : notnull
    {
        // Create expression: () => component.PropertyName
        var componentParam = Expression.Constant(component);
        var componentPropAccess = Expression.Property(componentParam, componentProp);
        var componentLambda = Expression.Lambda<Func<TParam>>(componentPropAccess);

        // Create expression: s => s.PropertyName
        var stateParam = Expression.Parameter(typeof(TState), "s");
        var statePropAccess = Expression.Property(stateParam, stateProp);
        var stateLambda = Expression.Lambda<Func<TState, TParam>>(statePropAccess, stateParam);

        // Configure builder
        builder.SyncQueryParam(componentLambda, stateLambda, queryParamName);
    }

    /// <summary>
    /// Finds a matching state property using convention-based rules.
    /// </summary>
    /// <remarks>
    /// Matching rules (in order):
    /// 1. Exact match: "Page" → "Page"
    /// 2. With "Current" prefix: "Page" → "CurrentPage"
    /// 3. With "Value" suffix: "Page" → "PageValue"
    /// 4. Case-insensitive match
    /// </remarks>
    private static string? FindMatchingStateProperty(string componentPropName, Type stateType)
    {
        var properties = stateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Rule 1: Exact match
        if (properties.Any(p => p.Name == componentPropName))
            return componentPropName;

        // Rule 2: Current prefix
        var currentName = "Current" + componentPropName;
        if (properties.Any(p => p.Name == currentName))
            return currentName;

        // Rule 3: Value suffix
        var valueName = componentPropName + "Value";
        if (properties.Any(p => p.Name == valueName))
            return valueName;

        // Rule 4: Case-insensitive
        var match = properties.FirstOrDefault(p =>
            string.Equals(p.Name, componentPropName, StringComparison.OrdinalIgnoreCase));

        return match?.Name;
    }
}
