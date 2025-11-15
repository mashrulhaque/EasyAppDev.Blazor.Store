using BlazorUnitedTest;
using BlazorUnitedTest.Components;
using EasyAppDev.Blazor.Store.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add store utilities (required for StoreComponent)
builder.Services.AddStoreUtilities();

// Register the store with NEW lazy IJSRuntime resolution
// This now works in Server, WASM, and Auto render modes!
builder.Services.AddStore(
    new CounterState(0),
    (store, sp) => store.WithDefaults(sp, "Counter")  // ✅ Works in all modes!
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
