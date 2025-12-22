using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EasyAppDev.Blazor.Store.Sample;
using EasyAppDev.Blazor.Store.Sample.State;
using EasyAppDev.Blazor.Store.Sample.Plugins;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.Persistence;
using EasyAppDev.Blazor.Store.History;
using EasyAppDev.Blazor.Store.Query;
using EasyAppDev.Blazor.Store.TabSync;
using EasyAppDev.Blazor.Store.Plugins;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.JSInterop;
#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics;
#endif
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Utilities;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register API services for demos using real REST APIs
builder.Services.AddScoped<JsonPlaceholderApi>();
builder.Services.AddScoped<ReqResApi>();
builder.Services.AddScoped<PublicApiService>();

// ============================================================================
// SECURITY NOTE: Diagnostics are DEBUG-only and expose full state snapshots
// Never ship diagnostic features to production
// ============================================================================
#if DEBUG
builder.Services.AddStoreDiagnostics();
#endif

// Register utility services required by StoreComponent
// Note: AddStoreUtilities() registers IDebounceManager, IThrottleManager, and ILazyCache
// These services are required dependencies for StoreComponent<T>
builder.Services.AddStoreUtilities();

// Register QueryClient for Query/Mutation demos
builder.Services.AddQueryClient();

// Register async action executors ONLY for state types that use ExecuteAsync
// These must be registered BEFORE their corresponding stores
builder.Services.AddAsyncActionExecutor<UserManagementState>();
builder.Services.AddAsyncActionExecutor<ComprehensiveDemoState>();

// Register custom plugins as singletons so they can be injected
builder.Services.AddSingleton<AnalyticsDemoPlugin>();

// ============================================================================
// Counter Store - Basic example with DevTools and Logging
// SECURITY: WithDefaults() includes DevTools - only use in DEBUG builds
// For production, use .WithLogging() instead
// ============================================================================
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter Store"));

// ============================================================================
// Debounce Store - Demonstrates debounce and throttle functionality
// ============================================================================
builder.Services.AddStore(
    new DebounceState(),
    (store, sp) => store.WithDefaults(sp, "Debounce Store"));

// ============================================================================
// Todo Store - Demonstrates immutable collections (ImmutableList)
// ============================================================================
builder.Services.AddStore(
    TodoState.Empty,
    (store, sp) => store.WithDefaults(sp, "Todo Store"));

// ============================================================================
// User Profile Store - Demonstrates async actions and loading states
// ============================================================================
builder.Services.AddStore(
    UserProfileState.Empty,
    (store, sp) => store.WithDefaults(sp, "User Profile Store"));

// ============================================================================
// AsyncData Demo Store - Demonstrates AsyncData<T> wrapper pattern
// Simplifies async state from 20+ lines to 1 property!
// ============================================================================
builder.Services.AddStore(
    AsyncDataDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "AsyncData Demo Store"));

// ============================================================================
// User Management Store - Demonstrates ExecuteAsync helper
// Automatic error handling - reduces try-catch from 12+ lines to 5 lines!
// ============================================================================
builder.Services.AddStore(
    UserManagementState.Initial,
    (store, sp) => store.WithDefaults(sp, "User Management Store"));

// ============================================================================
// Shopping Cart Store - Demonstrates persistence with LocalStorage
// State survives page refreshes and browser restarts
// WithPersistence automatically loads and saves state - no manual hydration needed!
// SECURITY: For production with sensitive data, use TransformOnSave to exclude
// sensitive fields and add IStateValidator to validate hydrated state
// ============================================================================
builder.Services.AddStore(
    ShoppingCartState.Empty,
    (store, sp) => store.WithDefaults(sp, "Shopping Cart Store")
                        .WithPersistence(sp, "shopping-cart-state"));

// ============================================================================
// Theme Store - Demonstrates selector optimization with SelectorStoreComponent
// Multiple components can subscribe to different slices of state
// WithPersistence automatically loads and saves state - no manual hydration needed!
// ============================================================================
builder.Services.AddStore(
    ThemeState.Default,
    (store, sp) => store.WithDefaults(sp, "Theme Store")
                        .WithPersistence(sp, "theme-state"));

// ============================================================================
// Product Catalog Store - Demonstrates LazyLoad with caching
// Automatic caching with request deduplication - no manual cache management!
// ============================================================================
builder.Services.AddStore(
    new ProductCatalogState(),
    (store, sp) => store.WithDefaults(sp, "Product Catalog Store"));

