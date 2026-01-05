using System.Linq.Expressions;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Fluent builder implementation for configuring URL-state synchronization.
/// </summary>
internal sealed class UrlSyncBuilder<TState> : IUrlSyncBuilder<TState> where TState : notnull
{
    private readonly List<object> _propertyMappings = new();
    private TimeSpan _debounce = TimeSpan.FromMilliseconds(300); // Default 300ms
    private UrlSyncNavigationMode _navigationMode = UrlSyncNavigationMode.Replace; // Default Replace
    private readonly HashSet<string> _excludedActions = new();
    private Action<string, Exception>? _onConversionError;
    private Action<Exception>? _onError;

    public IUrlSyncBuilder<TState> SyncQueryParam<TParam>(
        Expression<Func<TParam>> componentProperty,
        Expression<Func<TState, TParam>> stateProperty,
        string? queryParamName = null)
    {
        if (componentProperty == null)
            throw new ArgumentNullException(nameof(componentProperty));
        if (stateProperty == null)
            throw new ArgumentNullException(nameof(stateProperty));

        // Validate that TParam is a supported type
        ValidateSupportedType<TParam>();

        // Extract property name from expression
        var componentPropName = GetPropertyName(componentProperty);
        var paramName = queryParamName ?? componentPropName;

        // Compile the expressions to get getters
        var componentGetter = componentProperty.Compile();
        var stateSelector = stateProperty.Compile();

        // Create converter
        var converter = new DefaultUrlValueConverter<TParam>(_onConversionError);

        // Create mapping
        var mapping = new PropertyMapping<TState, TParam>(
            paramName,
            stateSelector,
            stateProperty,  // Pass expression for metadata
            componentGetter,
            converter
        );

        _propertyMappings.Add(mapping);

        return this;
    }

    public IUrlSyncBuilder<TState> WithDebounce(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Debounce duration cannot be negative", nameof(duration));

        _debounce = duration;
        return this;
    }

    public IUrlSyncBuilder<TState> WithNavigationMode(UrlSyncNavigationMode mode)
    {
        _navigationMode = mode;
        return this;
    }

    public IUrlSyncBuilder<TState> ExcludeActions(params string[] actions)
    {
        if (actions == null)
            throw new ArgumentNullException(nameof(actions));

        foreach (var action in actions.Where(a => !string.IsNullOrEmpty(a)))
        {
            _excludedActions.Add(action);
        }

        return this;
    }

    public IUrlSyncBuilder<TState> OnConversionError(Action<string, Exception> handler)
    {
        _onConversionError = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public IUrlSyncBuilder<TState> OnError(Action<Exception> handler)
    {
        _onError = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    /// <summary>
    /// Builds the URL sync configuration.
    /// Internal method called by UrlSyncStoreComponent.
    /// </summary>
    internal UrlSyncConfiguration<TState> Build()
    {
        return new UrlSyncConfiguration<TState>(
            _propertyMappings,
            _debounce,
            _navigationMode,
            _excludedActions,
            _onConversionError,
            _onError
        );
    }

    /// <summary>
    /// Validates that the type is supported for URL sync.
    /// Throws if type is a reference type (except string).
    /// </summary>
    private static void ValidateSupportedType<TParam>()
    {
        var paramType = typeof(TParam);
        var underlyingType = Nullable.GetUnderlyingType(paramType) ?? paramType;

        // Allow: primitives, string, Guid, DateTime, enums
        var isAllowed =
            underlyingType.IsPrimitive ||
            underlyingType == typeof(string) ||
            underlyingType == typeof(Guid) ||
            underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan) ||
            underlyingType == typeof(decimal) || // decimal is not primitive
            underlyingType.IsEnum;

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"URL sync does not support reference type '{paramType.Name}'.\n\n" +
                $"Supported types: int, bool, string, Guid, DateTime, enums, and their nullable variants.\n\n" +
                $"Reason: Reference types use reference equality, which causes infinite update loops.\n\n" +
                $"Solutions:\n" +
                $"  1. Store primitive representation in state (e.g., use Guid instead of User object)\n" +
                $"  2. Implement custom IUrlValueConverter<{paramType.Name}> (Phase 2)\n" +
                $"  3. Don't sync this property to the URL");
        }
    }

    /// <summary>
    /// Extracts property name from lambda expression.
    /// </summary>
    private static string GetPropertyName<T>(Expression<Func<T>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException(
            "Expression must be a simple property access (e.g., () => Page)",
            nameof(expression));
    }
}
