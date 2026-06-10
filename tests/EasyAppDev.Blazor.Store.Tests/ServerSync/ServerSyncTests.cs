// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.ServerSync;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.ServerSync;

public record SyncTestState(int Count, string Name);

public class ServerSyncTests
{
    // --- StateOperation tests ---

    [Fact]
    public void StateOperation_Set_ShouldCreateSetOperation()
    {
        // Act
        var operation = StateOperation.Set("user.name", "\"John\"", "doc1");

        // Assert
        operation.OperationType.Should().Be("SET");
        operation.Path.Should().Be("user.name");
        operation.ValueJson.Should().Be("\"John\"");
        operation.DocumentId.Should().Be("doc1");
        operation.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void StateOperation_Insert_ShouldCreateInsertOperationWithIndex()
    {
        // Act
        var operation = StateOperation.Insert("items", 5, "{\"id\":1}", "doc1");

        // Assert
        operation.OperationType.Should().Be("INSERT");
        operation.Path.Should().Be("items");
        operation.ValueJson.Should().Be("{\"id\":1}");
        operation.Metadata.Should().ContainKey("index");
        operation.Metadata!["index"].Should().Be(5);
    }

    [Fact]
    public void StateOperation_Delete_ShouldCreateDeleteOperation()
    {
        // Act
        var operation = StateOperation.Delete("user.address", "doc1");

        // Assert
        operation.OperationType.Should().Be("DELETE");
        operation.Path.Should().Be("user.address");
        operation.ValueJson.Should().BeNull();
    }

    [Fact]
    public void StateOperation_Update_ShouldCreateUpdateOperationWithPrevious()
    {
        // Act
        var operation = StateOperation.Update("counter", "10", "5", "doc1");

        // Assert
        operation.OperationType.Should().Be("UPDATE");
        operation.Path.Should().Be("counter");
        operation.ValueJson.Should().Be("10");
        operation.PreviousValueJson.Should().Be("5");
    }

    // --- CursorInfo tests ---

    [Fact]
    public void CursorInfo_HasSelection_ShouldReturnTrueWhenSelectionExists()
    {
        // Arrange
        var cursor = new CursorInfo
        {
            ConnectionId = "conn1",
            SelectionStart = 10,
            SelectionEnd = 20
        };

        // Assert
        cursor.HasSelection.Should().BeTrue();
    }

    [Fact]
    public void CursorInfo_HasSelection_ShouldReturnFalseWhenNoSelection()
    {
        // Arrange
        var cursor = new CursorInfo
        {
            ConnectionId = "conn1",
            Position = 10
        };

        // Assert
        cursor.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void CursorInfo_HasSelection_ShouldReturnFalseWhenSameStartEnd()
    {
        // Arrange
        var cursor = new CursorInfo
        {
            ConnectionId = "conn1",
            SelectionStart = 10,
            SelectionEnd = 10
        };

        // Assert
        cursor.HasSelection.Should().BeFalse();
    }

    // --- Conflict resolution tests ---

    [Fact]
    public void LastWriteWinsResolver_ShouldReturnRemoteState()
    {
        // Arrange
        var resolver = new LastWriteWinsResolver<SyncTestState>();
        var local = new SyncTestState(10, "Local");
        var remote = new SyncTestState(20, "Remote");

        // Act
        var result = resolver.Resolve(local, remote, null);

        // Assert
        result.Should().Be(remote);
    }

    [Fact]
    public void ServerWinsResolver_ShouldReturnRemoteState()
    {
        // Arrange
        var resolver = new ServerWinsResolver<SyncTestState>();
        var local = new SyncTestState(10, "Local");
        var remote = new SyncTestState(20, "Remote");

        // Act
        var result = resolver.Resolve(local, remote, null);

        // Assert
        result.Should().Be(remote);
    }

    [Fact]
    public void ClientWinsResolver_ShouldReturnLocalState()
    {
        // Arrange
        var resolver = new ClientWinsResolver<SyncTestState>();
        var local = new SyncTestState(10, "Local");
        var remote = new SyncTestState(20, "Remote");

        // Act
        var result = resolver.Resolve(local, remote, null);

        // Assert
        result.Should().Be(local);
    }

    // --- PresenceInfo tests ---

    [Fact]
    public void PresenceInfo_ShouldStoreUserInformation()
    {
        // Arrange & Act
        var presence = new PresenceInfo
        {
            ConnectionId = "conn123",
            DisplayName = "John Doe",
            ConnectedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["role"] = "editor",
                ["color"] = "#FF0000"
            }
        };

        // Assert
        presence.ConnectionId.Should().Be("conn123");
        presence.DisplayName.Should().Be("John Doe");
        presence.Metadata.Should().ContainKey("role");
        presence.Metadata!["role"].Should().Be("editor");
    }

    // --- StateUpdate tests ---

    [Fact]
    public void StateUpdate_ShouldHaveAllRequiredProperties()
    {
        // Arrange & Act
        var update = new StateUpdate
        {
            StateJson = "{\"count\": 10}",
            Action = "INCREMENT",
            Timestamp = DateTime.UtcNow,
            SenderId = "sender1",
            DocumentId = "doc1",
            Version = 5
        };

        // Assert
        update.StateJson.Should().Be("{\"count\": 10}");
        update.Action.Should().Be("INCREMENT");
        update.SenderId.Should().Be("sender1");
        update.DocumentId.Should().Be("doc1");
        update.Version.Should().Be(5);
    }

    // --- ServerSyncOptions tests ---

    [Fact]
    public void ServerSyncOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<SyncTestState> { HubUrl = "/hubs/sync" };

        // Assert
        options.HubUrl.Should().Be("/hubs/sync");
        options.ConflictResolution.Should().Be(ConflictResolution.LastWriteWins);
        options.AutoConnect.Should().BeTrue();
        options.AutoReconnect.Should().BeTrue();
        options.EnablePresence.Should().BeFalse();
        options.EnableCursorTracking.Should().BeFalse();
        options.EnableOfflineQueue.Should().BeFalse();
        options.UseOperationBasedSync.Should().BeFalse();
        options.MaxOfflineQueueSize.Should().Be(100);
        options.SyncDebounce.Should().Be(TimeSpan.FromMilliseconds(100));
        options.CursorDebounce.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void ServerSyncOptions_ExcludedActions_ShouldContainDefaultActions()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<SyncTestState> { HubUrl = "/hubs/sync" };

        // Assert
        options.ExcludedActions.Should().Contain("@@INIT");
        options.ExcludedActions.Should().Contain("@@SYNC");
        options.ExcludedActions.Should().Contain("@@SYNC_FULL");
        options.ExcludedActions.Should().Contain("@@JUMP_TO_STATE");
    }

