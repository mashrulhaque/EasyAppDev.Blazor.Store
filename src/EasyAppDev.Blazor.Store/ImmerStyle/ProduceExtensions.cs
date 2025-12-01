// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using System.Linq.Expressions;
using System.Reflection;

namespace EasyAppDev.Blazor.Store.ImmerStyle;

/// <summary>
/// Extension methods for Immer-style state updates using a mutable-looking syntax.
/// </summary>
public static class ProduceExtensions
{
    /// <summary>
    /// Updates state using an Immer-style recipe that appears mutable but produces
    /// immutable updates using record 'with' expressions.
    /// </summary>
    /// <typeparam name="TState">The type of state (must be a record type).</typeparam>
    /// <param name="store">The store to update.</param>
    /// <param name="recipe">A recipe describing the updates to make.</param>
    /// <param name="action">Optional action name for DevTools.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <example>
    /// <code>
    /// await store.ProduceAsync(draft => draft.Set(s => s.Count, 5));
    /// await store.ProduceAsync(draft => draft.Set(s => s.User.Name, "John"));
    /// await store.ProduceAsync(draft => draft
    ///     .Set(s => s.Count, 10)
    ///     .SetNested(s => s.User, u => u.Name, "John")
    ///     .DictSet(s => s.Items, "key1", newItem));
    /// </code>
    /// </example>
    public static Task ProduceAsync<TState>(
        this IStore<TState> store,
        Action<IDraft<TState>> recipe,
        string? action = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(recipe);

        return store.UpdateAsync(state =>
        {
            var draft = new Draft<TState>(state);
            recipe(draft);
            return draft.Produce();
        }, action ?? "PRODUCE");
    }

    /// <summary>
    /// Updates state using multiple Immer-style operations.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="store">The store to update.</param>
    /// <param name="recipe">An async recipe describing the updates to make.</param>
    /// <param name="action">Optional action name.</param>
    public static async Task ProduceAsync<TState>(
        this IStore<TState> store,
        Func<IDraft<TState>, Task> recipe,
        string? action = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(recipe);

        var state = store.GetState();
        var draft = new Draft<TState>(state);
        await recipe(draft);
        await store.UpdateAsync(_ => draft.Produce(), action ?? "PRODUCE_ASYNC");
    }

    /// <summary>
    /// Synchronously updates state using an Immer-style recipe.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="state">The initial state.</param>
    /// <param name="recipe">A recipe describing the updates to make.</param>
    /// <returns>The new state with all modifications applied.</returns>
    public static TState Produce<TState>(this TState state, Action<IDraft<TState>> recipe)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(recipe);

        var draft = new Draft<TState>(state);
        recipe(draft);
        return draft.Produce();
    }

    /// <summary>
    /// Creates a draft for producing an updated state outside of a store context.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="state">The initial state.</param>
    /// <returns>A draft for making modifications.</returns>
    public static IDraft<TState> CreateDraft<TState>(this TState state)
        where TState : notnull
    {
        return new Draft<TState>(state);
    }
}

/// <summary>
/// Interface for draft state modifications using Immer-style syntax.
/// </summary>
/// <typeparam name="TState">The type of state being modified.</typeparam>
public interface IDraft<TState> where TState : notnull
{
    /// <summary>
    /// Gets the current state being modified.
    /// </summary>
    TState Current { get; }

    /// <summary>
    /// Sets a property to a new value. Supports deeply nested paths.
    /// </summary>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="selector">Expression selecting the property to modify (e.g., s => s.User.Profile.Name).</param>
    /// <param name="value">The new value.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Set<TValue>(Expression<Func<TState, TValue>> selector, TValue value);

    /// <summary>
    /// Updates a property using a transformation function.
    /// </summary>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="selector">Expression selecting the property to modify.</param>
    /// <param name="updater">Function to transform the current value.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Update<TValue>(Expression<Func<TState, TValue>> selector, Func<TValue, TValue> updater);

