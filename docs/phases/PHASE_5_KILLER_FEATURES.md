# Phase 5: Killer Features

> Version: 3.0.0 | Status: Not Started | Risk: High (Innovation Required)

## Overview

Phase 5 transforms the library from "good" to "must-have." These features make EasyAppDev.Blazor.Store the undisputed best choice for Blazor state management.

**Goal:** Become the library developers *want* to use, not just the one they settle for.

---

## Features

### 5.1 TanStack Query-Style Data Fetching

**The Vision:**
TanStack Query (React Query) revolutionized data fetching in React. We bring the same experience to Blazor.

**Problem with Current Approach:**
```csharp
// Current: Manual, verbose, error-prone
public record UsersState(
    AsyncData<List<User>> Users,
    bool IsRefreshing);

@code {
    protected override async Task OnInitializedAsync()
    {
        await Update(s => s with { Users = AsyncData<List<User>>.Loading() });
        try
        {
            var users = await Http.GetFromJsonAsync<List<User>>("/api/users");
            await Update(s => s with { Users = AsyncData<List<User>>.Success(users!) });
        }
        catch (Exception ex)
        {
            await Update(s => s with { Users = AsyncData<List<User>>.Failure(ex.Message) });
        }
    }

    async Task Refresh()
    {
        await Update(s => s with { IsRefreshing = true });
        // Repeat the above...
    }
}
```

**The Solution:**
```csharp
@inherits QueryComponent

@code {
    Query<List<User>> Users = default!;

    protected override void OnInitialized()
    {
        Users = UseQuery(
            key: "users",
            queryFn: () => Http.GetFromJsonAsync<List<User>>("/api/users")
        );
    }
}

@if (Users.IsLoading)
{
    <Spinner />
}
else if (Users.IsError)
{
    <ErrorMessage Message="@Users.Error" OnRetry="Users.Refetch" />
}
else if (Users.Data is { } users)
{
    <UserList Users="users" />
}
```

**Full API:**
```csharp
// Query with all options
var users = UseQuery(new QueryOptions<List<User>>
{
    Key = "users",
    QueryFn = () => Http.GetFromJsonAsync<List<User>>("/api/users"),

    // Caching
    StaleTime = TimeSpan.FromMinutes(5),      // Data considered fresh for 5 min
    CacheTime = TimeSpan.FromMinutes(30),     // Keep in cache for 30 min after unmount

    // Refetching
    RefetchOnWindowFocus = true,              // Refetch when user returns to tab
    RefetchInterval = TimeSpan.FromSeconds(30), // Poll every 30 seconds
    RefetchOnReconnect = true,                // Refetch when network reconnects

    // Error handling
    Retry = 3,                                 // Retry failed requests 3 times
    RetryDelay = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff

    // Callbacks
    OnSuccess = data => Console.WriteLine($"Loaded {data.Count} users"),
    OnError = ex => ErrorTracker.Capture(ex),
    OnSettled = () => Analytics.Track("users_loaded"),

    // Transformations
    Select = users => users.Where(u => u.IsActive).ToList(),

    // Dependencies
    Enabled = () => IsAuthenticated  // Only run when condition is true
});
```

**Query State:**
```csharp
public class Query<T>
{
    // State
    public T? Data { get; }
    public Exception? Error { get; }
    public QueryStatus Status { get; }

    // Derived state
    public bool IsLoading => Status == QueryStatus.Loading;
    public bool IsError => Status == QueryStatus.Error;
    public bool IsSuccess => Status == QueryStatus.Success;
    public bool IsFetching { get; }      // True during any fetch (including background)
    public bool IsStale { get; }         // True if data is older than staleTime
    public bool IsPlaceholderData { get; }

    // Actions
    public Task Refetch();
    public Task Invalidate();
    public void SetData(T data);

    // Metadata
    public DateTime? DataUpdatedAt { get; }
    public int FailureCount { get; }
}

public enum QueryStatus { Idle, Loading, Error, Success }
```

