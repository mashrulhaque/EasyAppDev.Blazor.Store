using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Configuration for URL-state synchronization.
/// Provides methods to apply URL parameters to state and build URLs from state.
/// </summary>
internal sealed class UrlSyncConfiguration<TState> where TState : notnull
{
    private readonly List<IPropertyMapping<TState>> _propertyMappings;
    public TimeSpan Debounce { get; }
    public UrlSyncNavigationMode NavigationMode { get; }
    public HashSet<string> ExcludedActions { get; }
    public Action<string, Exception>? OnConversionError { get; }
    public Action<Exception>? OnError { get; }

    public UrlSyncConfiguration(
        List<IPropertyMapping<TState>> propertyMappings,
        TimeSpan debounce,
        UrlSyncNavigationMode navigationMode,
        HashSet<string> excludedActions,
        Action<string, Exception>? onConversionError,
        Action<Exception>? onError)
    {
        _propertyMappings = propertyMappings ?? throw new ArgumentNullException(nameof(propertyMappings));
        Debounce = debounce;
        NavigationMode = navigationMode;
        ExcludedActions = excludedActions ?? new HashSet<string>();
        OnConversionError = onConversionError;
        OnError = onError;
    }

    /// <summary>
    /// Extracts URL parameter values and converts them to typed values.
    /// Returns a dictionary of property name to converted value.
    /// </summary>
    public Dictionary<string, object?> ExtractUrlValues(ParameterDictionary urlParams)
    {
        var values = new Dictionary<string, object?>();

        foreach (var mapping in _propertyMappings)
        {
            mapping.ExtractUrlValue(urlParams, values);
        }

        return values;
    }

    /// <summary>
    /// Extracts component parameter values.
    /// Returns a dictionary of property name to current component parameter value.
    /// </summary>
    public Dictionary<string, object?> ExtractComponentValues()
    {
        var values = new Dictionary<string, object?>();

        foreach (var mapping in _propertyMappings)
        {
            mapping.ExtractComponentValue(values);
        }

        return values;
    }

    /// <summary>
    /// Builds a URL query string from the current state.
    /// Returns a dictionary of query parameter names to values.
    /// </summary>
    public Dictionary<string, object?> BuildUrlQueryParams(TState state)
    {
        var queryParams = new Dictionary<string, object?>();

        foreach (var mapping in _propertyMappings)
        {
            mapping.AddQueryParam(state, queryParams);
        }

        return queryParams;
    }

    /// <summary>
    /// Checks if any URL-relevant properties have changed between two states.
    /// </summary>
    public bool HasUrlRelevantChanges(TState oldState, TState newState)
    {
        foreach (var mapping in _propertyMappings)
        {
            if (mapping.HasChanged(oldState, newState))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies component parameter values to state using reflection.
    /// Returns a NEW state instance with the mapped values overlaid on the current
    /// property values - all unmapped properties retain their current values.
    /// </summary>
    /// <param name="currentState">The current store state.</param>
    /// <param name="presentQueryParams">
    /// The set of query parameter names actually present in the current URL.
    /// Mappings whose query parameter is absent are NOT applied, so hydrated/persisted
    /// state is not clobbered by component-parameter default values.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public TState ApplyComponentParamsToState(
        TState currentState,
        IReadOnlySet<string> presentQueryParams,
        ILogger? logger = null)
    {
        var stateType = typeof(TState);
        var properties = stateType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod != null && p.GetIndexParameters().Length == 0)
            .ToArray();

        // 1. Collect current property values from the existing state.
        var mergedValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in properties)
        {
            mergedValues[prop.Name] = prop.GetValue(currentState);
        }

        // 2. Overlay the mapped component-parameter values (presence-checked).
        //    Only mappings whose query parameter is actually present in the URL are
        //    applied - absent query params must not clobber state with component defaults.
        var overlaidNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in _propertyMappings)
        {
            if (!presentQueryParams.Contains(mapping.QueryParamName))
                continue;

            if (mapping.StatePropertyName is { } propertyName)
            {
                mergedValues[propertyName] = mapping.GetComponentParameterValue();
                overlaidNames.Add(propertyName);
            }
        }

        if (overlaidNames.Count == 0)
            return currentState;

        // Skip the rebuild entirely if every overlaid value already equals the current value.
        var anyChanged = false;
        foreach (var name in overlaidNames)
        {
            var prop = properties.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (prop == null)
                continue;

            var currentValue = prop.GetValue(currentState);
            if (!Equals(currentValue, mergedValues[name]))
            {
                anyChanged = true;
                break;
            }
        }

        if (!anyChanged)
            return currentState;

        // 3. Pick the constructor whose parameters best match property names (case-insensitive).
        //    Every parameter must be resolvable from a property value or an optional default.
        ConstructorInfo? bestCtor = null;
        var bestScore = -1;
        foreach (var ctor in stateType.GetConstructors())
        {
            var ctorParameters = ctor.GetParameters();
            var feasible = ctorParameters.All(p =>
                (p.Name != null && mergedValues.ContainsKey(p.Name)) || p.IsOptional);
            if (!feasible)
                continue;

            var score = ctorParameters.Count(p => p.Name != null && mergedValues.ContainsKey(p.Name));
            if (score > bestScore)
            {
                bestScore = score;
                bestCtor = ctor;
            }
        }

        if (bestCtor == null)
        {
            logger?.LogDebug(
                "UrlSync: no usable constructor found on {StateType}; state left unchanged",
                stateType.Name);
            return currentState;
        }

        // 4. Construct the new instance. For each ctor parameter use the merged
        //    (current-overlaid-with-parameters) property value when available,
        //    otherwise the parameter's default when optional. Never DBNull.
        var ctorParams = bestCtor.GetParameters();
        var ctorArgs = new object?[ctorParams.Length];
        var consumedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < ctorParams.Length; i++)
        {
            var param = ctorParams[i];
            if (param.Name != null && mergedValues.TryGetValue(param.Name, out var mergedValue))
            {
                ctorArgs[i] = mergedValue;
                consumedNames.Add(param.Name);
            }
            else
            {
                // Feasibility check above guarantees the parameter is optional here.
                ctorArgs[i] = param.HasDefaultValue && param.DefaultValue != DBNull.Value
                    ? param.DefaultValue
                    : (param.ParameterType.IsValueType
                        ? Activator.CreateInstance(param.ParameterType)
                        : null);
            }
        }

        var newState = (TState)bestCtor.Invoke(ctorArgs);

        // 5. Apply every remaining merged value through a writable setter so properties
        //    not covered by the constructor keep their current (or overlaid) values.
        foreach (var prop in properties)
        {
            if (consumedNames.Contains(prop.Name))
                continue;

            if (!mergedValues.TryGetValue(prop.Name, out var value))
                continue;

            if (prop.SetMethod == null)
                continue;

            if (IsInitOnly(prop))
            {
                // Init-only properties not covered by the constructor cannot be set safely.
                logger?.LogDebug(
                    "UrlSync: skipping init-only property {Property} on {StateType} - " +
                    "it is not covered by the selected constructor",
                    prop.Name,
                    stateType.Name);
                continue;
            }

            prop.SetValue(newState, value);
        }

        return newState;
    }

    /// <summary>
    /// Determines whether a property setter is init-only (init accessor).
    /// </summary>
    private static bool IsInitOnly(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod == null)
            return false;

        return setMethod.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(IsExternalInit));
    }
}