    /// <summary>
    /// Updates a nested object using a draft pattern. Allows modifying deeply nested
    /// objects without manual 'with' expressions.
    /// </summary>
    /// <typeparam name="TNested">The type of the nested object.</typeparam>
    /// <param name="selector">Expression selecting the nested object.</param>
    /// <param name="recipe">A recipe to modify the nested object.</param>
    /// <returns>The draft for chaining.</returns>
    /// <example>
    /// <code>
    /// draft.UpdateNested(s => s.User, userDraft => userDraft
    ///     .Set(u => u.Name, "John")
    ///     .Set(u => u.Age, 30));
    /// </code>
    /// </example>
    IDraft<TState> UpdateNested<TNested>(
        Expression<Func<TState, TNested>> selector,
        Action<IDraft<TNested>> recipe) where TNested : notnull;

    /// <summary>
    /// Increments a numeric property.
    /// </summary>
    /// <param name="selector">Expression selecting the property to increment.</param>
    /// <param name="amount">Amount to increment by (default 1).</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Increment(Expression<Func<TState, int>> selector, int amount = 1);

    /// <summary>
    /// Increments a long property.
    /// </summary>
    IDraft<TState> Increment(Expression<Func<TState, long>> selector, long amount = 1);

    /// <summary>
    /// Increments a double property.
    /// </summary>
    IDraft<TState> Increment(Expression<Func<TState, double>> selector, double amount = 1.0);

    /// <summary>
    /// Decrements a numeric property.
    /// </summary>
    /// <param name="selector">Expression selecting the property to decrement.</param>
    /// <param name="amount">Amount to decrement by (default 1).</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Decrement(Expression<Func<TState, int>> selector, int amount = 1);

    /// <summary>
    /// Decrements a long property.
    /// </summary>
    IDraft<TState> Decrement(Expression<Func<TState, long>> selector, long amount = 1);

    /// <summary>
    /// Decrements a double property.
    /// </summary>
    IDraft<TState> Decrement(Expression<Func<TState, double>> selector, double amount = 1.0);

    /// <summary>
    /// Toggles a boolean property.
    /// </summary>
    /// <param name="selector">Expression selecting the boolean property.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Toggle(Expression<Func<TState, bool>> selector);

    // --- ImmutableList operations ---

    /// <summary>
    /// Appends an item to an ImmutableList property.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the list.</typeparam>
    /// <param name="selector">Expression selecting the list property.</param>
    /// <param name="item">The item to append.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Append<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        TItem item);

