using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using ThemeMode = StartupProfiles.App.Themes.ThemeMode;

namespace StartupProfiles.App.Config;

/// <summary>
/// The configuration window. Drag and drop is view-only plumbing here; every change it makes goes through the
/// <see cref="ConfigViewModel"/>. Drag sources: the Defaults list, the Created list, and the grip of an action row.
/// Drop targets: the actions table (add or reorder) and the Created list (save to the library). Files from
/// Explorer are accepted by both targets.
/// </summary>
public partial class ConfigWindow : Window
{
    private const string StartupAppFormat = "StartupProfiles.StartupApp";
    private const string LibraryItemFormat = "StartupProfiles.LibraryItem";
    private const string ActionRowFormat = "StartupProfiles.ActionRow";

    // Columns whose values come from the library item on a linked row.
    private static readonly HashSet<string> LinkedColumns = ["Type", "Target", "Arguments", "Admin"];

    private readonly ConfigViewModel _viewModel;
    private readonly Action<ThemeMode> _applyTheme;
    private bool _ready;
    private Point _dragStart;
    private DataObject? _pendingDrag;

    public ConfigWindow(ConfigViewModel viewModel, ThemeMode current, Action<ThemeMode> applyTheme)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _applyTheme = applyTheme;
        ThemeBox.SelectedIndex = (int)current;
        var version = typeof(ConfigWindow).Assembly.GetName().Version;
        VersionText.Text = version is null ? "Startup Profiles" : $"Startup Profiles {version.Major}.{version.Minor}.{version.Build}";

        // The side panel hides what Base already starts while a profile is being edited.
        var startupApps = CollectionViewSource.GetDefaultView(viewModel.StartupApps);
        var libraryItems = CollectionViewSource.GetDefaultView(viewModel.Library.Items);
        startupApps.Filter = viewModel.IsOfferedInPanel;
        libraryItems.Filter = viewModel.IsOfferedInPanel;
        viewModel.PanelFilterChanged += () =>
        {
            startupApps.Refresh();
            libraryItems.Refresh();
        };

