namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating debounce and throttle functionality.
/// </summary>
public record DebounceState(
    string SearchQuery = "",
    int SearchCount = 0,
    int MouseX = 0,
    int MouseY = 0,
    int UpdateCount = 0)
{
    /// <summary>
    /// Sets the search query and increments the search count.
    /// </summary>
    public DebounceState SetQuery(string query) => this with
    {
        SearchQuery = query,
        SearchCount = SearchCount + 1
    };

    /// <summary>
    /// Sets the mouse position and increments the update count.
    /// </summary>
    public DebounceState SetMousePosition(int x, int y) => this with
    {
        MouseX = x,
        MouseY = y,
        UpdateCount = UpdateCount + 1
    };
}