    /// <summary>
    /// Appends multiple items to an ImmutableList property.
    /// </summary>
    IDraft<TState> AppendRange<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        IEnumerable<TItem> items);

    /// <summary>
    /// Inserts an item at a specific index in an ImmutableList property.
    /// </summary>
    IDraft<TState> Insert<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        TItem item);

    /// <summary>
    /// Removes an item from an ImmutableList property.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the list.</typeparam>
    /// <param name="selector">Expression selecting the list property.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> Remove<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        TItem item);

    /// <summary>
    /// Removes an item at a specific index from an ImmutableList property.
    /// </summary>
    IDraft<TState> RemoveAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index);

    /// <summary>
    /// Removes items matching a predicate from an ImmutableList property.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the list.</typeparam>
    /// <param name="selector">Expression selecting the list property.</param>
    /// <param name="predicate">Predicate to match items to remove.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> RemoveAll<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        Func<TItem, bool> predicate);

    /// <summary>
    /// Replaces an item at a specific index in an ImmutableList property.
    /// </summary>
    IDraft<TState> SetAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        TItem item);

    /// <summary>
    /// Updates an item at a specific index using a transformation function.
    /// </summary>
    IDraft<TState> UpdateAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        Func<TItem, TItem> updater);

    /// <summary>
    /// Updates items matching a predicate using a transformation function.
    /// </summary>
    IDraft<TState> UpdateWhere<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        Func<TItem, bool> predicate,
        Func<TItem, TItem> updater);

    /// <summary>
    /// Clears all items from an ImmutableList property.
    /// </summary>
    IDraft<TState> Clear<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector);

    // --- ImmutableDictionary operations ---

    /// <summary>
    /// Sets or adds a key-value pair in an ImmutableDictionary property.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="selector">Expression selecting the dictionary property.</param>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>The draft for chaining.</returns>
    IDraft<TState> DictSet<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key,
        TValue value) where TKey : notnull;

    /// <summary>
    /// Updates a value in an ImmutableDictionary using a transformation function.
    /// </summary>
    IDraft<TState> DictUpdate<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key,
        Func<TValue, TValue> updater) where TKey : notnull;

    /// <summary>
    /// Removes a key from an ImmutableDictionary property.
    /// </summary>
    IDraft<TState> DictRemove<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key) where TKey : notnull;

    /// <summary>
    /// Removes multiple keys from an ImmutableDictionary property.
    /// </summary>
    IDraft<TState> DictRemoveRange<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        IEnumerable<TKey> keys) where TKey : notnull;

    /// <summary>
    /// Clears all items from an ImmutableDictionary property.
    /// </summary>
    IDraft<TState> DictClear<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector)
        where TKey : notnull;

    /// <summary>
    /// Adds or updates multiple key-value pairs in an ImmutableDictionary property.
    /// </summary>
    IDraft<TState> DictSetRange<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        IEnumerable<KeyValuePair<TKey, TValue>> items) where TKey : notnull;

    // --- String operations ---

    /// <summary>
    /// Appends text to a string property.
    /// </summary>
    IDraft<TState> Concat(Expression<Func<TState, string>> selector, string value);

    /// <summary>
    /// Replaces occurrences in a string property.
    /// </summary>
    IDraft<TState> Replace(
        Expression<Func<TState, string>> selector,
        string oldValue,
        string newValue);

    /// <summary>
    /// Trims whitespace from a string property.
    /// </summary>
    IDraft<TState> Trim(Expression<Func<TState, string>> selector);

    // --- Nullable operations ---

    /// <summary>
    /// Sets a nullable property to null.
    /// </summary>
    IDraft<TState> SetNull<TValue>(Expression<Func<TState, TValue?>> selector) where TValue : class;

    /// <summary>
    /// Sets a nullable value type property to null.
    /// </summary>
    IDraft<TState> SetNull<TValue>(Expression<Func<TState, TValue?>> selector) where TValue : struct;

    /// <summary>
    /// Produces the final immutable state with all modifications applied.
    /// </summary>
    /// <returns>The new state with all changes.</returns>
    TState Produce();
}

/// <summary>
/// Default implementation of IDraft that tracks and applies modifications.
/// </summary>
internal sealed class Draft<TState> : IDraft<TState> where TState : notnull
{
    private TState _current;
    private readonly List<Func<TState, TState>> _modifications = new();

    public Draft(TState initial)
    {
        _current = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    public TState Current => _current;

    public IDraft<TState> Set<TValue>(Expression<Func<TState, TValue>> selector, TValue value)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var path = GetPropertyPath(selector);
        _modifications.Add(state => SetNestedProperty(state, path, value));
        return this;
    }

    public IDraft<TState> Update<TValue>(Expression<Func<TState, TValue>> selector, Func<TValue, TValue> updater)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(updater);