    [Fact]
    public void ServerSyncOptions_ReconnectDelays_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new ServerSyncOptions<SyncTestState> { HubUrl = "/hubs/sync" };

        // Assert
        options.ReconnectDelays.Should().HaveCount(5);
        options.ReconnectDelays[0].Should().Be(TimeSpan.FromSeconds(0));
        options.ReconnectDelays[1].Should().Be(TimeSpan.FromSeconds(2));
        options.ReconnectDelays[2].Should().Be(TimeSpan.FromSeconds(5));
        options.ReconnectDelays[3].Should().Be(TimeSpan.FromSeconds(10));
        options.ReconnectDelays[4].Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ServerSyncOptions_ShouldAcceptCustomConflictResolver()
    {
        // Arrange
        var customResolver = new MergeResolver();

        // Act
        var options = new ServerSyncOptions<SyncTestState>
        {
            HubUrl = "/hubs/sync",
            ConflictResolution = ConflictResolution.Custom,
            CustomConflictResolver = customResolver
        };

        // Assert
        options.CustomConflictResolver.Should().Be(customResolver);
    }

    private class MergeResolver : IConflictResolver<SyncTestState>
    {
        public SyncTestState Resolve(SyncTestState local, SyncTestState remote, SyncTestState? common)
        {
            // Merge: take max count, prefer remote name
            return new SyncTestState(Math.Max(local.Count, remote.Count), remote.Name);
        }
    }

    // --- Operation signing tests ---

    [Fact]
    public void StateOperation_GetSignaturePayload_ShouldCoverIdentityAndValue()
    {
        // Arrange
        var operation = StateOperation.Set("user.name", "\"John\"", "doc1");

        // Act
        var payload = operation.GetSignaturePayload();

        // Assert - payload binds the operation's identity, type, path and value
        payload.Should().Be($"{operation.Id}|SET|user.name|\"John\"");
    }

    [Fact]
    public void StateOperation_Signature_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var operation = StateOperation.Set("/", "{}", "doc1");

        // Assert
        operation.Signature.Should().BeNull();
    }

    // --- Middleware attach/store wiring tests ---

    [Fact]
    public void AttachStore_WithAutoConnectDisabled_DoesNotConnect()
    {
        // Arrange
        var options = new ServerSyncOptions<SyncTestState>
        {
            HubUrl = "https://localhost/hubs/sync",
            RequireValidation = false,
            AutoConnect = false
        };
        var middleware = new ServerSyncMiddleware<SyncTestState>(options);
        var store = StoreBuilder<SyncTestState>.Create(new SyncTestState(0, "init")).Build();

        // Act
        middleware.AttachStore(store);

        // Assert - no connection attempt was made
        middleware.ConnectionState.Should().Be(SyncConnectionState.Disconnected);
    }

    [Fact]
    public void AttachStore_CalledTwiceWithSameStore_IsIdempotent()
    {
        // Arrange
        var options = new ServerSyncOptions<SyncTestState>
        {
            HubUrl = "https://localhost/hubs/sync",
            RequireValidation = false,
            AutoConnect = false
        };
        var middleware = new ServerSyncMiddleware<SyncTestState>(options);
        var store = StoreBuilder<SyncTestState>.Create(new SyncTestState(0, "init")).Build();

        // Act & Assert - no throw on repeated attach
        middleware.AttachStore(store);
        var act = () => middleware.AttachStore(store);
        act.Should().NotThrow();
    }

    [Fact]
    public void AttachStore_NullStore_Throws()
    {
        // Arrange
        var options = new ServerSyncOptions<SyncTestState>
        {
            HubUrl = "https://localhost/hubs/sync",
            RequireValidation = false,
            AutoConnect = false
        };
        var middleware = new ServerSyncMiddleware<SyncTestState>(options);

        // Act & Assert
        var act = () => middleware.AttachStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ServerSyncMiddleware_ImplementsIStoreAwareMiddleware()
    {
        // Arrange
        var options = new ServerSyncOptions<SyncTestState>
        {
            HubUrl = "https://localhost/hubs/sync",
            RequireValidation = false,
            AutoConnect = false
        };
        var middleware = new ServerSyncMiddleware<SyncTestState>(options);

        // Assert - StoreBuilder.Build attaches stores via this interface
        middleware.Should().BeAssignableTo<EasyAppDev.Blazor.Store.Middleware.IStoreAwareMiddleware<SyncTestState>>();
    }
}