// ============================================================================
// Comprehensive Demo Store - Demonstrates ALL async helpers working together
// Debounce + Throttle + ExecuteAsync + LazyLoad + AsyncData
// Real-world e-commerce scenario showing all features in action!
// ============================================================================
builder.Services.AddStore(
    ComprehensiveDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Comprehensive Demo Store"));

// ============================================================================
// Form Validation Store - Demonstrates form state management and validation
// Field-level validation, async validation, and form submission
// Real-world form patterns with comprehensive error handling!
// ============================================================================
builder.Services.AddStore(
    new FormValidationState(),
    (store, sp) => store.WithDefaults(sp, "Form Validation Store"));

// ============================================================================
// Cross-Store Demo Stores - Demonstrates Phase 1 updates
// Auth and Cart stores for showcasing cross-store update patterns
// Shows proper use of UpdateAsync to prevent deadlocks!
// ============================================================================
builder.Services.AddStore(
    AuthDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Auth Demo Store"));

builder.Services.AddStore(
    CartDemoState.Empty,
    (store, sp) => store.WithDefaults(sp, "Cart Demo Store"));

// ============================================================================
// Optimistic Updates Demo Store - Demonstrates optimistic updates with rollback
// Update UI immediately, rollback automatically on server error
// ============================================================================
builder.Services.AddStore(
    OptimisticDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Optimistic Demo Store"));

// ============================================================================
// Editor History Store - Demonstrates Undo/Redo functionality
// Full history stack with memory management for editor-like experiences
// ============================================================================
builder.Services.AddStoreWithHistory(
    EditorHistoryState.Initial,
    opts => opts
        .WithMaxSize(100)
        .ExcludeActions("CURSOR", "SELECTION"),
    (store, sp) => store.WithDefaults(sp, "Editor History Store"));

// ============================================================================
// Immer Demo Store - Demonstrates Immer-style draft updates
// Clean syntax for complex nested state modifications
// ============================================================================
builder.Services.AddStore(
    ImmerDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Immer Demo Store"));

// ============================================================================
// Actions Demo Store - Demonstrates Redux-style action dispatching
// Type-safe actions with pattern matching reducers
// ============================================================================
builder.Services.AddStore(
    ActionsDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Actions Demo Store"));

// ============================================================================
// Tab Sync Demo Store - Demonstrates cross-tab synchronization
// Real-time state sync across browser tabs using BroadcastChannel
// SECURITY: For production with sensitive data, enable message signing:
//   .EnableMessageSigning()
//   .RequireValidSignature(true)
//   .MaxMessageAgeSeconds(30)
//   .ValidateTimestamp(true)
// ============================================================================
builder.Services.AddStore(
    TabSyncDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Tab Sync Demo Store")
                        .WithTabSync(sp, opts => opts
                            .Channel("tab-sync-demo")
                            .ExcludeActions("LOCAL_ONLY")));

// ============================================================================
// Plugin Demo Store - Demonstrates the plugin system
// Extensible hooks for logging, analytics, validation, and more
// ============================================================================
builder.Services.AddStore(
    PluginDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Plugin Demo Store")
                        .WithPlugin<PluginDemoState, LoggingPlugin>(sp)
                        .WithPlugin<PluginDemoState, ValidationPlugin>(sp)
                        .WithPlugin(sp.GetRequiredService<AnalyticsDemoPlugin>(), sp));

// ============================================================================
// Middleware Demo Store - Demonstrates custom middleware creation
// Shows logging, performance tracking, and functional middleware
// ============================================================================
builder.Services.AddStore(
    MiddlewareDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Middleware Demo Store"));

// ============================================================================
// Real API Demo Store - Showcases multiple free public REST APIs
// Dog CEO, Cat Fact, Chuck Norris, Open Trivia, Quotable
// ============================================================================
builder.Services.AddStore(
    RealApiDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Real API Demo Store"));

// ============================================================================
// Security Demo Store - Demonstrates sensitive data filtering
// Properties marked with [SensitiveData] are filtered from DevTools
// SECURITY BEST PRACTICE: Always mark passwords, tokens, API keys, and PII
// with [SensitiveData] attribute to prevent exposure in DevTools/logs
// Example: [property: SensitiveData] string Password
// ============================================================================
builder.Services.AddStore(
    SecurityDemoState.Initial,
    (store, sp) => store.WithDefaults(sp, "Security Demo Store"));

await builder.Build().RunAsync();