        var path = GetPropertyPath(selector);
        _modifications.Add(state =>
        {
            var compiled = selector.Compile();
            var currentValue = compiled(state);
            var newValue = updater(currentValue);
            return SetNestedProperty(state, path, newValue);
        });
        return this;
    }

    public IDraft<TState> UpdateNested<TNested>(
        Expression<Func<TState, TNested>> selector,
        Action<IDraft<TNested>> recipe) where TNested : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(recipe);

        return Update(selector, nested =>
        {
            var nestedDraft = new Draft<TNested>(nested);
            recipe(nestedDraft);
            return nestedDraft.Produce();
        });
    }

    // --- Numeric operations ---

    public IDraft<TState> Increment(Expression<Func<TState, int>> selector, int amount = 1)
        => Update(selector, v => v + amount);

    public IDraft<TState> Increment(Expression<Func<TState, long>> selector, long amount = 1)
        => Update(selector, v => v + amount);

    public IDraft<TState> Increment(Expression<Func<TState, double>> selector, double amount = 1.0)
        => Update(selector, v => v + amount);

    public IDraft<TState> Decrement(Expression<Func<TState, int>> selector, int amount = 1)
        => Update(selector, v => v - amount);

    public IDraft<TState> Decrement(Expression<Func<TState, long>> selector, long amount = 1)
        => Update(selector, v => v - amount);

    public IDraft<TState> Decrement(Expression<Func<TState, double>> selector, double amount = 1.0)
        => Update(selector, v => v - amount);

    public IDraft<TState> Toggle(Expression<Func<TState, bool>> selector)
        => Update(selector, v => !v);

    // --- ImmutableList operations ---

    public IDraft<TState> Append<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        TItem item)
        => Update(selector, list => list.Add(item));

    public IDraft<TState> AppendRange<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        IEnumerable<TItem> items)
        => Update(selector, list => list.AddRange(items));

    public IDraft<TState> Insert<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        TItem item)
        => Update(selector, list => list.Insert(index, item));

    public IDraft<TState> Remove<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        TItem item)
        => Update(selector, list => list.Remove(item));

    public IDraft<TState> RemoveAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index)
        => Update(selector, list => list.RemoveAt(index));

    public IDraft<TState> RemoveAll<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        Func<TItem, bool> predicate)
        => Update(selector, list => list.RemoveAll(item => predicate(item)));

    public IDraft<TState> SetAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        TItem item)
        => Update(selector, list => list.SetItem(index, item));

    public IDraft<TState> UpdateAt<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        int index,
        Func<TItem, TItem> updater)
        => Update(selector, list => list.SetItem(index, updater(list[index])));

    public IDraft<TState> UpdateWhere<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector,
        Func<TItem, bool> predicate,
        Func<TItem, TItem> updater)
    {
        return Update(selector, list =>
        {
            var builder = list.ToBuilder();
            for (int i = 0; i < builder.Count; i++)
            {
                if (predicate(builder[i]))
                {
                    builder[i] = updater(builder[i]);
                }
            }
            return builder.ToImmutable();
        });
    }

    public IDraft<TState> Clear<TItem>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableList<TItem>>> selector)
        => Update(selector, _ => System.Collections.Immutable.ImmutableList<TItem>.Empty);

    // --- ImmutableDictionary operations ---

    public IDraft<TState> DictSet<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key,
        TValue value) where TKey : notnull
        => Update(selector, dict => dict.SetItem(key, value));

    public IDraft<TState> DictUpdate<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key,
        Func<TValue, TValue> updater) where TKey : notnull
    {
        return Update(selector, dict =>
        {
            if (dict.TryGetValue(key, out var existingValue))
            {
                return dict.SetItem(key, updater(existingValue));
            }
            return dict;
        });
    }

    public IDraft<TState> DictRemove<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        TKey key) where TKey : notnull
        => Update(selector, dict => dict.Remove(key));

    public IDraft<TState> DictRemoveRange<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        IEnumerable<TKey> keys) where TKey : notnull
        => Update(selector, dict => dict.RemoveRange(keys));

    public IDraft<TState> DictClear<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector)
        where TKey : notnull
        => Update(selector, _ => System.Collections.Immutable.ImmutableDictionary<TKey, TValue>.Empty);

    public IDraft<TState> DictSetRange<TKey, TValue>(
        Expression<Func<TState, System.Collections.Immutable.ImmutableDictionary<TKey, TValue>>> selector,
        IEnumerable<KeyValuePair<TKey, TValue>> items) where TKey : notnull
        => Update(selector, dict => dict.SetItems(items));

    // --- String operations ---

    public IDraft<TState> Concat(Expression<Func<TState, string>> selector, string value)
        => Update(selector, s => s + value);

    public IDraft<TState> Replace(
        Expression<Func<TState, string>> selector,
        string oldValue,
        string newValue)
        => Update(selector, s => s.Replace(oldValue, newValue));

    public IDraft<TState> Trim(Expression<Func<TState, string>> selector)
        => Update(selector, s => s.Trim());

    // --- Nullable operations ---

    public IDraft<TState> SetNull<TValue>(Expression<Func<TState, TValue?>> selector) where TValue : class
        => Set(selector, null);

    IDraft<TState> IDraft<TState>.SetNull<TValue>(Expression<Func<TState, TValue?>> selector)
        => Set(selector, null);

    public TState Produce()
    {
        var result = _current;
        foreach (var modification in _modifications)
        {
            result = modification(result);
        }
        return result;
    }

    private static List<PropertyInfo> GetPropertyPath<TValue>(Expression<Func<TState, TValue>> selector)
    {
        var path = new List<PropertyInfo>();
        var expression = selector.Body;

        while (expression is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo prop)
            {
                path.Insert(0, prop);
                expression = memberExpr.Expression!;
            }
            else
            {
                throw new ArgumentException(
                    $"Expression must only access properties. Found: {memberExpr.Member.MemberType}",
                    nameof(selector));
            }
        }

        if (expression is not ParameterExpression)
        {
            throw new ArgumentException(
                "Expression must be a simple property path like x => x.Property or x => x.Nested.Property",
                nameof(selector));
        }

        if (path.Count == 0)
        {
            throw new ArgumentException(
                "Expression must access at least one property",
                nameof(selector));
        }

        return path;
    }

    private static TState SetNestedProperty<TValue>(TState state, List<PropertyInfo> path, TValue value)
    {
        if (path.Count == 1)
        {
            return SetProperty(state, path[0], value);
        }

        // For nested properties, we need to rebuild the entire chain
        return SetNestedPropertyRecursive(state, path, 0, value);
    }

    private static TObj SetNestedPropertyRecursive<TObj, TValue>(
        TObj obj,
        List<PropertyInfo> path,
        int index,
        TValue value)
    {
        if (index >= path.Count)
        {
            return obj;
        }

        var prop = path[index];

        if (index == path.Count - 1)
        {
            // Last property - set the value
            return SetProperty(obj, prop, value);
        }

        // Get current value and recursively update it
        var currentValue = prop.GetValue(obj);
        if (currentValue == null)
        {
            throw new InvalidOperationException(
                $"Cannot set nested property because '{prop.Name}' is null");
        }

        // Recursively set the nested property
        var nestedUpdated = SetNestedPropertyRecursive(currentValue, path, index + 1, value);

        // Now set the updated nested object back on the parent
        return SetProperty(obj, prop, nestedUpdated);
    }

    private static TObj SetProperty<TObj, TValue>(TObj obj, PropertyInfo prop, TValue value)
    {
        var type = typeof(TObj);

        // Check if it's a record type with a with expression (has <Clone>$ method)
        var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public);
        if (cloneMethod != null)
        {
            // It's a record - use clone and set
            var clone = (TObj)cloneMethod.Invoke(obj, null)!;

            // Find the init accessor
            var setter = prop.GetSetMethod();
            if (setter != null)
            {
                setter.Invoke(clone, new object?[] { value });
                return clone;
            }

            // Try to find the backing field for init-only property
            var backingField = type.GetField($"<{prop.Name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (backingField != null)
            {
                backingField.SetValue(clone, value);
                return clone;
            }
        }

        // Fallback for classes with setters
        var setMethod = prop.GetSetMethod();
        if (setMethod != null)
        {
            var clone = Clone(obj);
            setMethod.Invoke(clone, new object?[] { value });
            return clone;
        }

        throw new InvalidOperationException(
            $"Cannot set property '{prop.Name}' on type '{type.Name}'. " +
            "The property must be settable or the type must be a record with init properties.");
    }

    private static TObj Clone<TObj>(TObj obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var type = typeof(TObj);

        // Try record clone method
        var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public);
        if (cloneMethod != null)
        {
            return (TObj)cloneMethod.Invoke(obj, null)!;
        }

        // Try ICloneable
        if (obj is ICloneable cloneable)
        {
            return (TObj)cloneable.Clone();
        }

        // Fallback: create new instance with same constructor parameters
        throw new InvalidOperationException(
            $"Cannot clone object of type '{type.Name}'. " +
            "Consider using a record type for state.");
    }
}
