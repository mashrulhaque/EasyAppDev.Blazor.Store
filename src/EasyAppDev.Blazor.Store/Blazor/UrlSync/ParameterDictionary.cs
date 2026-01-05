namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Immutable dictionary wrapper for URL parameters (query and route parameters).
/// Provides type-safe access to parameter values.
/// </summary>
internal sealed class ParameterDictionary
{
    private readonly IReadOnlyDictionary<string, string?> _parameters;

    public ParameterDictionary(IReadOnlyDictionary<string, string?> parameters)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Gets a parameter value by name.
    /// Returns null if parameter doesn't exist.
    /// </summary>
    public string? Get(string name)
    {
        return _parameters.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    public bool Contains(string name)
    {
        return _parameters.ContainsKey(name);
    }

    /// <summary>
    /// Gets all parameter names.
    /// </summary>
    public IEnumerable<string> Keys => _parameters.Keys;
}