**Mutations:**
```csharp
@code {
    Mutation<User, CreateUserRequest> CreateUser = default!;

    protected override void OnInitialized()
    {
        CreateUser = UseMutation(new MutationOptions<User, CreateUserRequest>
        {
            MutationFn = request => Http.PostAsJsonAsync<User>("/api/users", request),

            OnSuccess = (user, request) =>
            {
                // Invalidate and refetch users query
                QueryClient.InvalidateQueries("users");

                // Or optimistically update cache
                QueryClient.SetQueryData<List<User>>("users",
                    users => users.Append(user).ToList());
            }
        });
    }

    async Task HandleSubmit()
    {
        await CreateUser.MutateAsync(new CreateUserRequest { Name = name });
    }
}

<button @onclick="HandleSubmit" disabled="@CreateUser.IsLoading">
    @(CreateUser.IsLoading ? "Creating..." : "Create User")
</button>
```

**Query Client (Global):**
```csharp
// Program.cs
builder.Services.AddQueryClient(options =>
{
    options.DefaultStaleTime = TimeSpan.FromMinutes(5);
    options.DefaultCacheTime = TimeSpan.FromMinutes(30);
    options.DefaultRetry = 3;
});

// Access anywhere
@inject IQueryClient QueryClient

QueryClient.InvalidateQueries("users");
QueryClient.InvalidateQueries(key => key.StartsWith("user-"));
QueryClient.RefetchQueries("users");
QueryClient.GetQueryData<List<User>>("users");
QueryClient.SetQueryData("users", newData);
QueryClient.Clear();
```

**DevTools Integration:**
Query state visible in Redux DevTools:
- Active queries
- Cache contents
- Stale/fresh status
- Fetch history

---

### 5.2 Immer-Style Mutable Syntax

**The Vision:**
Write mutations that look mutable but produce immutable updates.

**Problem:**
```csharp
// Current: Verbose, error-prone for nested updates
await Update(s => s with
{
    Users = s.Users.SetItem(
        s.Users.FindIndex(u => u.Id == userId),
        s.Users.First(u => u.Id == userId) with
        {
            Profile = s.Users.First(u => u.Id == userId).Profile with
            {
                Address = s.Users.First(u => u.Id == userId).Profile.Address with
                {
                    City = "New York"
                }
            }
        }
    )
});
```

**The Solution:**
```csharp
// Looks mutable, produces immutable update
await Store.Produce(draft =>
{
    var user = draft.Users.First(u => u.Id == userId);
    user.Profile.Address.City = "New York";
});
```

**Implementation Approach:**

**Option A: Proxy-Based (Like Immer.js)**
```csharp
public static class ImmerExtensions
{
    public static Task Produce<TState>(
        this IStore<TState> store,
        Action<TState> recipe,
        string? action = null)
        where TState : class
    {
        return store.UpdateAsync(state =>
        {
            var proxy = ProxyGenerator.CreateProxy(state);
            recipe(proxy);
            return proxy.Produce();  // Generate new immutable state from changes
        }, action);
    }
}

// Uses Castle.Core or similar for dynamic proxy generation
```

**Option B: Expression Tree Analysis**
```csharp
public static Task Produce<TState>(
    this IStore<TState> store,
    Expression<Action<TState>> recipe,
    string? action = null)
    where TState : notnull
{
    var visitor = new MutationExpressionVisitor<TState>();
    var updater = visitor.ConvertToUpdater(recipe);
    return store.UpdateAsync(updater, action);
}

// Analyzes expression tree and generates with expressions
```

**Option C: Source Generator**
```csharp
[Produce]
void UpdateUserCity(AppState state, int userId, string city)
{
    state.Users.First(u => u.Id == userId).Profile.Address.City = city;
}

// Generated at compile time:
public static AppState UpdateUserCity(this AppState state, int userId, string city)
{
    var userIndex = state.Users.FindIndex(u => u.Id == userId);
    var user = state.Users[userIndex];
    return state with
    {
        Users = state.Users.SetItem(userIndex, user with
        {
            Profile = user.Profile with
            {
                Address = user.Profile.Address with { City = city }
            }
        })
    };
}
```

**Collection Mutations:**
```csharp
await Store.Produce(draft =>
{
    // Array mutations
    draft.Items.Add(newItem);
    draft.Items.Remove(item);
    draft.Items[0].Name = "Updated";

    // Dictionary mutations
    draft.UsersById[userId].Name = "New Name";
    draft.UsersById.Remove(userId);

    // Nested updates
    draft.Cart.Items.First(i => i.Id == itemId).Quantity++;
});
```

---

### 5.3 Full DevTools Time-Travel

**The Vision:**
Not just view state history, but replay, skip, and modify actions.

**Current:**
- View state at each action
- Basic time-travel

