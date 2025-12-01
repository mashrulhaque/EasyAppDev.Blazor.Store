using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.History;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.History;

public record DocumentState(string Content, int CursorPosition);

public class StoreHistoryTests : IDisposable
{
    private readonly IStore<DocumentState> _store;
    private readonly StoreHistory<DocumentState> _history;

    public StoreHistoryTests()
    {
        _history = new StoreHistory<DocumentState>(new HistoryOptions().WithMaxSize(10));

        _store = StoreBuilder<DocumentState>.Create(new DocumentState("", 0))
            .WithMiddleware(_history)
            .Build();

        // Initialize history with store
        _history.Initialize(_store);
    }

    [Fact]
    public void InitialState_HasOneEntry()
    {
        // Assert
        _history.Count.Should().Be(1);
        _history.CurrentIndex.Should().Be(0);
        _history.CanUndo.Should().BeFalse();
        _history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_RestoresPreviousState()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "Hello" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "Hello World" }, "TYPE");

        _store.GetState().Content.Should().Be("Hello World");
        _history.Count.Should().Be(3);

        // Act
        await _history.UndoAsync();

        // Assert
        _store.GetState().Content.Should().Be("Hello");
        _history.CurrentIndex.Should().Be(1);
        _history.CanUndo.Should().BeTrue();
        _history.CanRedo.Should().BeTrue();
    }

    [Fact]
    public async Task Redo_RestoresNextState()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "Hello" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "Hello World" }, "TYPE");
        await _history.UndoAsync();

        // Act
        await _history.RedoAsync();

        // Assert
        _store.GetState().Content.Should().Be("Hello World");
        _history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_WhenAtStart_DoesNothing()
    {
        // Arrange
        var initialState = _store.GetState();

        // Act
        await _history.UndoAsync();

        // Assert
        _store.GetState().Should().Be(initialState);
        _history.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public async Task Redo_WhenAtEnd_DoesNothing()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "Hello" }, "TYPE");
        var currentState = _store.GetState();

        // Act
        await _history.RedoAsync();

        // Assert
        _store.GetState().Should().Be(currentState);
    }

    [Fact]
    public async Task NewUpdate_TruncatesForwardHistory()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "B" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "C" }, "TYPE");
        await _history.UndoAsync();
        await _history.UndoAsync();
        // Now at "A", with "B" and "C" in forward history

        // Act
        await _store.UpdateAsync(s => s with { Content = "D" }, "TYPE");

        // Assert
        _history.CanRedo.Should().BeFalse();
        _history.Count.Should().Be(3); // Initial, A, D
        _store.GetState().Content.Should().Be("D");
    }

    [Fact]
    public async Task MaxSize_EnforcesLimit()
    {
        // Arrange & Act
        for (int i = 1; i <= 15; i++)
        {
            await _store.UpdateAsync(s => s with { Content = $"Content {i}" }, "TYPE");
        }

        // Assert
        _history.Count.Should().Be(10); // Limited to MaxSize
        _history.Entries[0].State.Content.Should().NotBe(""); // Oldest entries removed
    }

    [Fact]
    public async Task GoToAsync_JumpsToSpecificEntry()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "B" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "C" }, "TYPE");

        // Act
        await _history.GoToAsync(1);

        // Assert
        _store.GetState().Content.Should().Be("A");
        _history.CurrentIndex.Should().Be(1);
    }

    [Fact]
    public async Task GoToAsync_WithInvalidIndex_Throws()
    {
        // Act
        Func<Task> act = async () => await _history.GoToAsync(100);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Clear_ResetsToCurrentState()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "B" }, "TYPE");

        // Act
        _history.Clear();

        // Assert
        _history.Count.Should().Be(1);
        _history.CurrentIndex.Should().Be(0);
        _history.CanUndo.Should().BeFalse();
        _history.CanRedo.Should().BeFalse();
        _store.GetState().Content.Should().Be("B"); // Current state preserved
    }

    [Fact]
    public async Task Entries_ReturnsAllHistoryEntries()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "ACTION_A");
        await _store.UpdateAsync(s => s with { Content = "B" }, "ACTION_B");

        // Act
        var entries = _history.Entries;

        // Assert
        entries.Should().HaveCount(3);
        entries[0].State.Content.Should().Be("");
        entries[1].State.Content.Should().Be("A");
        entries[1].Action.Should().Be("ACTION_A");
        entries[2].State.Content.Should().Be("B");
        entries[2].Action.Should().Be("ACTION_B");
    }

    [Fact]
    public async Task OnHistoryChanged_FiresOnStateChange()
    {
        // Arrange
        var eventCount = 0;
        _history.OnHistoryChanged += () => eventCount++;

        // Act
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await _history.UndoAsync();
        await _history.RedoAsync();

        // Assert
        eventCount.Should().Be(3); // Add, Undo, Redo
    }

    [Fact]
    public async Task ExcludedActions_AreNotTracked()
    {
        // Arrange
        var historyWithExclusions = new StoreHistory<DocumentState>(
            new HistoryOptions().ExcludeActions("CURSOR"));

        var store = StoreBuilder<DocumentState>.Create(new DocumentState("", 0))
            .WithMiddleware(historyWithExclusions)
            .Build();

        historyWithExclusions.Initialize(store);

        // Act
        await store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await store.UpdateAsync(s => s with { CursorPosition = 5 }, "CURSOR_MOVE");
        await store.UpdateAsync(s => s with { Content = "AB" }, "TYPE");

        // Assert
        historyWithExclusions.Count.Should().Be(3); // Initial, TYPE, TYPE (CURSOR excluded)

        store.Dispose();
    }

    [Fact]
    public async Task GroupWindow_CombinesRapidChanges()
    {
        // Arrange
        var historyWithGrouping = new StoreHistory<DocumentState>(
            new HistoryOptions().GroupActions(TimeSpan.FromMilliseconds(200)));

        var store = StoreBuilder<DocumentState>.Create(new DocumentState("", 0))
            .WithMiddleware(historyWithGrouping)
            .Build();

        historyWithGrouping.Initialize(store);

        // Act - rapid updates within grouping window
        await store.UpdateAsync(s => s with { Content = "H" }, "TYPE");
        await store.UpdateAsync(s => s with { Content = "He" }, "TYPE");
        await store.UpdateAsync(s => s with { Content = "Hel" }, "TYPE");

        // Wait for grouping window to pass
        await Task.Delay(300);

        await store.UpdateAsync(s => s with { Content = "Hello" }, "TYPE");

        // Assert - rapid changes should be grouped
        // Initial + grouped rapid changes + final after delay
        historyWithGrouping.Count.Should().BeGreaterThanOrEqualTo(2);
        historyWithGrouping.Count.Should().BeLessThanOrEqualTo(3);

        store.Dispose();
    }

    [Fact]
    public async Task TrackWhen_FiltersBasedOnPredicate()
    {
        // Arrange
        var historyWithPredicate = new StoreHistory<DocumentState>(
            new HistoryOptions().TrackWhen(action => action?.StartsWith("IMPORTANT") ?? false));

        var store = StoreBuilder<DocumentState>.Create(new DocumentState("", 0))
            .WithMiddleware(historyWithPredicate)
            .Build();

        historyWithPredicate.Initialize(store);

        // Act
        await store.UpdateAsync(s => s with { Content = "A" }, "MINOR_CHANGE");
        await store.UpdateAsync(s => s with { Content = "B" }, "IMPORTANT_SAVE");
        await store.UpdateAsync(s => s with { Content = "C" }, "MINOR_UPDATE");

        // Assert
        historyWithPredicate.Count.Should().Be(2); // Initial + IMPORTANT_SAVE

        store.Dispose();
    }

    [Fact]
    public async Task UndoRedo_DoNotAddToHistory()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        await _store.UpdateAsync(s => s with { Content = "B" }, "TYPE");
        var countBeforeUndo = _history.Count;

        // Act
        await _history.UndoAsync();
        await _history.RedoAsync();

        // Assert
        _history.Count.Should().Be(countBeforeUndo); // No new entries from undo/redo
    }

    [Fact]
    public async Task SameState_DoesNotCreateEntry()
    {
        // Arrange
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");
        var countAfterFirst = _history.Count;

        // Act - update to same state
        await _store.UpdateAsync(s => s with { Content = "A" }, "TYPE");

        // Assert
        _history.Count.Should().Be(countAfterFirst); // No new entry for same state
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
