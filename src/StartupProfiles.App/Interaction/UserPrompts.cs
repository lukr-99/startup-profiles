using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Interaction;

/// <summary>
/// WPF implementation of <see cref="IUserPrompts"/>. Uses small theme-aware dialogs (not the native
/// message box, which can't follow the app theme) and the shell file dialogs.
/// </summary>
public sealed class UserPrompts : IUserPrompts
{
    public bool Confirm(string message)
    {
        var panel = NewPanel();
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });

        var ok = PrimaryButton("OK");
        var cancel = SecondaryButton("Cancel", isCancel: true);
        panel.Children.Add(ButtonRow(ok, cancel));

        var dialog = CreateDialog("Startup Profiles", panel);
        ok.Click += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true;
    }

    public void Info(string message)
    {
        var panel = NewPanel();
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });

        var ok = PrimaryButton("OK");
        panel.Children.Add(ButtonRow(ok));

        var dialog = CreateDialog("Startup Profiles", panel);
        ok.Click += (_, _) => dialog.DialogResult = true;
        dialog.ShowDialog();
    }

    public string? AskText(string title, string prompt)
    {
        var box = new TextBox { Margin = new Thickness(0, 8, 0, 0), MinWidth = 300 };
        var panel = NewPanel();
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(box);

        var ok = PrimaryButton("OK");
        var cancel = SecondaryButton("Cancel", isCancel: true);
        panel.Children.Add(ButtonRow(ok, cancel));

        var dialog = CreateDialog(title, panel);
        ok.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => box.Focus();

        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }

    private static readonly string[] Emojis =
    [
        "💼", "🏢", "💻", "⌨️", "🖥️", "🐛", "🧩", "🎓",
        "📚", "✏️", "📝", "🎮", "🕹️", "🎯", "🎲", "🎧",
        "🎵", "🎬", "📺", "🌐", "📧", "💬", "📞", "📅",
        "📁", "🗂️", "🔧", "⚙️", "🚀", "⭐", "✨", "🔥",
        "💡", "🏠", "☕", "🌙", "🎨", "📷", "💰", "📊",
        "🔒", "🛡️", "🩺", "🏋️", "🍕", "🧠", "✅", "🌟",
    ];

    public string? PickIcon(string? current)
    {
        string? picked = null;
        Window dialog = null!;

        var grid = new WrapPanel { MaxWidth = 8 * 52, Margin = new Thickness(0, 10, 0, 0) };
        var emojiStyle = Application.Current.TryFindResource("App.EmojiButton") as Style;
        foreach (var emoji in Emojis)
        {
            var button = new Button { Content = emoji };
            if (emojiStyle is not null) button.Style = emojiStyle;
            var captured = emoji;
            button.Click += (_, _) => { picked = captured; dialog.DialogResult = true; };
            grid.Children.Add(button);
        }

        var panel = NewPanel();
        panel.Children.Add(new TextBlock { Text = "Choose an icon", FontWeight = FontWeights.SemiBold, FontSize = 15 });
        panel.Children.Add(grid);

        var clear = SecondaryButton("Clear icon", isCancel: false);
        clear.Click += (_, _) => { picked = ""; dialog.DialogResult = true; };
        var cancel = SecondaryButton("Cancel", isCancel: true);
        panel.Children.Add(ButtonRow(clear, cancel));

        dialog = CreateDialog("Choose icon", panel);
        return dialog.ShowDialog() == true ? picked : null;
    }

    public string? PickSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog { FileName = suggestedFileName, Filter = "JSON (*.json)|*.json" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOpenPath()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static StackPanel NewPanel() => new() { Margin = new Thickness(18) };

    private static Button PrimaryButton(string content)
    {
        var button = new Button { Content = content, IsDefault = true, MinWidth = 88, Margin = new Thickness(0, 16, 8, 0) };
        if (Application.Current.TryFindResource("App.AccentButton") is Style accent) button.Style = accent;
        return button;
    }

    private static Button SecondaryButton(string content, bool isCancel) => new()
    {
        Content = content,
        IsCancel = isCancel,
        MinWidth = 88,
        Padding = new Thickness(16, 7, 16, 7),
        Margin = new Thickness(0, 16, 0, 0),
    };

    private static StackPanel ButtonRow(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons) row.Children.Add(button);
        return row;
    }

    // Built in code, so a dialog does not inherit the themed window's resources - apply them explicitly.
    private static Window CreateDialog(string title, object content)
    {
        var dialog = new Window
        {
            Title = title,
            Content = content,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
        };
        dialog.SetResourceReference(Control.BackgroundProperty, "App.Background");
        dialog.SetResourceReference(Control.ForegroundProperty, "App.Text");
        dialog.SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(dialog);
        return dialog;
    }
}
