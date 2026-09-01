using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Interaction;

/// <summary>WPF implementation of <see cref="IUserPrompts"/> using message boxes and file dialogs.</summary>
public sealed class UserPrompts : IUserPrompts
{
    public bool Confirm(string message) =>
        MessageBox.Show(message, "Startup Profiles", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            == MessageBoxResult.OK;

    public void Info(string message) =>
        MessageBox.Show(message, "Startup Profiles", MessageBoxButton.OK, MessageBoxImage.Information);

    public string? AskText(string title, string prompt)
    {
        var box = new TextBox { Margin = new Thickness(0, 8, 0, 0), MinWidth = 300 };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(box);

        var ok = new Button { Content = "OK", IsDefault = true, Width = 84, Margin = new Thickness(0, 14, 8, 0) };
        if (Application.Current.TryFindResource("App.AccentButton") is Style accent) ok.Style = accent;
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 84, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(16, 7, 16, 7) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = title,
            Content = panel,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
        };
        // Built in code, so it does not inherit the themed window's resources - apply them explicitly.
        dialog.SetResourceReference(Control.BackgroundProperty, "App.Background");
        dialog.SetResourceReference(Control.ForegroundProperty, "App.Text");
        dialog.SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(dialog);

        ok.Click += (_, _) => { dialog.DialogResult = true; };
        dialog.Loaded += (_, _) => box.Focus();

        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
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
}
