namespace StartupProfiles.App.Tray;

/// <summary>
/// One line of the tray menu, free of WPF so the menu's contents are unit-tested. A null
/// <see cref="Header"/> is a separator; <see cref="TrayCommand.None"/> with a header is a plain label; a non-empty
/// <see cref="Children"/> makes a submenu. <see cref="Argument"/> carries a profile id or theme name.
/// </summary>
public sealed record TrayMenuEntry(
    string? Header,
    TrayCommand Command = TrayCommand.None,
    string? Argument = null,
    bool? IsChecked = null,
    bool IsDefault = false,
    bool IsEnabled = true)
{
    public IReadOnlyList<TrayMenuEntry> Children { get; init; } = [];

    public static TrayMenuEntry Separator { get; } = new(Header: null);

    public bool IsSeparator => Header is null;
}
