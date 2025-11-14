namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// Represents available themes.
/// </summary>
public enum Theme
{
    Light,
    Dark,
    Auto
}

/// <summary>
/// State for the theme example - demonstrates selector optimization.
/// Multiple components can subscribe to different parts of this state,
/// and only re-render when their selected portion changes.
/// </summary>
/// <param name="CurrentTheme">The current theme setting.</param>
/// <param name="FontSize">Font size in pixels.</param>
/// <param name="Animations">Whether animations are enabled.</param>
/// <param name="ReducedMotion">Whether reduced motion is enabled.</param>
/// <param name="HighContrast">Whether high contrast mode is enabled.</param>
/// <param name="LastChanged">When the settings were last changed.</param>
public record ThemeState(
    Theme CurrentTheme = Theme.Auto,
    int FontSize = 16,
    bool Animations = true,
    bool ReducedMotion = false,
    bool HighContrast = false,
    DateTime? LastChanged = null)
{
    /// <summary>
    /// Creates the default theme state.
    /// </summary>
    public static ThemeState Default => new();

    /// <summary>
    /// Changes the theme.
    /// </summary>
    public ThemeState SetTheme(Theme theme) => this with
    {
        CurrentTheme = theme,
        LastChanged = DateTime.Now
    };

    /// <summary>
    /// Sets the font size (clamped between 12 and 24).
    /// </summary>
    public ThemeState SetFontSize(int size) => this with
    {
        FontSize = Math.Clamp(size, 12, 24),
        LastChanged = DateTime.Now
    };

    /// <summary>
    /// Increases font size by 2px.
    /// </summary>
    public ThemeState IncreaseFontSize() => SetFontSize(FontSize + 2);

    /// <summary>
    /// Decreases font size by 2px.
    /// </summary>
    public ThemeState DecreaseFontSize() => SetFontSize(FontSize - 2);

    /// <summary>
    /// Toggles animations on/off.
    /// </summary>
    public ThemeState ToggleAnimations() => this with
    {
        Animations = !Animations,
        LastChanged = DateTime.Now
    };

    /// <summary>
    /// Toggles reduced motion on/off.
    /// </summary>
    public ThemeState ToggleReducedMotion() => this with
    {
        ReducedMotion = !ReducedMotion,
        LastChanged = DateTime.Now
    };

    /// <summary>
    /// Toggles high contrast mode on/off.
    /// </summary>
    public ThemeState ToggleHighContrast() => this with
    {
        HighContrast = !HighContrast,
        LastChanged = DateTime.Now
    };

    /// <summary>
    /// Resets all settings to default.
    /// </summary>
    public ThemeState Reset() => Default with { LastChanged = DateTime.Now };

    /// <summary>
    /// Gets the CSS class for the current theme.
    /// </summary>
    public string ThemeClass => CurrentTheme switch
    {
        Theme.Dark => "theme-dark",
        Theme.Light => "theme-light",
        Theme.Auto => "theme-auto",
        _ => "theme-auto"
    };

    /// <summary>
    /// Gets the CSS styles for font size.
    /// </summary>
    public string FontSizeStyle => $"font-size: {FontSize}px;";

    /// <summary>
    /// Gets whether animations should be disabled (based on both settings).
    /// </summary>
    public bool AnimationsDisabled => !Animations || ReducedMotion;
}
