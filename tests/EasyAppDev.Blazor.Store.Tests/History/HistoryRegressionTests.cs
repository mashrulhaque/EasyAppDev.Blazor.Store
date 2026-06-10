using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.History;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.History;

public record NoteState(string Content);

/// <summary>
/// Regression tests for StoreHistory bugs: missing initialization via WithHistory,
/// shared (static) undo/redo flag, and history corruption when grouping after Undo.
/// </summary>
public class HistoryRegressionTests
{
    [Fact]
    public async Task WithHistory_InitializesHistory_WithoutExplicitInitialize()
    {
        // Arrange - WithHistory must wire up the history via the store-aware middleware
        // mechanism; no explicit Initialize call is performed here.
        var builder = StoreBuilder<NoteState>.Create(new NoteState(""))
            .WithHistory(out var history);
        var store = builder.Build();

        // Act
        await store.UpdateAsync(s => s with { Content = "Hello" }, "TYPE");

        // Assert - history tracked the update and undo works
        history.Count.Should().Be(2);
        history.CanUndo.Should().BeTrue();

        await history.UndoAsync();
        store.GetState().Content.Should().Be("");
    }

    [Fact]
    public async Task GroupWindow_NeverGroupsIntoInitialEntry()
    {
        // Arrange - a huge group window so the first update lands within it
        IStoreHistory<NoteState> history;
        var store = StoreBuilder<NoteState>.Create(new NoteState(""))
            .WithHistory(o => o.GroupActions(TimeSpan.FromHours(1)), out history)
            .Build();

        // Act - the very first update occurs within the group window of INITIAL
        await store.UpdateAsync(s => s with { Content = "A" }, "TYPE");

        // Assert - the INITIAL entry must never be overwritten by grouping
        history.Count.Should().Be(2);
        history.Entries[0].Action.Should().Be("INITIAL");
        history.CanUndo.Should().BeTrue();

        await history.UndoAsync();
        store.GetState().Content.Should().Be("");
    }

    [Fact]
    public async Task GroupWindow_AfterUndo_DoesNotCorruptHistory()
    {
        // Arrange
        IStoreHistory<NoteState> history;
        var store = StoreBuilder<NoteState>.Create(new NoteState(""))
            .WithHistory(o => o.GroupActions(TimeSpan.FromHours(1)), out history)
            .Build();

        await store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await store.UpdateAsync(s => s with { Content = "B" }, "TYPE"); // groups into "A" entry
        history.Count.Should().Be(2); // INITIAL, B

        await history.UndoAsync(); // back to INITIAL (index 0)
        store.GetState().Content.Should().Be("");

        // Act - a new update while NOT at the end of history must truncate and append,
        // never replace the current (INITIAL) entry.
        await store.UpdateAsync(s => s with { Content = "C" }, "TYPE");

        // Assert
        history.Entries[0].Action.Should().Be("INITIAL");
        history.Count.Should().Be(2); // INITIAL, C
        history.CurrentIndex.Should().Be(1);
        history.CanRedo.Should().BeFalse();

        await history.UndoAsync();
        store.GetState().Content.Should().Be("");
    }

    [Fact]
    public async Task UndoRedoFlag_IsPerInstance_NotSharedAcrossStores()
    {
        // Arrange - two independent stores with their own histories for the SAME TState.
        // With a static AsyncLocal flag, an undo on store1 would suppress history tracking
        // for an update on store2 performed in the same execution context.
        var store1 = StoreBuilder<NoteState>.Create(new NoteState(""))
            .WithHistory(out var history1)
            .Build();
        var store2 = StoreBuilder<NoteState>.Create(new NoteState(""))
            .WithHistory(out var history2)
            .Build();

        await store1.UpdateAsync(s => s with { Content = "A" }, "TYPE");

        // During store1's UNDO notification, synchronously update store2 (the undo flag
        // of history1 is still set in this execution context at that point).
        store1.Subscribe(s =>
        {
            if (s.Content == "")
            {
                store2.UpdateAsync(x => x with { Content = "FromUndo" }, "TYPE")
                    .GetAwaiter().GetResult();
            }
        });

        // Act
        await history1.UndoAsync();

        // Assert - store2's history must have tracked its own update
        store2.GetState().Content.Should().Be("FromUndo");
        history2.Count.Should().Be(2);
        history2.CanUndo.Should().BeTrue();
    }
}
