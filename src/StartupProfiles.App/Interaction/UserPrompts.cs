using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StartupProfiles.App.Themes;
using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

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

    public string? PickIcon(string? current)
    {
        string? picked = null;
        Window dialog = null!;
        const int columns = 12;

        var emojiStyle = Application.Current.TryFindResource("App.EmojiButton") as Style;
        var groups = new StackPanel();
        foreach (var (group, icons) in IconCatalog.Groups)
        {
            var label = new TextBlock { Text = group, FontSize = 12, Margin = new Thickness(4, 12, 0, 4) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "App.Muted");
            groups.Children.Add(label);

            var row = new WrapPanel { MaxWidth = columns * 46 };
            foreach (var icon in icons)
            {
                var button = new Button { Content = icon, ToolTip = group };
                if (emojiStyle is not null) button.Style = emojiStyle;

                // The current icon is ringed so the user sees what they are changing from.
                if (icon == current?.Trim())
                {
                    button.BorderThickness = new Thickness(2);
                    button.SetResourceReference(Control.BorderBrushProperty, "App.Accent");
                    button.Loaded += (_, _) => button.Focus();
                }

                var captured = icon;
                button.Click += (_, _) => { picked = captured; dialog.DialogResult = true; };
                row.Children.Add(button);
            }
            groups.Children.Add(row);
        }

        var panel = NewPanel();
        panel.Children.Add(new TextBlock { Text = "Choose an icon", FontWeight = FontWeights.SemiBold, FontSize = 16 });
        var hint = new TextBlock { Text = "It shows on the launcher and in the profile list.", Margin = new Thickness(0, 4, 0, 0) };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "App.Muted");
        panel.Children.Add(hint);
        panel.Children.Add(new ScrollViewer
        {
            Content = groups,
            MaxHeight = SystemParameters.WorkArea.Height * 0.6,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(-4, 0, 0, 0),
        });

        var clear = SecondaryButton("Use the first letter", isCancel: false);
        clear.Margin = new Thickness(0, 16, 8, 0);
        clear.Click += (_, _) => { picked = ""; dialog.DialogResult = true; };
        var cancel = SecondaryButton("Cancel", isCancel: true);
        panel.Children.Add(ButtonRow(clear, cancel));

        dialog = CreateDialog("Choose icon", panel);
        return dialog.ShowDialog() == true ? picked : null;
    }

    public string? PickTarget(ActionType type, string? current)
    {
        var start = StartFolder(current);
        if (type == ActionType.OpenFolder)
        {
            var folder = new OpenFolderDialog { Title = "Choose a folder to open", InitialDirectory = start };
            return folder.ShowDialog() == true ? folder.FolderName : null;
        }

        var file = new OpenFileDialog
        {
            Title = type == ActionType.LaunchApp ? "Choose a program to start" : "Choose a file to open",
            Filter = type == ActionType.LaunchApp
                ? "Programs and shortcuts (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files (*.*)|*.*"
                : "All files (*.*)|*.*",
            InitialDirectory = start,
            DereferenceLinks = false,
        };
        return file.ShowDialog() == true ? file.FileName : null;
    }

    private static string? StartFolder(string? current)
    {
        if (string.IsNullOrWhiteSpace(current)) return null;
        try
        {
            var path = Environment.ExpandEnvironmentVariables(current.Trim().Trim('"'));
            if (Directory.Exists(path)) return path;
            var parent = Path.GetDirectoryName(path);
            return parent is not null && Directory.Exists(parent) ? parent : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
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
