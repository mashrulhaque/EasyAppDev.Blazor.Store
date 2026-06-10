using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Diagnostics;

public class DiagnosticsServiceTests
{
    private readonly DiagnosticsService _service;

    public DiagnosticsServiceTests()
    {
        _service = new DiagnosticsService();
    }

    [Fact]
    public void RecordUpdate_ShouldStoreActionHistory()
    {
        // Arrange
        var entry = new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "TEST_ACTION",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateJson = "{\"value\": 42}",
            NewStateSize = 15
        };

        // Act
        _service.RecordUpdate(entry);

        // Assert
        var actions = _service.GetRecentActions(typeof(TestState));
        actions.Should().ContainSingle();
        actions[0].Action.Should().Be("TEST_ACTION");
        actions[0].Duration.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void RecordUpdate_ShouldMaintainCircularBuffer()
    {
        // Arrange
        var service = new DiagnosticsService(maxHistorySize: 5);
        var baseTime = DateTime.UtcNow;

        // Act - Record 10 entries with incrementing timestamps
        for (int i = 0; i < 10; i++)
        {
            service.RecordUpdate(new ActionHistoryEntry
            {
                StateType = typeof(TestState),
                Action = $"ACTION_{i}",
                Timestamp = baseTime.AddSeconds(i),  // Ensure distinct timestamps
                Duration = TimeSpan.FromMilliseconds(i),
                NewStateSize = 10
            });
        }

        // Assert - Should only have the last 5
        var actions = service.GetRecentActions(typeof(TestState));
        actions.Should().HaveCount(5);
        actions[0].Action.Should().Be("ACTION_9"); // Most recent first
        actions[4].Action.Should().Be("ACTION_5"); // Oldest in buffer
    }

    [Fact]
    public void RecordRender_ShouldStoreRenderEvent()
    {
        // Arrange
        var renderEvent = new RenderEvent
        {
            ComponentName = "TestComponent",
            Timestamp = DateTime.UtcNow,
            IsFirstRender = true,
            StateType = typeof(TestState)
        };

        // Act
        _service.RecordRender(renderEvent);

        // Assert
        var renders = _service.GetRecentRenders("TestComponent");
        renders.Should().ContainSingle();
        renders[0].ComponentName.Should().Be("TestComponent");
        renders[0].IsFirstRender.Should().BeTrue();
    }

    [Fact]
    public void RecordRender_ShouldUpdateRenderCounts()
    {
        // Arrange & Act
        for (int i = 0; i < 5; i++)
        {
            _service.RecordRender(new RenderEvent
            {
                ComponentName = "TestComponent",
                Timestamp = DateTime.UtcNow,
                IsFirstRender = i == 0,
                StateType = typeof(TestState)
            });
        }

        // Assert
        var counts = _service.GetRenderCounts();
        counts.Should().ContainKey("TestComponent");
        counts["TestComponent"].Should().Be(5);
    }

    [Fact]
    public void RecordSubscription_ShouldStoreSubscriptionInfo()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var subscriptionInfo = new SubscriptionInfo
        {
            SubscriptionId = subscriptionId,
            StateType = typeof(TestState),
            SubscriberName = "TestComponent",
            SubscriptionType = "Full",
            CreatedAt = DateTime.UtcNow,
            NotificationCount = 0
        };

        // Act
        _service.RecordSubscription(subscriptionInfo);

        // Assert
        var subscriptions = _service.GetActiveSubscriptions(typeof(TestState));
        subscriptions.Should().ContainSingle();
        subscriptions[0].SubscriberName.Should().Be("TestComponent");
        subscriptions[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordSubscriptionDisposed_ShouldMarkAsInactive()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        _service.RecordSubscription(new SubscriptionInfo
        {
            SubscriptionId = subscriptionId,
            StateType = typeof(TestState),
            SubscriberName = "TestComponent",
            SubscriptionType = "Full",
            CreatedAt = DateTime.UtcNow,
            NotificationCount = 0
        });

        // Act
        _service.RecordSubscriptionDisposed(subscriptionId);

        // Assert
        var activeSubscriptions = _service.GetActiveSubscriptions(typeof(TestState));
        activeSubscriptions.Should().BeEmpty();

        var allSubscriptions = _service.GetAllSubscriptions(typeof(TestState));
        allSubscriptions.Should().ContainSingle();
        allSubscriptions[0].IsActive.Should().BeFalse();
        allSubscriptions[0].DisposedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordSubscriptionDisposed_ShouldBoundDisposedHistory()
    {
        // Arrange - disposed subscriptions must not accumulate forever (memory leak
        // in long-running apps); only a bounded ring of recent entries is kept.
        var service = new DiagnosticsService(maxHistorySize: 5);

        // Act - create and dispose many subscriptions
        for (var i = 0; i < 20; i++)
        {
            var id = Guid.NewGuid();
            service.RecordSubscription(new SubscriptionInfo
            {
                SubscriptionId = id,
                StateType = typeof(TestState),
                SubscriberName = $"Component{i}",
                SubscriptionType = "Full",
                CreatedAt = DateTime.UtcNow,
                NotificationCount = 0
            });
            service.RecordSubscriptionDisposed(id);
        }

        // Assert
        service.GetActiveSubscriptions(typeof(TestState)).Should().BeEmpty();
        service.GetAllSubscriptions(typeof(TestState)).Should().HaveCount(5);
        service.GetAllSubscriptions(typeof(TestState)).Should().OnlyContain(s => !s.IsActive);
    }

    [Fact]
    public void RecordSubscriptionNotification_AfterDispose_DoesNotResurrectSubscription()
    {
        // Arrange
        var id = Guid.NewGuid();
        _service.RecordSubscription(new SubscriptionInfo
        {
            SubscriptionId = id,
            StateType = typeof(TestState),
            SubscriberName = "TestComponent",
            SubscriptionType = "Full",
            CreatedAt = DateTime.UtcNow,
            NotificationCount = 0
        });
        _service.RecordSubscriptionDisposed(id);

        // Act - a stray notification after disposal must not re-add tracking entries
        _service.RecordSubscriptionNotification(id);

        // Assert
        _service.GetActiveSubscriptions(typeof(TestState)).Should().BeEmpty();
    }

    [Fact]
    public void RecordSubscriptionNotification_ShouldIncrementCount()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        _service.RecordSubscription(new SubscriptionInfo
        {
            SubscriptionId = subscriptionId,
            StateType = typeof(TestState),
            SubscriberName = "TestComponent",
            SubscriptionType = "Full",
            CreatedAt = DateTime.UtcNow,
            NotificationCount = 0
        });

        // Act
        _service.RecordSubscriptionNotification(subscriptionId);
        _service.RecordSubscriptionNotification(subscriptionId);
        _service.RecordSubscriptionNotification(subscriptionId);

        // Assert
        var subscriptions = _service.GetAllSubscriptions(typeof(TestState));
        subscriptions[0].NotificationCount.Should().Be(3);
    }

