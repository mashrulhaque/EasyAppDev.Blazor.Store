using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Selectors;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Selectors;

public record SelectorTestState(int Count, string Name, decimal Price);

public class SelectorSubscriptionTests
{
    private static ISelector<SelectorTestState, TResult> CreateSelector<TResult>(
        Func<SelectorTestState, TResult> selector)
    {
        return EasyAppDev.Blazor.Store.Selectors.Selectors.Create(selector);
    }

    [Fact]
    public async Task Subscribe_WithISelector_ReceivesSelectedValue()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(0, "Initial", 0m))
            .Build();

        var countSelector = CreateSelector(s => s.Count);
        var receivedValues = new List<int>();

        // Act
        var subscription = store.Subscribe(countSelector, value => receivedValues.Add(value));
        await store.UpdateAsync(s => s with { Count = 1 });
        await store.UpdateAsync(s => s with { Count = 2 });
        await store.UpdateAsync(s => s with { Name = "Changed" }); // Should not trigger (Count unchanged)

        // Assert
        receivedValues.Should().ContainInOrder(1, 2);
        receivedValues.Should().HaveCount(2);

        subscription.Dispose();
        store.Dispose();
    }

    [Fact]
    public async Task Subscribe_WithISelector_OnlyNotifiesOnChange()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(0, "Initial", 100m))
            .Build();

        var priceSelector = CreateSelector(s => s.Price);
        var callCount = 0;

        // Act
        var subscription = store.Subscribe(priceSelector, _ => callCount++);
        await store.UpdateAsync(s => s with { Price = 100m }); // Same value, no notification
        await store.UpdateAsync(s => s with { Count = 5 }); // Different property, no notification
        await store.UpdateAsync(s => s with { Price = 200m }); // Changed, should notify

        // Assert
        callCount.Should().Be(1);

        subscription.Dispose();
        store.Dispose();
    }

    [Fact]
    public async Task Subscribe_WithComposedSelector_WorksCorrectly()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(5, "Test", 10m))
            .Build();

        var countSelector = CreateSelector(s => s.Count);
        var priceSelector = CreateSelector(s => s.Price);
        var composedSelector = EasyAppDev.Blazor.Store.Selectors.Selectors.Create(
            countSelector,
            priceSelector,
            (count, price) => count * price);

        var receivedValues = new List<decimal>();

        // Act
        var subscription = store.Subscribe(composedSelector, value => receivedValues.Add(value));
        await store.UpdateAsync(s => s with { Count = 10 }); // 10 * 10 = 100

        // Assert
        receivedValues.Should().Contain(100m);

        subscription.Dispose();
        store.Dispose();
    }

    [Fact]
    public void Subscribe_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(0, "", 0m))
            .Build();

        ISelector<SelectorTestState, int>? nullSelector = null;

        // Act & Assert
        var act = () => store.Subscribe(nullSelector!, _ => { });
        act.Should().Throw<ArgumentNullException>();

        store.Dispose();
    }

    [Fact]
    public void Subscribe_WithNullCallback_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(0, "", 0m))
            .Build();

        var selector = CreateSelector(s => s.Count);

        // Act & Assert
        var act = () => store.Subscribe(selector, null!);
        act.Should().Throw<ArgumentNullException>();

        store.Dispose();
    }

    [Fact]
    public async Task Subscribe_WithISelector_DisposedSubscription_DoesNotReceiveUpdates()
    {
        // Arrange
        var store = StoreBuilder<SelectorTestState>
            .Create(new SelectorTestState(0, "", 0m))
            .Build();

        var selector = CreateSelector(s => s.Count);
        var callCount = 0;

        // Act
        var subscription = store.Subscribe(selector, _ => callCount++);
        await store.UpdateAsync(s => s with { Count = 1 }); // Should notify
        subscription.Dispose();
        await store.UpdateAsync(s => s with { Count = 2 }); // Should not notify

        // Assert
        callCount.Should().Be(1);

        store.Dispose();
    }
}
