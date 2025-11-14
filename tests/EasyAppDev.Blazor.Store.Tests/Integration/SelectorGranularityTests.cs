using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Integration;

/// <summary>
/// Tests demonstrating the granular update capabilities with selectors.
/// </summary>
public class SelectorGranularityTests
{
    private record ComplexState(
        int Counter,
        string Message,
        List<string> Items,
        bool IsLoading,
        DateTime LastUpdated);

    [Fact]
    public async Task Selector_OnlyNotifiesWhenSelectedPropertyChanges()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 0,
            Message: "Hello",
            Items: new List<string> { "A", "B" },
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var counterNotifications = 0;
        var messageNotifications = 0;

        // Subscribe to Counter only
        using var counterSub = store.Subscribe(
            state => state.Counter,
            _ => counterNotifications++);

        // Subscribe to Message only
        using var messageSub = store.Subscribe(
            state => state.Message,
            _ => messageNotifications++);

        // Act 1: Change Counter
        await store.UpdateAsync(s => s with { Counter = 1 });

        // Assert 1
        counterNotifications.Should().Be(1);
        messageNotifications.Should().Be(0); // Should NOT fire

        // Act 2: Change Message
        await store.UpdateAsync(s => s with { Message = "World" });

        // Assert 2
        counterNotifications.Should().Be(1); // Should NOT fire
        messageNotifications.Should().Be(1);

        // Act 3: Change Items (neither subscription cares)
        await store.UpdateAsync(s => s with { Items = new List<string> { "C", "D" } });

        // Assert 3 - Neither should fire
        counterNotifications.Should().Be(1);
        messageNotifications.Should().Be(1);

        // Act 4: Change both Counter and Message
        await store.UpdateAsync(s => s with { Counter = 2, Message = "Blazor" });

