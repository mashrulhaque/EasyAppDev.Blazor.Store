using System.Linq.Expressions;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Represents a bidirectional mapping between a URL parameter and a state property.
/// </summary>
/// <typeparam name="TState">The state type</typeparam>
/// <typeparam name="TParam">The parameter type</typeparam>
internal sealed class PropertyMapping<TState, TParam> where TState : notnull
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
    }
}
