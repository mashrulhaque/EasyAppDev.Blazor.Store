using System.Linq.Expressions;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Configuration for URL-state synchronization.
/// Provides methods to apply URL parameters to state and build URLs from state.
/// </summary>
internal sealed class UrlSyncConfiguration<TState> where TState : notnull
{
    private readonly List<object> _propertyMappings;
    public TimeSpan Debounce { get; }
    public UrlSyncNavigationMode NavigationMode { get; }
    public HashSet<string> ExcludedActions { get; }
    public Action<string, Exception>? OnConversionError { get; }
    public Action<Exception>? OnError { get; }

    public UrlSyncConfiguration(
        List<object> propertyMappings,
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
            ExtractValue((dynamic)mapping, urlParams, values);
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
            ExtractComponentValue((dynamic)mapping, values);
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
            AddQueryParam((dynamic)mapping, state, queryParams);
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
            if (HasMappingChanged((dynamic)mapping, oldState, newState))
                return true;
        }

        return false;
    }

    // Dynamic dispatch methods (called via dynamic keyword)

    private void ExtractValue<TParam>(PropertyMapping<TState, TParam> mapping, ParameterDictionary urlParams, Dictionary<string, object?> values)
    {
        var urlValue = urlParams.Get(mapping.QueryParamName);
        var convertedValue = mapping.Converter.FromUrl(urlValue);
        values[mapping.QueryParamName] = convertedValue;
    }

    private void ExtractComponentValue<TParam>(PropertyMapping<TState, TParam> mapping, Dictionary<string, object?> values)
    {
        var value = mapping.ComponentParameterGetter();
        values[mapping.QueryParamName] = value;
    }

    private void AddQueryParam<TParam>(PropertyMapping<TState, TParam> mapping, TState state, Dictionary<string, object?> queryParams)
    {
        var value = mapping.StateSelector(state);
        var urlValue = mapping.Converter.ToUrl(value);

        if (urlValue != null)
        {
            queryParams[mapping.QueryParamName] = urlValue;
        }
    }

    private bool HasMappingChanged<TParam>(PropertyMapping<TState, TParam> mapping, TState oldState, TState newState)
    {
        var oldValue = mapping.StateSelector(oldState);
        var newValue = mapping.StateSelector(newState);

        return !EqualityComparer<TParam>.Default.Equals(oldValue, newValue);
    }

    /// <summary>
    /// Applies component parameter values to state using reflection.
    /// This creates a new state instance with updated property values.
    /// </summary>
    public TState ApplyComponentParamsToState(TState currentState)
    {
        var stateType = typeof(TState);
        var properties = stateType.GetProperties();
        var propertyValues = new Dictionary<string, object?>();

        // Get current state property values
        foreach (var prop in properties)
        {
            propertyValues[prop.Name] = prop.GetValue(currentState);
        }

        // Update with component parameter values
        foreach (var mapping in _propertyMappings)
        {
            UpdatePropertyValue((dynamic)mapping, propertyValues);
        }

        // Create new state instance using primary constructor
        var constructors = stateType.GetConstructors();
        var primaryConstructor = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (primaryConstructor == null)
            return currentState;

        var ctorParams = primaryConstructor.GetParameters();
        var ctorArgs = new object?[ctorParams.Length];

        for (int i = 0; i < ctorParams.Length; i++)
        {
            var paramName = ctorParams[i].Name;
            // Match case-insensitive (records use lowercase param names)
            var matchingKey = propertyValues.Keys.FirstOrDefault(k =>
                string.Equals(k, paramName, StringComparison.OrdinalIgnoreCase));

            ctorArgs[i] = matchingKey != null
                ? propertyValues[matchingKey]
                : ctorParams[i].DefaultValue;
        }

        return (TState)primaryConstructor.Invoke(ctorArgs);
    }

    private void UpdatePropertyValue<TParam>(PropertyMapping<TState, TParam> mapping, Dictionary<string, object?> propertyValues)
    {
        // Get property name from state selector expression
        var expression = mapping.StateSelectorExpression;
        var body = expression.Body;

        if (body is MemberExpression memberExpr)
        {
            var propertyName = memberExpr.Member.Name;
            var componentValue = mapping.ComponentParameterGetter();
            propertyValues[propertyName] = componentValue;
        }
    }
}
