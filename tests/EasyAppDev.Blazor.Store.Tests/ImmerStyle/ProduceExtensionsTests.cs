// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.ImmerStyle;
using FluentAssertions;
using System.Collections.Immutable;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.ImmerStyle;

public record TestState(int Count, string Name, bool IsActive);

public record NestedState(string Title, TestState Inner);

public record CollectionState(ImmutableList<string> Items);

public class ProduceExtensionsTests
{
    [Fact]
    public void Draft_Set_ShouldUpdateSimpleProperty()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Set(s => s.Count, 20);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(20);
        result.Name.Should().Be("Test");
        result.IsActive.Should().BeFalse();
        state.Count.Should().Be(10); // Original unchanged
    }

    [Fact]
    public void Draft_Set_ShouldUpdateStringProperty()
    {
        // Arrange
        var state = new TestState(10, "Original", false);
        var draft = state.CreateDraft();

        // Act
        draft.Set(s => s.Name, "Updated");
        var result = draft.Produce();

        // Assert
        result.Name.Should().Be("Updated");
        state.Name.Should().Be("Original");
    }

    [Fact]
    public void Draft_Update_ShouldTransformValue()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Update(s => s.Count, c => c * 2);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(20);
    }

    [Fact]
    public void Draft_Increment_ShouldIncreaseValue()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Increment(s => s.Count);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(11);
    }

    [Fact]
    public void Draft_Increment_ShouldIncreaseByAmount()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Increment(s => s.Count, 5);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(15);
    }

    [Fact]
    public void Draft_Decrement_ShouldDecreaseValue()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Decrement(s => s.Count);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(9);
    }

    [Fact]
    public void Draft_Decrement_ShouldDecreaseByAmount()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Decrement(s => s.Count, 3);
        var result = draft.Produce();

        // Assert
        result.Count.Should().Be(7);
    }

    [Fact]
    public void Draft_Toggle_ShouldFlipBooleanValue()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft.Toggle(s => s.IsActive);
        var result = draft.Produce();

        // Assert
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Draft_Toggle_ShouldFlipTrueToFalse()
    {
        // Arrange
        var state = new TestState(10, "Test", true);
        var draft = state.CreateDraft();

        // Act
        draft.Toggle(s => s.IsActive);
        var result = draft.Produce();

        // Assert
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Draft_Chaining_ShouldApplyMultipleChanges()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        draft
            .Set(s => s.Name, "Updated")
            .Increment(s => s.Count, 5)
            .Toggle(s => s.IsActive);

        var result = draft.Produce();

        // Assert
        result.Name.Should().Be("Updated");
        result.Count.Should().Be(15);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Draft_NestedProperty_ShouldUpdateTitle()
    {
        // Arrange
        var state = new NestedState("Parent", new TestState(5, "Child", true));
        var draft = state.CreateDraft();

        // Act - Update top-level property
        draft.Set(s => s.Title, "Updated Parent");
        var result = draft.Produce();

        // Assert
        result.Title.Should().Be("Updated Parent");
        result.Inner.Count.Should().Be(5);
        state.Title.Should().Be("Parent"); // Original unchanged
    }

    [Fact]
    public void Draft_NestedState_ShouldUpdateWithNewInner()
    {
        // Arrange
        var state = new NestedState("Parent", new TestState(5, "Child", true));
        var draft = state.CreateDraft();

        // Act - Update by replacing the entire nested object
        draft.Update(s => s.Inner, inner => inner with { Count = 100 });
        var result = draft.Produce();

        // Assert
        result.Inner.Count.Should().Be(100);
        result.Inner.Name.Should().Be("Child");
        result.Title.Should().Be("Parent");
        state.Inner.Count.Should().Be(5); // Original unchanged
    }

    [Fact]
    public void Draft_Append_ShouldAddToImmutableList()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("item1", "item2"));
        var draft = state.CreateDraft();

        // Act
        draft.Append(s => s.Items, "item3");
        var result = draft.Produce();

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().Contain("item3");
        state.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Draft_Remove_ShouldRemoveFromImmutableList()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "b", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.Remove(s => s.Items, "b");
        var result = draft.Produce();

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain("b");
    }

    [Fact]
    public void Draft_RemoveAll_ShouldRemoveMatchingItems()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("apple", "banana", "apricot", "cherry"));
        var draft = state.CreateDraft();

        // Act
        draft.RemoveAll(s => s.Items, item => item.StartsWith("a"));
        var result = draft.Produce();

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain("banana");
        result.Items.Should().Contain("cherry");
    }

    [Fact]
    public void Draft_Current_ShouldReturnOriginalState()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        var current = draft.Current;

        // Assert
        current.Should().Be(state);
    }

    [Fact]
    public void Draft_MultipleProduce_ShouldReturnSameResult()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();
        draft.Set(s => s.Count, 20);

        // Act
        var result1 = draft.Produce();
        var result2 = draft.Produce();

        // Assert
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public void Draft_NoChanges_ShouldReturnEquivalentState()
    {
        // Arrange
        var state = new TestState(10, "Test", false);
        var draft = state.CreateDraft();

        // Act
        var result = draft.Produce();

        // Assert
        result.Should().BeEquivalentTo(state);
    }

    // --- Dictionary operations tests ---

    [Fact]
    public void Draft_DictSet_ShouldAddNewKey()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty);
        var draft = state.CreateDraft();

        // Act
        draft.DictSet(s => s.Items, "key1", 100);
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainKey("key1");
        result.Items["key1"].Should().Be(100);
        state.Items.Should().BeEmpty();
    }

    [Fact]
    public void Draft_DictSet_ShouldUpdateExistingKey()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty.Add("key1", 50));
        var draft = state.CreateDraft();

        // Act
        draft.DictSet(s => s.Items, "key1", 100);
        var result = draft.Produce();

        // Assert
        result.Items["key1"].Should().Be(100);
    }

    [Fact]
    public void Draft_DictUpdate_ShouldTransformValue()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty.Add("key1", 50));
        var draft = state.CreateDraft();

        // Act
        draft.DictUpdate(s => s.Items, "key1", v => v * 2);
        var result = draft.Produce();

        // Assert
        result.Items["key1"].Should().Be(100);
    }

    [Fact]
    public void Draft_DictRemove_ShouldRemoveKey()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty.Add("key1", 50).Add("key2", 100));
        var draft = state.CreateDraft();

        // Act
        draft.DictRemove(s => s.Items, "key1");
        var result = draft.Produce();

        // Assert
        result.Items.Should().NotContainKey("key1");
        result.Items.Should().ContainKey("key2");
    }

    [Fact]
    public void Draft_DictClear_ShouldRemoveAllKeys()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty.Add("key1", 50).Add("key2", 100));
        var draft = state.CreateDraft();

        // Act
        draft.DictClear(s => s.Items);
        var result = draft.Produce();

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Draft_DictSetRange_ShouldAddMultipleKeys()
    {
        // Arrange
        var state = new DictionaryState(ImmutableDictionary<string, int>.Empty);
        var draft = state.CreateDraft();

        // Act
        draft.DictSetRange(s => s.Items, new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("c", 3)
        });
        var result = draft.Produce();

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items["a"].Should().Be(1);
        result.Items["b"].Should().Be(2);
        result.Items["c"].Should().Be(3);
    }

    // --- List operations tests ---

    [Fact]
    public void Draft_AppendRange_ShouldAddMultipleItems()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a"));
        var draft = state.CreateDraft();

        // Act
        draft.AppendRange(s => s.Items, new[] { "b", "c", "d" });
        var result = draft.Produce();

        // Assert
        result.Items.Should().HaveCount(4);
        result.Items.Should().ContainInOrder("a", "b", "c", "d");
    }

    [Fact]
    public void Draft_Insert_ShouldInsertAtIndex()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.Insert(s => s.Items, 1, "b");
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainInOrder("a", "b", "c");
    }

    [Fact]
    public void Draft_RemoveAt_ShouldRemoveAtIndex()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "b", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.RemoveAt(s => s.Items, 1);
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainInOrder("a", "c");
    }

    [Fact]
    public void Draft_SetAt_ShouldReplaceAtIndex()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "b", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.SetAt(s => s.Items, 1, "X");
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainInOrder("a", "X", "c");
    }

    [Fact]
    public void Draft_UpdateAt_ShouldTransformAtIndex()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "b", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.UpdateAt(s => s.Items, 1, s => s.ToUpper());
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainInOrder("a", "B", "c");
    }

    [Fact]
    public void Draft_UpdateWhere_ShouldTransformMatchingItems()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("apple", "banana", "apricot", "cherry"));
        var draft = state.CreateDraft();

        // Act
        draft.UpdateWhere(s => s.Items, s => s.StartsWith("a"), s => s.ToUpper());
        var result = draft.Produce();

        // Assert
        result.Items.Should().ContainInOrder("APPLE", "banana", "APRICOT", "cherry");
    }

    [Fact]
    public void Draft_Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var state = new CollectionState(ImmutableList.Create("a", "b", "c"));
        var draft = state.CreateDraft();

        // Act
        draft.Clear(s => s.Items);
        var result = draft.Produce();

        // Assert
        result.Items.Should().BeEmpty();
    }

    // --- String operations tests ---

    [Fact]
    public void Draft_Concat_ShouldAppendText()
    {
        // Arrange
        var state = new TestState(10, "Hello", false);
        var draft = state.CreateDraft();

        // Act
        draft.Concat(s => s.Name, " World");
        var result = draft.Produce();

        // Assert
        result.Name.Should().Be("Hello World");
    }

    [Fact]
    public void Draft_Replace_ShouldReplaceText()
    {
        // Arrange
        var state = new TestState(10, "Hello World", false);
        var draft = state.CreateDraft();

        // Act
        draft.Replace(s => s.Name, "World", "Universe");
        var result = draft.Produce();

        // Assert
        result.Name.Should().Be("Hello Universe");
    }

    [Fact]
    public void Draft_Trim_ShouldTrimWhitespace()
    {
        // Arrange
        var state = new TestState(10, "  Hello  ", false);
        var draft = state.CreateDraft();

        // Act
        draft.Trim(s => s.Name);
        var result = draft.Produce();

        // Assert
        result.Name.Should().Be("Hello");
    }

    // --- Nested update tests ---

    [Fact]
    public void Draft_UpdateNested_ShouldModifyNestedObject()
    {
        // Arrange
        var state = new NestedState("Parent", new TestState(5, "Child", true));
        var draft = state.CreateDraft();

        // Act
        draft.UpdateNested(s => s.Inner, inner => inner
            .Set(i => i.Count, 100)
            .Set(i => i.Name, "Updated"));
        var result = draft.Produce();

        // Assert
        result.Inner.Count.Should().Be(100);
        result.Inner.Name.Should().Be("Updated");
        result.Inner.IsActive.Should().BeTrue(); // Unchanged
        state.Inner.Count.Should().Be(5); // Original unchanged
    }

    // --- Numeric overloads tests ---

    [Fact]
    public void Draft_IncrementLong_ShouldIncreaseLongValue()
    {
        // Arrange
        var state = new NumericState(10, 100L, 1.5);
        var draft = state.CreateDraft();

        // Act
        draft.Increment(s => s.LongValue, 50);
        var result = draft.Produce();

        // Assert
        result.LongValue.Should().Be(150L);
    }

    [Fact]
    public void Draft_IncrementDouble_ShouldIncreaseDoubleValue()
    {
        // Arrange
        var state = new NumericState(10, 100L, 1.5);
        var draft = state.CreateDraft();

        // Act
        draft.Increment(s => s.DoubleValue, 0.5);
        var result = draft.Produce();

        // Assert
        result.DoubleValue.Should().Be(2.0);
    }

    [Fact]
    public void Draft_DecrementLong_ShouldDecreaseLongValue()
    {
        // Arrange
        var state = new NumericState(10, 100L, 1.5);
        var draft = state.CreateDraft();

        // Act
        draft.Decrement(s => s.LongValue, 30);
        var result = draft.Produce();

        // Assert
        result.LongValue.Should().Be(70L);
    }

    // --- Static Produce extension test ---

    [Fact]
    public void Produce_ShouldApplyRecipeToState()
    {
        // Arrange
        var state = new TestState(10, "Test", false);

        // Act
        var result = state.Produce(draft => draft
            .Set(s => s.Count, 20)
            .Toggle(s => s.IsActive));

        // Assert
        result.Count.Should().Be(20);
        result.IsActive.Should().BeTrue();
        state.Count.Should().Be(10); // Original unchanged
    }
}

public record DictionaryState(ImmutableDictionary<string, int> Items);

public record NumericState(int IntValue, long LongValue, double DoubleValue);
