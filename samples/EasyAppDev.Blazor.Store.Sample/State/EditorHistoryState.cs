namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating undo/redo history functionality.
/// Simulates a simple text editor with formatting options.
/// </summary>
public record EditorHistoryState(
    string Content,
    bool IsBold,
    bool IsItalic,
    string FontSize,
    string TextColor)
{
    public static EditorHistoryState Initial => new(
        "Start typing here...",
        false,
        false,
        "16px",
        "#000000");

    public EditorHistoryState SetContent(string content) =>
        this with { Content = content };

    public EditorHistoryState ToggleBold() =>
        this with { IsBold = !IsBold };

    public EditorHistoryState ToggleItalic() =>
        this with { IsItalic = !IsItalic };

    public EditorHistoryState SetFontSize(string size) =>
        this with { FontSize = size };

    public EditorHistoryState SetTextColor(string color) =>
        this with { TextColor = color };

    public EditorHistoryState Reset() => Initial;
}
