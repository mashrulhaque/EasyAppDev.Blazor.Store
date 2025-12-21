using EasyAppDev.Blazor.Store.Blazor;
using EasyAppDev.Blazor.Store.ServerSample;
using EasyAppDev.Blazor.Store.ServerSample.State;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register store utilities (required for StoreComponent)
builder.Services.AddStoreUtilities();

// ============================================================================
// SINGLETON STORE - Shared across ALL users (demonstrates the problem)
// SECURITY: Singleton stores should NOT contain user-specific data
// Use scoped stores for per-user isolation
// ============================================================================
builder.Services.AddStore(
    new SingletonCounterState(),
    (store, sp) => store.WithLogging());  // Only logging works in Server mode

// ============================================================================
// SCOPED STORES - Isolated per user/circuit (the solution!)
// With IServiceProvider access, DevTools now work!
// SECURITY: Scoped stores provide per-user isolation - use for user-specific data
// For production, wrap DevTools with #if DEBUG
// ============================================================================

// Scoped counter - each user gets their own
builder.Services.AddScopedStore(
    new ScopedCounterState(),
    (store, sp) => store
        .WithDevTools(sp, "Scoped Counter")  // ✅ DevTools work with scoped stores!
        .WithLogging());

// User session - tracks per-user data
builder.Services.AddScopedStore(
    sp => UserSessionState.Create(),  // Factory creates unique session per user
    (store, sp) => store
        .WithDevTools(sp, "User Session")
        .WithLogging());

// Scoped cart - demonstrates cross-store updates
builder.Services.AddScopedStore(
    ScopedCartState.Empty,
    (store, sp) => store
        .WithDevTools(sp, "Scoped Cart")
        .WithLogging());

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
