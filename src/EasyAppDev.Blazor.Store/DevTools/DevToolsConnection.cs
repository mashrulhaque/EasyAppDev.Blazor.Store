using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Manages connection to Redux DevTools.
/// </summary>
internal class DevToolsConnection : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private bool _connected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsConnection"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    public DevToolsConnection(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Connects to Redux DevTools.
    /// </summary>
    /// <param name="storeName">The name of the store.</param>
    /// <returns>True if connection was successful; otherwise, false.</returns>
    public async Task<bool> ConnectAsync(string storeName)
    {
        if (_connected)
            return true;

        try
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/EasyAppDev.Blazor.Store/devtools.js");

            await _module.InvokeVoidAsync("initDevTools", storeName);
            _connected = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends an action to Redux DevTools.
    /// </summary>
    /// <param name="actionType">The type of action.</param>
    /// <param name="stateJson">The serialized state.</param>
    public async Task SendActionAsync(string actionType, string stateJson)
    {
        if (!_connected || _module == null)
            return;

        try
        {
            await _module.InvokeVoidAsync("sendToDevTools", actionType, stateJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DevTools send error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore
            }
        }

        GC.SuppressFinalize(this);
    }
}
