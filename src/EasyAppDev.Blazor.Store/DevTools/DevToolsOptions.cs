// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

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
    /// Creates default options with the given store name.
    /// </summary>
    public static DevToolsOptions<TState> Default(string? name = null) => new()
    {
        Name = name ?? typeof(TState).Name
    };
}
