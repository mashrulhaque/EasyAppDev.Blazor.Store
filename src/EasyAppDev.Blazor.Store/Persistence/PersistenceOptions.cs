using System.Text.Json;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Configuration options for state persistence.
/// </summary>
/// <typeparam name="TState">The type of state being persisted.</typeparam>
public class PersistenceOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the storage key for persisting state.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets the debounce duration in milliseconds.
    /// When set to a value greater than 0, saves are debounced to reduce storage writes.
    /// Default: 0 (no debounce).
    /// </summary>
    public int DebounceMs { get; init; } = 0;

    /// <summary>
    /// Gets or sets the JSON serialization options.
    /// Default: CamelCase property naming.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; init; }

    /// <summary>
    /// Gets or sets whether to hydrate state on store initialization.
    /// Default: true.
    /// </summary>
    public bool HydrateOnInit { get; init; } = true;

    /// <summary>
    /// Gets or sets the callback invoked when hydration succeeds.
    /// Receives the loaded state.
    /// </summary>
    public Action<TState>? OnHydrationSuccess { get; init; }

    /// <summary>
    /// Gets or sets the callback invoked when hydration fails.
    /// Receives the exception that occurred.
    /// </summary>
    public Action<Exception>? OnHydrationFailure { get; init; }

    /// <summary>
    /// Gets or sets the callback invoked when no persisted state is found.
    /// </summary>
    public Action? OnHydrationSkipped { get; init; }

    /// <summary>
    /// Gets or sets a predicate that determines whether to persist a state change.
    /// Parameters: previous state, current state, action name.
    /// Return true to persist, false to skip.
    /// Default: always persist.
    /// </summary>
    public Func<TState, TState, string?, bool>? ShouldPersist { get; init; }

    /// <summary>
    /// Gets or sets a function to transform the state when loading from storage.
    /// Useful for clearing sensitive data or migrating state schema.
    /// </summary>
    public Func<TState, TState>? TransformOnLoad { get; init; }

    /// <summary>
    /// Gets or sets a function to transform the state before saving to storage.
    /// Useful for excluding sensitive data from persistence.
    /// </summary>
    public Func<TState, TState>? TransformOnSave { get; init; }
}

/// <summary>
/// Factory for creating persistence options with a fluent API.
/// </summary>
public static class PersistenceOptions
{
    /// <summary>
    /// Creates a new persistence options builder.
    /// </summary>
    /// <typeparam name="TState">The type of state being persisted.</typeparam>
    /// <param name="key">The storage key.</param>
    /// <returns>A new options instance.</returns>
    public static PersistenceOptions<TState> Create<TState>(string key) where TState : notnull
    {
        return new PersistenceOptions<TState> { Key = key };
    }
}