**Enhanced:**
```csharp
builder.Services.AddStore(state, (store, sp) => store
    .WithDevTools(sp, new DevToolsOptions
    {
        Name = "AppStore",

        // Time travel
        EnableTimeTravel = true,
        MaxHistory = 100,

        // Action replay
        EnableActionReplay = true,

        // State editing
        EnableStateEditing = true,

        // Action filtering
        ActionFilter = action => !action.StartsWith("@@"),

        // State sanitization (hide sensitive data)
        StateSanitizer = state => state with { Password = "***" },

        // Action transformation for DevTools
        ActionTransformer = action => new { type = action, timestamp = DateTime.UtcNow }
    })
);
```

**Features:**
1. **Action Replay:** Re-execute actions from DevTools
2. **Action Skipping:** Skip actions in history to see alternative state
3. **State Editing:** Modify state directly in DevTools
4. **State Export/Import:** Save and load state snapshots
5. **Action Dispatch:** Dispatch actions directly from DevTools

**JS Integration:**
```javascript
// devtools.js (enhanced)
export function initDevTools(storeName, options) {
    const devTools = window.__REDUX_DEVTOOLS_EXTENSION__?.connect({
        name: storeName,
        features: {
            jump: true,      // Time travel
            skip: true,      // Skip actions
            dispatch: true,  // Dispatch from DevTools
            persist: true,   // Persist state
            export: true,    // Export state
            import: true,    // Import state
        }
    });

    devTools?.subscribe(message => {
        switch (message.type) {
            case 'DISPATCH':
                handleDispatch(message.payload);
                break;
            case 'JUMP_TO_ACTION':
                handleJump(message.payload);
                break;
        }
    });
}
```

---

### 5.4 Plugin Ecosystem

**The Vision:**
First-class plugin system for community extensions.

**Plugin Interface:**
```csharp
public interface IStorePlugin<TState> where TState : notnull
{
    string Name { get; }
    Version Version { get; }

    void Configure(StoreBuilder<TState> builder, IServiceProvider services);
    Task OnStoreCreated(IStore<TState> store);
    Task OnStoreDisposed(IStore<TState> store);
}
```

**Example Plugins:**

**1. Offline Plugin**
```csharp
builder.Services.AddStore(state, (store, sp) => store
    .UsePlugin<OfflinePlugin>(options =>
    {
        options.StorageName = "offline-queue";
        options.SyncEndpoint = "/api/sync";
        options.ConflictResolution = ConflictResolution.LastWriteWins;
    })
);
```

**2. Analytics Plugin**
```csharp
.UsePlugin<AnalyticsPlugin>(options =>
{
    options.Provider = sp.GetRequiredService<IAnalyticsProvider>();
    options.TrackActions = true;
    options.TrackStateSize = true;
    options.SampleRate = 0.1; // 10% sampling
})
```

**3. Encryption Plugin**
```csharp
.UsePlugin<EncryptionPlugin>(options =>
{
    options.Key = configuration["EncryptionKey"];
    options.EncryptPersistence = true;
    options.SensitiveFields = new[] { "Password", "CreditCard" };
})
```

**4. Validation Plugin**
```csharp
.UsePlugin<ValidationPlugin>(options =>
{
    options.Validator = new FluentValidationValidator<MyState>();
    options.OnValidationError = errors => logger.LogWarning("Invalid state: {Errors}", errors);
    options.PreventInvalidUpdates = true;
})
```

**Plugin Discovery:**
```csharp
// Auto-discover plugins from assemblies
builder.Services.AddStore(state, (store, sp) => store
    .UsePlugins(assembly: typeof(Program).Assembly)
    .UsePlugins(typeof(OfflinePlugin).Assembly)
);
```

---

### 5.5 IDE Tooling

**The Vision:**
First-class support in Visual Studio and JetBrains Rider.

**Visual Studio Extension:**

**1. State Visualizer**
- Live view of current state
- Expandable tree view
- Search and filter
- Copy as JSON

**2. Action Logger**
- Real-time action stream
- Filter by action type
- Click to see state diff

**3. Store Explorer**
- List all registered stores
- View subscribers
- See middleware chain
- Performance metrics

**4. Code Generation**
- Right-click → "Generate Store State"
- Templates for common patterns
- Snippet support

**5. Diagnostics**
- Inline warnings for anti-patterns
- Performance suggestions
- Unused subscription detection

**Rider Plugin:**
- Same features via JetBrains plugin SDK
- Integration with Rider's debugger
- Custom inspections

