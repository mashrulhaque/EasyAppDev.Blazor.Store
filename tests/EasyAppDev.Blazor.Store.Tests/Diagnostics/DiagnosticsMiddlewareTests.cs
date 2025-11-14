#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Diagnostics;

public class DiagnosticsMiddlewareTests
{
    private readonly DiagnosticsService _diagnosticsService;
    private readonly DiagnosticsMiddleware<TestState> _middleware;

    public DiagnosticsMiddlewareTests()
    {
        _diagnosticsService = new DiagnosticsService();
        _middleware = new DiagnosticsMiddleware<TestState>(_diagnosticsService);
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldRecordActionEntry()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions.Should().ContainSingle();
        actions[0].Action.Should().Be("TEST_ACTION");
        actions[0].StateType.Should().Be(typeof(TestState));
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldCalculateDuration()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await Task.Delay(10); // Simulate some work
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions[0].Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldSerializeStateAsJson()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions[0].PreviousStateJson.Should().NotBeNullOrEmpty();
        actions[0].NewStateJson.Should().NotBeNullOrEmpty();
        actions[0].PreviousStateJson.Should().Contain("\"value\": 10");
        actions[0].NewStateJson.Should().Contain("\"value\": 20");
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldCalculateStateSizes()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions[0].PreviousStateSize.Should().BeGreaterThan(0);
        actions[0].NewStateSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldCalculateStateDiff()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions[0].Diff.Should().NotBeNull();
        actions[0].Diff!.AreIdentical.Should().BeFalse();
        actions[0].Diff.Changes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldDetectPropertyChanges()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "old");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, "TEST_ACTION");
        await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST_ACTION");

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        var diff = actions[0].Diff;
        diff.Should().NotBeNull();

        var valueChange = diff!.Changes.FirstOrDefault(c => c.PropertyName == "value");
        valueChange.Should().NotBeNull();
        valueChange!.IsAdded.Should().BeFalse();
        valueChange.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldHandleNullAction()
    {
        // Arrange
        var previousState = new TestState(10, "old");
        var currentState = new TestState(20, "new");

        // Act
        await _middleware.OnBeforeUpdateAsync(previousState, null);
        await _middleware.OnAfterUpdateAsync(previousState, currentState, null);

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions.Should().ContainSingle();
        actions[0].Action.Should().BeNull();
    }

    [Fact]
    public async Task OnAfterUpdateAsync_ShouldNotThrowOnSerializationError()
    {
        // Arrange - Create a state that might have serialization issues
        var previousState = new TestState(10, "test");
        var currentState = new TestState(20, "test");

        // Act & Assert - Should not throw
        var act = async () =>
        {
            await _middleware.OnBeforeUpdateAsync(previousState, "TEST");
            await _middleware.OnAfterUpdateAsync(previousState, currentState, "TEST");
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MultipleUpdates_ShouldRecordAllActions()
    {
        // Arrange
        var states = new[]
        {
            new TestState(1, "a"),
            new TestState(2, "b"),
            new TestState(3, "c")
        };

        // Act
        for (int i = 0; i < states.Length - 1; i++)
        {
            await _middleware.OnBeforeUpdateAsync(states[i], $"ACTION_{i}");
            await _middleware.OnAfterUpdateAsync(states[i], states[i + 1], $"ACTION_{i}");
        }

        // Assert
        var actions = _diagnosticsService.GetRecentActions(typeof(TestState));
        actions.Should().HaveCount(2);
        actions[0].Action.Should().Be("ACTION_1"); // Most recent
        actions[1].Action.Should().Be("ACTION_0");
    }

    private record TestState(int Value, string Text);
}
#endif
