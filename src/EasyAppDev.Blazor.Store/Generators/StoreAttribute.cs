namespace EasyAppDev.Blazor.Store.Generators;

/// <summary>
/// Marks a record for source generation of setter and updater methods.
/// </summary>
/// <remarks>
/// When applied to a record, the generator creates:
/// <list type="bullet">
/// <item><description>SetX(value) methods for each property</description></item>
/// <item><description>UpdateX(Func&lt;T, T&gt;) methods for each property</description></item>
/// <item><description>Optionally, action records for each property</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [Store]
/// public partial record CounterState(int Count, string? LastAction);
///
/// // Generated:
/// // public CounterState SetCount(int value) => this with { Count = value };
/// // public CounterState UpdateCount(Func&lt;int, int&gt; updater) => this with { Count = updater(Count) };
/// // public CounterState SetLastAction(string? value) => this with { LastAction = value };
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StoreAttribute : Attribute
{
    /// <summary>
    /// Whether to generate action records for each property setter.
    /// Default is false.
    /// </summary>
    public bool GenerateActions { get; set; }

    /// <summary>
    /// Whether to generate With* methods (aliases for Set*).
    /// Default is false.
    /// </summary>
    public bool GenerateWithMethods { get; set; }
}

/// <summary>
/// Marks a property in a store record to be treated as an immutable collection.
/// The generator will create Add, Remove, and Update methods.
/// </summary>
/// <example>
/// <code>
/// [Store]
/// public partial record TodoState(
///     [property: ImmutableCollection] ImmutableList&lt;Todo&gt; Items
/// );
///
/// // Generated:
/// // public TodoState AddItems(Todo item) => this with { Items = Items.Add(item) };
/// // public TodoState RemoveItems(Todo item) => this with { Items = Items.Remove(item) };
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class ImmutableCollectionAttribute : Attribute
{
}

/// <summary>
/// Marks a property in a store record as computed/derived.
/// The generator will skip generating setters for this property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class ComputedAttribute : Attribute
{
}

/// <summary>
/// Marks a property in a store record as transient.
/// Transient properties are not persisted and are excluded from serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class TransientAttribute : Attribute
{
}