**Implementation:**
```xml
<!-- VS Extension -->
<VSIXManifest>
    <Identifier Id="EasyAppDev.Blazor.Store.VSExtension" />
    <Name>EasyAppDev Store Tools</Name>
</VSIXManifest>
```

---

### 5.6 Server-Side State Sync

**The Vision:**
Real-time state synchronization between server and all connected clients.

**Use Cases:**
- Collaborative editing
- Live dashboards
- Multi-player games
- Real-time notifications

**API:**
```csharp
// Server (Program.cs)
builder.Services.AddStoreHub<DocumentState>();

app.MapStoreHub<DocumentState>("/hubs/document");

// Client
builder.Services.AddStore(
    DocumentState.Empty,
    (store, sp) => store
        .WithServerSync(options =>
        {
            options.HubUrl = "/hubs/document";
            options.DocumentId = documentId;

            // Conflict resolution
            options.ConflictResolution = ConflictResolution.OperationalTransform;

            // Selective sync
            options.SyncSelector = s => s.Content; // Only sync content, not UI state

            // Presence
            options.EnablePresence = true;
            options.OnUserJoined = user => /* ... */;
            options.OnUserLeft = user => /* ... */;
        })
);
```

**Server Hub:**
```csharp
public class StoreHub<TState> : Hub where TState : notnull
{
    private readonly IStoreSync<TState> _sync;

    public async Task SendUpdate(StateUpdate update)
    {
        var resolvedState = await _sync.ResolveConflicts(update);
        await Clients.Others.SendAsync("ReceiveUpdate", resolvedState);
    }

    public async Task<TState> GetCurrentState(string documentId)
    {
        return await _sync.GetState(documentId);
    }
}
```

**Operational Transform:**
For collaborative editing, implement OT or CRDT:
```csharp
public interface IConflictResolver<TState>
{
    TState Resolve(TState local, TState remote, TState common);
}

public class OTConflictResolver<TState> : IConflictResolver<TState>
{
    // Operational transformation logic
}

public class CRDTConflictResolver<TState> : IConflictResolver<TState>
{
    // Conflict-free replicated data type logic
}
```

---

## Implementation Roadmap

### v3.0.0-alpha
- Query/Mutation basics
- Simple Immer syntax

### v3.0.0-beta
- Full Query API
- DevTools enhancements
- Plugin system foundation

### v3.0.0
- Production-ready Query
- Plugin ecosystem launch
- IDE tooling preview

### v3.1.0
- Server-side sync
- Advanced Immer
- Full IDE tooling

---

## Technical Challenges

### Query System
- Cache invalidation strategies
- Memory management for large caches
- Background fetch scheduling
- SSR compatibility

### Immer Syntax
- Proxy performance overhead
- Complex nested updates
- Collection mutation tracking
- Source generator complexity

### Server Sync
- Conflict resolution algorithms
- Network partition handling
- Eventual consistency guarantees
- Scale to many connections

---

## Success Criteria

1. **Query adoption:** 80% of data fetching uses Query API
2. **Developer satisfaction:** NPS > 50
3. **Performance:** No measurable overhead vs. manual approach
4. **Plugin ecosystem:** 10+ community plugins
5. **IDE adoption:** 50%+ users install tooling

---

## Competition Analysis

| Feature | EasyAppDev (v3) | Fluxor | Blazor-State | Redux |
|---------|-----------------|--------|--------------|-------|
| Query/Mutations | ✅ | ❌ | ❌ | (RTK Query) |
| Immer Syntax | ✅ | ❌ | ❌ | ✅ |
| Time Travel | ✅ | ✅ | ❌ | ✅ |
| Plugin System | ✅ | ❌ | ❌ | ✅ |
| Server Sync | ✅ | ❌ | ❌ | ❌ |
| IDE Tooling | ✅ | ❌ | ❌ | ✅ |
| Simplicity | ✅ | ❌ | ✅ | ❌ |

**Our Advantage:** Combining React ecosystem's best ideas with C# type safety and Blazor integration.

---

## The "Killer" Pitch

> "EasyAppDev.Blazor.Store is what happens when you take the best ideas from Zustand, TanStack Query, and Immer, and rebuild them for C# developers who value type safety and simplicity. It's the state management library that makes you enjoy writing Blazor apps."

---

[← Phase 4](PHASE_4_ADVANCED_FEATURES.md) | [Back to Roadmap](../ROADMAP.md)