        _ready = true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Themes.ThemeManager.ApplyTitleBar(this);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) _applyTheme((ThemeMode)ThemeBox.SelectedIndex);
    }

    // ----- Drag sources -----

    private void OnStartupAppsMouseDown(object sender, MouseButtonEventArgs e) =>
        ArmDrag(e, StartupAppFormat, ItemUnder<ListBoxItem>(e.OriginalSource)?.DataContext as StartupAppItem);

    private void OnLibraryMouseDown(object sender, MouseButtonEventArgs e) =>
        ArmDrag(e, LibraryItemFormat, ItemUnder<ListBoxItem>(e.OriginalSource)?.DataContext as LibraryItemRow);

    // Rows drag only from their grip, so clicking cells still selects and edits them.
    private void OnActionsMouseDown(object sender, MouseButtonEventArgs e) =>
        ArmDrag(e, ActionRowFormat, HandleUnder(e.OriginalSource) is { DataContext: ActionEditor row } ? row : null);

    private void ArmDrag(MouseButtonEventArgs e, string format, object? payload)
    {
        _pendingDrag = payload is null ? null : new DataObject(format, payload);
        _dragStart = e.GetPosition(this);
    }

    private void OnDragSourceMouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingDrag is null || e.LeftButton != MouseButtonState.Pressed) return;

        var moved = _dragStart - e.GetPosition(this);
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var data = _pendingDrag;
        _pendingDrag = null;
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
    }

    private void OnStartupAppsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder<ListBoxItem>(e.OriginalSource)?.DataContext is not StartupAppItem app) return;
        CommitGridEdits();
        _viewModel.AddStartupApp(app);
        ScrollToSelectedAction();
    }

    private void OnLibraryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder<ListBoxItem>(e.OriginalSource)?.DataContext is not LibraryItemRow row) return;
        CommitGridEdits();
        _viewModel.AddLibraryRow(row);
        ScrollToSelectedAction();
    }

    // ----- Actions table: add from the panel or Explorer, or reorder its own rows -----

    private void OnActionsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_viewModel.HasEditor ? DragDropEffects.None
            : e.Data.GetData(ActionRowFormat) is ActionEditor row ? (_viewModel.Editor!.Actions.Contains(row) ? DragDropEffects.Move : DragDropEffects.None)
            : e.Data.GetDataPresent(StartupAppFormat) || e.Data.GetDataPresent(LibraryItemFormat) || e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link
            : DragDropEffects.None;

        if (e.Effects == DragDropEffects.None) ActionsGrid.ClearValue(BorderBrushProperty);
        else ActionsGrid.SetResourceReference(BorderBrushProperty, "App.Accent");
        e.Handled = true;
    }

    private void OnActionsDragLeave(object sender, DragEventArgs e) => ActionsGrid.ClearValue(BorderBrushProperty);

    private void OnActionsDrop(object sender, DragEventArgs e)
    {
        ActionsGrid.ClearValue(BorderBrushProperty);
        e.Handled = true;

        // Dropped on a row: land above it. Dropped on the header or empty space: go last.
        var index = ItemUnder<DataGridRow>(e.OriginalSource)?.GetIndex();
        CommitGridEdits();

        switch (Payload(e))
        {
            case ActionEditor row:
                _viewModel.MoveAction(row, index);
                break;
            case StartupAppItem app:
                _viewModel.AddStartupApp(app, index);
                break;
            case LibraryItemRow item:
                _viewModel.AddLibraryRow(item, index);
                break;
            case string[] files:
                _viewModel.AddFiles(files, index);
                break;
        }

        ScrollToSelectedAction();
    }

    private void OnActionsBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is ActionEditor { IsLinked: true } && e.Column.Header is string header && LinkedColumns.Contains(header))
        {
            e.Cancel = true;
            _viewModel.Library.Status = "That row links to the library. Change what it starts in Created.";
        }
    }

    private void OnGridEditorLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }

    // ----- Sidebar: drop onto a profile (or Base) to add there, saved, without opening it -----

    private ProfileListItem? _dropTarget;

    private void OnProfileListDragOver(object sender, DragEventArgs e)
    {
        var target = ItemUnder<ListBoxItem>(e.OriginalSource)?.DataContext as ProfileListItem;
        var accepts = target is not null && Payload(e) is StartupAppItem or LibraryItemRow or ActionEditor or string[];
        SetDropTarget(accepts ? target : null);
        e.Effects = accepts ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnProfileListDragLeave(object sender, DragEventArgs e) => SetDropTarget(null);

    private void OnProfileListDrop(object sender, DragEventArgs e)
    {
        var target = _dropTarget;
        SetDropTarget(null);
        e.Handled = true;
        if (target is null || Payload(e) is not { } payload) return;

        CommitGridEdits();
        _viewModel.DropOnProfile(target, payload);
    }

    private void SetDropTarget(ProfileListItem? target)
    {
        if (ReferenceEquals(_dropTarget, target)) return;
        if (_dropTarget is not null) _dropTarget.IsDropTarget = false;
        _dropTarget = target;
        if (target is not null) target.IsDropTarget = true;
    }

    // ----- Created list: keep table rows, defaults, and Explorer files in the library -----

    private void OnLibraryDragOver(object sender, DragEventArgs e)
    {
        var accepts = e.Data.GetDataPresent(ActionRowFormat) || e.Data.GetDataPresent(StartupAppFormat) ||
                      e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;

        if (accepts) LibraryList.SetResourceReference(BackgroundProperty, "App.SurfaceHover");
        else LibraryList.ClearValue(BackgroundProperty);
        e.Handled = true;
    }

    private void OnLibraryDragLeave(object sender, DragEventArgs e) => LibraryList.ClearValue(BackgroundProperty);

    private void OnLibraryDrop(object sender, DragEventArgs e)
    {
        LibraryList.ClearValue(BackgroundProperty);
        e.Handled = true;

        switch (Payload(e))
        {
            case ActionEditor row:
                CommitGridEdits();
                _viewModel.SaveActionToLibrary(row);
                break;
            case StartupAppItem app:
                _viewModel.SaveStartupAppToLibrary(app);
                break;
            case string[] files:
                _viewModel.SaveFilesToLibrary(files);
                break;
        }

        if (LibraryList.SelectedItem is { } selected) LibraryList.ScrollIntoView(selected);
    }

    // ----- Helpers -----

    private static object? Payload(DragEventArgs e) =>
        e.Data.GetData(ActionRowFormat) ?? e.Data.GetData(StartupAppFormat) ?? e.Data.GetData(LibraryItemFormat) ??
        e.Data.GetData(DataFormats.FileDrop);

    // Inserting or moving rows while a cell is being edited throws inside DataGrid, so finish any edit first.
    private void CommitGridEdits() => ActionsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

    private void ScrollToSelectedAction()
    {
        if (_viewModel.SelectedAction is { } action) ActionsGrid.ScrollIntoView(action);
    }

    private static FrameworkElement? HandleUnder(object source)
    {
        for (var node = source as DependencyObject; node is not null; node = ParentOf(node))
        {
            if (node is FrameworkElement { Tag: "DragHandle" } handle) return handle;
            if (node is DataGridRow) return null;
        }

        return null;
    }

    private static T? ItemUnder<T>(object source) where T : DependencyObject
    {
        for (var node = source as DependencyObject; node is not null; node = ParentOf(node))
        {
            if (node is T match) return match;
        }

        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
}
