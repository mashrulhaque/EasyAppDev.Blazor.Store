namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Navigation mode for State-to-URL synchronization.
/// Determines how browser history is affected when state changes trigger URL updates.
/// </summary>
public enum UrlSyncNavigationMode
{
    /// <summary>
    /// Replace current history entry (recommended).
    /// Prevents browser history pollution from rapid state updates.
    /// User's back button navigates to previous page, not previous state.
    /// </summary>
    /// <remarks>
    /// Use this mode for:
    /// <list type="bullet">
    /// <item>High-frequency state updates (sliders, filters, pagination)</item>
    /// <item>Intermediate states user doesn't need to revisit</item>
    /// <item>When you want clean browser history</item>
    /// </list>
    /// </remarks>
    Replace,

    /// <summary>
    /// Add new history entry.
    /// Use sparingly - each state change adds to browser history.
    /// User's back button will step through all state changes.
    /// </summary>
    /// <remarks>
    /// Use this mode for:
    /// <list type="bullet">
    /// <item>Intentional navigation steps (wizard pages, tabs)</item>
    /// <item>User actions that should be in history</item>
    /// </list>
    /// Warning: Rapid state updates will pollute browser history.
    /// </remarks>
    Push
}
