using System.Linq.Expressions;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Non-generic (over TParam) view of a property mapping, allowing
/// <see cref="UrlSyncConfiguration{TState}"/> to operate on heterogeneous mappings
/// without dynamic dispatch.
/// </summary>
/// <typeparam name="TState">The state type</typeparam>
internal interface IPropertyMapping<TState> where TState : notnull
{
    /// <summary>
    /// Name of the query parameter in the URL.
    /// </summary>
    string QueryParamName { get; }

    /// <summary>
    /// Name of the mapped state property (null if the selector is not a simple member access).
    /// </summary>
    string? StatePropertyName { get; }

    /// <summary>
    /// Gets the current component parameter value (boxed).
    /// </summary>
    object? GetComponentParameterValue();

    /// <summary>
    /// Extracts and converts the URL parameter value into <paramref name="values"/>.
    /// </summary>
    void ExtractUrlValue(ParameterDictionary urlParams, Dictionary<string, object?> values);

    /// <summary>
    /// Extracts the current component parameter value into <paramref name="values"/>.
    /// </summary>
    void ExtractComponentValue(Dictionary<string, object?> values);

    /// <summary>
    /// Adds this mapping's query parameter (converted from state) to <paramref name="queryParams"/>.
    /// A null value is included so the parameter is removed from the URL.
    /// </summary>
    void AddQueryParam(TState state, Dictionary<string, object?> queryParams);

    /// <summary>
    /// Returns true if the mapped state value differs between the two states.
    /// </summary>
    bool HasChanged(TState oldState, TState newState);
}

/// <summary>
/// Represents a bidirectional mapping between a URL parameter and a state property.
/// </summary>
/// <typeparam name="TState">The state type</typeparam>
/// <typeparam name="TParam">The parameter type</typeparam>
internal sealed class PropertyMapping<TState, TParam> : IPropertyMapping<TState>
    where TState : notnull
{
    /// <summary>
    /// Name of the query parameter in the URL.
    /// </summary>
    public string QueryParamName { get; }

    /// <summary>
    /// Selector function to extract the value from state.
    /// </summary>
    public Func<TState, TParam> StateSelector { get; }

    /// <summary>
    /// Selector expression for metadata extraction.
    /// </summary>
    public Expression<Func<TState, TParam>> StateSelectorExpression { get; }

    /// <summary>
    /// Function to get the component parameter value.
    /// </summary>
    public Func<TParam> ComponentParameterGetter { get; }

    /// <summary>
    /// Converter for URL ↔ State value conversion.
    /// </summary>
    public IUrlValueConverter<TParam> Converter { get; }

    /// <inheritdoc />
    public string? StatePropertyName { get; }

    public PropertyMapping(
        string queryParamName,
        Func<TState, TParam> stateSelector,
        Expression<Func<TState, TParam>> stateSelectorExpression,
        Func<TParam> componentParameterGetter,
        IUrlValueConverter<TParam> converter)
    {
        QueryParamName = queryParamName ?? throw new ArgumentNullException(nameof(queryParamName));
        StateSelector = stateSelector ?? throw new ArgumentNullException(nameof(stateSelector));
        StateSelectorExpression = stateSelectorExpression ?? throw new ArgumentNullException(nameof(stateSelectorExpression));
        ComponentParameterGetter = componentParameterGetter ?? throw new ArgumentNullException(nameof(componentParameterGetter));
        Converter = converter ?? throw new ArgumentNullException(nameof(converter));

        StatePropertyName = stateSelectorExpression.Body is MemberExpression memberExpr
            ? memberExpr.Member.Name
            : null;
    }

    /// <inheritdoc />
    public object? GetComponentParameterValue() => ComponentParameterGetter();

    /// <inheritdoc />
    public void ExtractUrlValue(ParameterDictionary urlParams, Dictionary<string, object?> values)
    {
        var urlValue = urlParams.Get(QueryParamName);
        values[QueryParamName] = Converter.FromUrl(urlValue);
    }

    /// <inheritdoc />
    public void ExtractComponentValue(Dictionary<string, object?> values)
    {
        values[QueryParamName] = ComponentParameterGetter();
    }

    /// <inheritdoc />
    public void AddQueryParam(TState state, Dictionary<string, object?> queryParams)
    {
        var value = StateSelector(state);
        var urlValue = Converter.ToUrl(value);

        // A null value is intentionally included: GetUriWithQueryParameters treats a null
        // entry as "remove this query parameter", which clears stale params from the URL.
        queryParams[QueryParamName] = urlValue;
    }

    /// <inheritdoc />
    public bool HasChanged(TState oldState, TState newState)
    {
        var oldValue = StateSelector(oldState);
        var newValue = StateSelector(newState);

        return !EqualityComparer<TParam>.Default.Equals(oldValue, newValue);
    }
}
