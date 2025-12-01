// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Configuration options for Redux DevTools integration with enhanced time-travel support.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class DevToolsOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the name to display in Redux DevTools.
    /// Defaults to the state type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether time-travel debugging is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableTimeTravel { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of actions to keep in history.
    /// Default is 100.
    /// </summary>
    public int MaxHistory { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether action replay is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableActionReplay { get; set; } = true;

    /// <summary>
    /// Gets or sets whether state editing from DevTools is enabled.
    /// Default is false for safety.
    /// </summary>
    public bool EnableStateEditing { get; set; } = false;

    /// <summary>
    /// Gets or sets a filter function for actions.
    /// Actions returning false are not sent to DevTools.
    /// </summary>
    public Func<string, bool>? ActionFilter { get; set; }

    /// <summary>
    /// Gets or sets a function to sanitize state before sending to DevTools.
    /// Useful for hiding sensitive data.
    /// </summary>
    public Func<TState, TState>? StateSanitizer { get; set; }

    /// <summary>
    /// Gets or sets a function to transform actions before sending to DevTools.
    /// </summary>
    public Func<string, object>? ActionTransformer { get; set; }

    /// <summary>
    /// Gets or sets whether to trace action performance.
    /// Default is false.
    /// </summary>
    public bool TracePerformance { get; set; } = false;

    /// <summary>
    /// Gets or sets actions that should not be recorded.
    /// </summary>
    public HashSet<string> IgnoredActions { get; set; } = new()
    {
        "@@INIT"
    };

    /// <summary>
    /// Gets or sets whether to serialize state with indentation.
    /// Default is false for performance.
    /// </summary>
    public bool SerializeIndented { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to pause recording.
    /// Can be toggled at runtime.
    /// </summary>
    public bool Paused { get; set; } = false;

    /// <summary>
    /// Gets or sets the callback invoked when time-travel jump occurs.
    /// </summary>
    public Action<TState>? OnJump { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when an action is replayed.
    /// </summary>
    public Action<string>? OnActionReplay { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when state is imported.
    /// </summary>
    public Action<TState>? OnStateImport { get; set; }

    /// <summary>
    /// Gets or sets options for filtering sensitive data from DevTools output.
    /// When enabled, properties marked with [SensitiveData] or matching common
    /// sensitive property names (Password, Token, Secret, etc.) are redacted.
    /// </summary>
    /// <remarks>
    /// This provides automatic protection against accidentally exposing sensitive
    /// data in the browser's Redux DevTools extension. Consider enabling this
    /// for any store that may contain user credentials, tokens, or PII.
    /// </remarks>
    public SensitiveDataFilterOptions? SensitiveDataFilter { get; set; }

    /// <summary>
    /// Creates default options with the given store name.
    /// </summary>
    public static DevToolsOptions<TState> Default(string? name = null) => new()
    {
        Name = name ?? typeof(TState).Name
    };

    /// <summary>
    /// Creates options with sensitive data filtering enabled.
    /// </summary>
    /// <param name="name">Optional store name.</param>
    /// <returns>Options with sensitive data filtering enabled.</returns>
    public static DevToolsOptions<TState> WithSensitiveDataFiltering(string? name = null) => new()
    {
        Name = name ?? typeof(TState).Name,
        SensitiveDataFilter = new SensitiveDataFilterOptions { Enabled = true }
    };
}
