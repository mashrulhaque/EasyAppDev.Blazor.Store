using EasyAppDev.Blazor.Store.TabSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.TabSync;

public class TabSyncOptionsTests
{
    [Fact]
    public void ShouldSyncAction_WithNoFilters_ReturnsTrue()
    {
        // Arrange
        var options = new TabSyncOptions();

        // Assert
        options.ShouldSyncAction("ANY_ACTION").Should().BeTrue();
        options.ShouldSyncAction(null).Should().BeTrue();
    }

    [Fact]
    public void ShouldSyncAction_WithWhitelist_OnlySyncsMatchingActions()
    {
        // Arrange
        var options = new TabSyncOptions()
            .SyncActions("ADD_ITEM", "REMOVE_ITEM");

        // Assert
        options.ShouldSyncAction("ADD_ITEM").Should().BeTrue();
        options.ShouldSyncAction("REMOVE_ITEM_FROM_CART").Should().BeTrue(); // Contains match
        options.ShouldSyncAction("UPDATE_UI").Should().BeFalse();
        options.ShouldSyncAction(null).Should().BeFalse(); // Null doesn't match whitelist
    }

    [Fact]
    public void ShouldSyncAction_WithExcludedActions_ExcludesMatching()
    {
        // Arrange
        var options = new TabSyncOptions()
            .ExcludeActions("UI_STATE", "CURSOR");

        // Assert
        options.ShouldSyncAction("ADD_ITEM").Should().BeTrue();
        options.ShouldSyncAction("UI_STATE_CHANGE").Should().BeFalse();
        options.ShouldSyncAction("CURSOR_MOVE").Should().BeFalse();
        options.ShouldSyncAction(null).Should().BeTrue(); // Null not excluded
    }

    [Fact]
    public void ShouldSyncAction_WithBothFilters_AppliesBoth()
    {
        // Arrange
        var options = new TabSyncOptions()
            .SyncActions("CART")
            .ExcludeActions("TEMP");

        // Assert
        options.ShouldSyncAction("CART_ADD_ITEM").Should().BeTrue();
        options.ShouldSyncAction("CART_TEMP_UPDATE").Should().BeFalse(); // Excluded takes precedence
        options.ShouldSyncAction("USER_UPDATE").Should().BeFalse(); // Not in whitelist
    }

    [Fact]
    public void ShouldSyncAction_IsCaseInsensitive()
    {
        // Arrange
        var options = new TabSyncOptions()
            .SyncActions("ADD_ITEM")
            .ExcludeActions("CURSOR");

        // Assert
        options.ShouldSyncAction("add_item").Should().BeTrue();
        options.ShouldSyncAction("cursor_move").Should().BeFalse();
    }

    [Fact]
    public void Channel_SetsChannelName()
    {
        // Arrange & Act
        var options = new TabSyncOptions().Channel("my-channel");

        // Assert
        options.ChannelName.Should().Be("my-channel");
    }

    [Fact]
    public void Debounce_SetsDebounceMs()
    {
        // Arrange & Act
        var options = new TabSyncOptions().Debounce(100);

        // Assert
        options.DebounceMs.Should().Be(100);
    }

    [Fact]
    public void OnReceived_SetsCallback()
    {
        // Arrange
        string? receivedAction = null;
        var options = new TabSyncOptions()
            .OnReceived(action => receivedAction = action);

        // Act
        options.OnSyncReceived?.Invoke("TEST_ACTION");

        // Assert
        receivedAction.Should().Be("TEST_ACTION");
    }

    [Fact]
    public void OnError_SetsCallback()
    {
        // Arrange
        Exception? receivedException = null;
        var options = new TabSyncOptions()
            .OnError(ex => receivedException = ex);

        // Act
        options.OnSyncError?.Invoke(new InvalidOperationException("Test error"));

        // Assert
        receivedException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void DefaultOptions_HaveCorrectDefaults()
    {
        // Arrange
        var options = new TabSyncOptions();

        // Assert
        options.ChannelName.Should().BeNull();
        options.SyncedActions.Should().BeEmpty();
        options.ExcludedActions.Should().BeEmpty();
        options.SyncFullState.Should().BeTrue();
        options.DebounceMs.Should().Be(0);
        options.OnSyncReceived.Should().BeNull();
        options.OnSyncError.Should().BeNull();
    }

    [Fact]
    public void FluentApi_SupportsChaining()
    {
        // Arrange & Act
        var options = new TabSyncOptions()
            .Channel("test-channel")
            .SyncActions("ADD", "REMOVE")
            .ExcludeActions("UI")
            .Debounce(50)
            .OnReceived(_ => { })
            .OnError(_ => { });

        // Assert
        options.ChannelName.Should().Be("test-channel");
        options.SyncedActions.Should().HaveCount(2);
        options.ExcludedActions.Should().HaveCount(1);
        options.DebounceMs.Should().Be(50);
        options.OnSyncReceived.Should().NotBeNull();
        options.OnSyncError.Should().NotBeNull();
    }
}