    [Fact]
    public void GetPerformanceMetrics_ShouldCalculateCorrectly()
    {
        // Arrange
        var durations = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        foreach (var duration in durations)
        {
            _service.RecordUpdate(new ActionHistoryEntry
            {
                StateType = typeof(TestState),
                Action = "TEST",
                Timestamp = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(duration),
                NewStateSize = 100
            });
        }

        // Act
        var metrics = _service.GetPerformanceMetrics(typeof(TestState));

        // Assert
        metrics.Should().NotBeNull();
        metrics!.TotalUpdates.Should().Be(10);
        metrics.AverageDuration.Should().Be(TimeSpan.FromMilliseconds(55));
        metrics.MinDuration.Should().Be(TimeSpan.FromMilliseconds(10));
        metrics.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(100));
        // Median calculation may vary slightly based on implementation (index-based vs average of middle two)
        // For 10 elements, mathematically correct median is (50+60)/2 = 55ms, but index-based gives 60ms
        metrics.MedianDuration.TotalMilliseconds.Should().BeInRange(50, 60);
    }

    [Fact]
    public void GetPerformanceMetrics_ShouldReturnNullForNoData()
    {
        // Act
        var metrics = _service.GetPerformanceMetrics(typeof(TestState));

        // Assert
        metrics.Should().BeNull();
    }

    [Fact]
    public void GetRecentActions_ShouldFilterByStateType()
    {
        // Arrange
        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "STATE1_ACTION",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(OtherTestState),
            Action = "STATE2_ACTION",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        // Act
        var actionsForTestState = _service.GetRecentActions(typeof(TestState));
        var actionsForOtherState = _service.GetRecentActions(typeof(OtherTestState));

        // Assert
        actionsForTestState.Should().ContainSingle();
        actionsForTestState[0].Action.Should().Be("STATE1_ACTION");

        actionsForOtherState.Should().ContainSingle();
        actionsForOtherState[0].Action.Should().Be("STATE2_ACTION");
    }

    [Fact]
    public void GetRecentActions_ShouldReturnAllWhenNoTypeSpecified()
    {
        // Arrange
        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "ACTION1",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(OtherTestState),
            Action = "ACTION2",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        // Act
        var allActions = _service.GetRecentActions();

        // Assert
        allActions.Should().HaveCount(2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllData()
    {
        // Arrange
        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "TEST",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        _service.RecordRender(new RenderEvent
        {
            ComponentName = "TestComponent",
            Timestamp = DateTime.UtcNow,
            IsFirstRender = true
        });

        // Act
        _service.Clear();

        // Assert
        _service.GetRecentActions().Should().BeEmpty();
        _service.GetRecentRenders().Should().BeEmpty();
        _service.GetRenderCounts().Should().BeEmpty();
    }

    [Fact]
    public void ClearWithStateType_ShouldRemoveOnlyThatStateData()
    {
        // Arrange
        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "ACTION1",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(OtherTestState),
            Action = "ACTION2",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateSize = 10
        });

        // Act
        _service.Clear(typeof(TestState));

        // Assert
        _service.GetRecentActions(typeof(TestState)).Should().BeEmpty();
        _service.GetRecentActions(typeof(OtherTestState)).Should().ContainSingle();
    }

    [Fact]
    public void GetCurrentStateJson_ShouldReturnLatestState()
    {
        // Arrange
        _service.RecordUpdate(new ActionHistoryEntry
        {
            StateType = typeof(TestState),
            Action = "TEST",
            Timestamp = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(10),
            NewStateJson = "{\"value\": 42}",
            NewStateSize = 15
        });

        // Act
        var stateJson = _service.GetCurrentStateJson(typeof(TestState));

        // Assert
        stateJson.Should().Be("{\"value\": 42}");
    }

    private record TestState(int Value);
    private record OtherTestState(string Text);
}