        // Assert 4 - Both should fire
        counterNotifications.Should().Be(2);
        messageNotifications.Should().Be(2);
    }

    [Fact]
    public async Task Selector_WithMultipleProperties_OnlyNotifiesWhenAnySelectedPropertyChanges()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 0,
            Message: "Hello",
            Items: new List<string>(),
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var notifications = 0;

        // Subscribe to multiple properties
        using var sub = store.Subscribe(
            state => new { state.Counter, state.Message },
            _ => notifications++);

        // Act 1: Change Counter (selected)
        await store.UpdateAsync(s => s with { Counter = 1 });
        notifications.Should().Be(1);

        // Act 2: Change Message (selected)
        await store.UpdateAsync(s => s with { Message = "World" });
        notifications.Should().Be(2);

        // Act 3: Change IsLoading (NOT selected)
        await store.UpdateAsync(s => s with { IsLoading = true });
        notifications.Should().Be(2); // Should NOT increment

        // Act 4: Change LastUpdated (NOT selected)
        await store.UpdateAsync(s => s with { LastUpdated = DateTime.Now.AddHours(1) });
        notifications.Should().Be(2); // Should NOT increment
    }

    [Fact]
    public async Task Selector_WithComputedValue_OnlyNotifiesWhenComputedValueChanges()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 2, // Even
            Message: "Test",
            Items: new List<string>(),
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var notifications = 0;
        var lastValue = false;

        // Subscribe to computed value (is counter even?)
        using var sub = store.Subscribe(
            state => state.Counter % 2 == 0,
            isEven =>
            {
                notifications++;
                lastValue = isEven;
            });

        // Act 1: Change counter from 2 to 4 (both even)
        await store.UpdateAsync(s => s with { Counter = 4 });

        // Assert 1: Should NOT notify (computed value didn't change)
        notifications.Should().Be(0);

        // Act 2: Change counter from 4 to 5 (even to odd)
        await store.UpdateAsync(s => s with { Counter = 5 });

        // Assert 2: Should notify (computed value changed)
        notifications.Should().Be(1);
        lastValue.Should().BeFalse();

        // Act 3: Change counter from 5 to 7 (both odd)
        await store.UpdateAsync(s => s with { Counter = 7 });

        // Assert 3: Should NOT notify (computed value didn't change)
        notifications.Should().Be(1);

        // Act 4: Change counter from 7 to 8 (odd to even)
        await store.UpdateAsync(s => s with { Counter = 8 });

        // Assert 4: Should notify (computed value changed)
        notifications.Should().Be(2);
        lastValue.Should().BeTrue();
    }

    [Fact]
    public async Task Selector_WithListCount_OnlyNotifiesWhenCountChanges()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 0,
            Message: "Test",
            Items: new List<string> { "A", "B" },
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var notifications = 0;

        // Subscribe to list count only
        using var sub = store.Subscribe(
            state => state.Items.Count,
            _ => notifications++);

        // Act 1: Change list items but keep same count
        await store.UpdateAsync(s => s with { Items = new List<string> { "C", "D" } });

        // Assert 1: Should NOT notify (count didn't change)
        notifications.Should().Be(0);

        // Act 2: Add an item (count changes)
        await store.UpdateAsync(s => s with { Items = new List<string> { "C", "D", "E" } });

        // Assert 2: Should notify (count changed from 2 to 3)
        notifications.Should().Be(1);

        // Act 3: Remove all items (count changes)
        await store.UpdateAsync(s => s with { Items = new List<string>() });

        // Assert 3: Should notify (count changed from 3 to 0)
        notifications.Should().Be(2);
    }

    [Fact]
    public async Task Selector_WithCustomComparer_UsesComparerForEquality()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 0,
            Message: "HELLO",
            Items: new List<string>(),
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var notifications = 0;

        // Subscribe with case-insensitive comparer
        using var sub = store.Subscribe(
            state => state.Message,
            _ => notifications++,
            StringComparer.OrdinalIgnoreCase);

        // Act 1: Change to same string with different case
        await store.UpdateAsync(s => s with { Message = "hello" });

        // Assert 1: Should NOT notify (equal with case-insensitive comparer)
        notifications.Should().Be(0);

        // Act 2: Change to different string
        await store.UpdateAsync(s => s with { Message = "WORLD" });

        // Assert 2: Should notify (not equal even with case-insensitive)
        notifications.Should().Be(1);

        // Act 3: Change to same string with different case
        await store.UpdateAsync(s => s with { Message = "world" });

        // Assert 3: Should NOT notify (equal with case-insensitive comparer)
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task MultipleSubscribers_OnlySelectedOnesGetNotified()
    {
        // Arrange
        var initialState = new ComplexState(
            Counter: 0,
            Message: "Test",
            Items: new List<string>(),
            IsLoading: false,
            LastUpdated: DateTime.Now);

        var store = StoreBuilder<ComplexState>
            .Create(initialState)
            .Build();

        var sub1Notifications = 0;
        var sub2Notifications = 0;
        var sub3Notifications = 0;

        using var sub1 = store.Subscribe(state => state.Counter, _ => sub1Notifications++);
        using var sub2 = store.Subscribe(state => state.Message, _ => sub2Notifications++);
        using var sub3 = store.Subscribe(state => state.IsLoading, _ => sub3Notifications++);

        // Act: Update only Counter
        await store.UpdateAsync(s => s with { Counter = 1 });

        // Assert: Only sub1 should fire
        sub1Notifications.Should().Be(1);
        sub2Notifications.Should().Be(0);
        sub3Notifications.Should().Be(0);

        // Act: Update only Message
        await store.UpdateAsync(s => s with { Message = "Changed" });

        // Assert: Only sub2 should fire
        sub1Notifications.Should().Be(1);
        sub2Notifications.Should().Be(1);
        sub3Notifications.Should().Be(0);

        // Act: Update only IsLoading
        await store.UpdateAsync(s => s with { IsLoading = true });

        // Assert: Only sub3 should fire
        sub1Notifications.Should().Be(1);
        sub2Notifications.Should().Be(1);
        sub3Notifications.Should().Be(1);

        // Act: Update all three
        await store.UpdateAsync(s => s with { Counter = 2, Message = "All", IsLoading = false });

        // Assert: All should fire
        sub1Notifications.Should().Be(2);
        sub2Notifications.Should().Be(2);
        sub3Notifications.Should().Be(2);
    }
}
