using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Windows;
using StartupProfiles.Windows;

namespace StartupProfiles.App.Config;

/// <summary>
/// Drives the configuration window: the sidebar (the base pinned first, then the profiles), the editor for
/// the selected row, and the side panel it is filled from - Windows startup apps (Defaults) and the global
/// library (Created, see <see cref="LibraryPanel"/>). Rows added from either link to a library item.
/// </summary>
public sealed class ConfigViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions PortableJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IProfileStore _profiles;
    private readonly IBaseStore _base;
    private readonly IStartupAppCatalog _startupCatalog;
    private readonly ProfileExecutor _executor;
    private readonly IUserPrompts _prompts;
    private readonly ISaveScheduler _saves;
    private readonly IConfigStore? _config;

    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _undoRemoveCommand;
    private readonly RelayCommand _browseTargetCommand;
    private readonly RelayCommand _deleteCommand;
    private readonly RelayCommand _runCommand;
    private readonly RelayCommand _pickIconCommand;
    private readonly RelayCommand _addActionCommand;
    private readonly RelayCommand _removeActionCommand;
    private readonly RelayCommand _moveUpCommand;
    private readonly RelayCommand _moveDownCommand;

    private ProfileListItem? _selected;
    private ProfileEditor? _editor;
    private ActionEditor? _selectedAction;
    private string? _status;
    private string? _saveState;
    private RemovedRow? _lastRemoved;

    /// <param name="saveScheduler">When edits are written; saves at once when omitted (tests).</param>
    /// <param name="config">App settings shown on the Settings tab; those settings are hidden when omitted.</param>
    public ConfigViewModel(
        IProfileStore profiles,
        IBaseStore baseStore,
        LibraryService library,
        IStartupAppCatalog startupCatalog,
        ProfileExecutor executor,
        IUserPrompts prompts,
        ISaveScheduler? saveScheduler = null,
        IConfigStore? config = null)
    {
        _profiles = profiles;
        _base = baseStore;
        _startupCatalog = startupCatalog;
        _executor = executor;
        _prompts = prompts;
        _saves = saveScheduler ?? new ImmediateSaveScheduler();
        _config = config;

        Library = new LibraryPanel(library, prompts);
        Library.ItemSaved += RelinkRows;
        Library.ItemRemoved += UnlinkRows;

        NewCommand = new RelayCommand(_ => NewProfile());
        RefreshStartupAppsCommand = new RelayCommand(_ => LoadStartupApps());
        ExportCommand = new RelayCommand(_ => Export());
        ImportCommand = new RelayCommand(_ => Import());
        _saveCommand = new RelayCommand(_ => Save(), _ => Editor is not null);
        _deleteCommand = new RelayCommand(_ => Delete(), _ => Editor is { IsBase: false });
        _runCommand = new RelayCommand(_ => _ = RunAsync(), _ => Editor is not null);
        _pickIconCommand = new RelayCommand(_ => PickIcon(), _ => Editor is { IsBase: false });
        _addActionCommand = new RelayCommand(_ => AddAction(), _ => Editor is not null);
        _removeActionCommand = new RelayCommand(p => RemoveAction(p as ActionEditor), p => p is ActionEditor || SelectedAction is not null);
        _undoRemoveCommand = new RelayCommand(_ => UndoRemove(), _ => CanUndoRemove);
        _browseTargetCommand = new RelayCommand(p => BrowseTarget(p as ActionEditor ?? SelectedAction));
        _moveUpCommand = new RelayCommand(_ => Move(-1), _ => SelectedAction is not null);
        _moveDownCommand = new RelayCommand(_ => Move(1), _ => SelectedAction is not null);

        LoadList();
        LoadStartupApps();
    }

    /// <summary>Sidebar rows: the base first (<see cref="ProfileListItem.IsBase"/>), then every profile.</summary>
    public ObservableCollection<ProfileListItem> Profiles { get; } = [];

    /// <summary>The apps Windows has registered to start at login (on or off), except Startup Profiles itself.</summary>
    public ObservableCollection<StartupAppItem> StartupApps { get; } = [];

    /// <summary>The global library (the side panel's Created tab).</summary>
    public LibraryPanel Library { get; }

    public ProfileListItem? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) LoadEditor(); }
    }

    public ProfileEditor? Editor
    {
        get => _editor;
        private set
        {
            if (ReferenceEquals(_editor, value)) return;
            if (_editor is not null) _editor.Changed -= OnEditorEdited;
            SetProperty(ref _editor, value);
            if (value is not null) value.Changed += OnEditorEdited;
            SaveState = null;
            OnPropertyChanged(nameof(HasEditor));
            OnPropertyChanged(nameof(CanUndoRemove));
            _undoRemoveCommand.NotifyCanExecuteChanged();
            _saveCommand.NotifyCanExecuteChanged();
            _deleteCommand.NotifyCanExecuteChanged();
            _runCommand.NotifyCanExecuteChanged();
            _pickIconCommand.NotifyCanExecuteChanged();
            _addActionCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasEditor => Editor is not null;

    public ActionEditor? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (!SetProperty(ref _selectedAction, value)) return;
            _removeActionCommand.NotifyCanExecuteChanged();
            _moveUpCommand.NotifyCanExecuteChanged();
            _moveDownCommand.NotifyCanExecuteChanged();
        }
    }

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Whether the open profile's edits are on disk ("All changes saved"), or why they are not.</summary>
    public string? SaveState { get => _saveState; private set => SetProperty(ref _saveState, value); }

    /// <summary>True right after a row was removed from the open profile, until another row is removed or it is reopened.</summary>
    public bool CanUndoRemove => _lastRemoved is { } removed && ReferenceEquals(removed.Editor, Editor);

    /// <summary>Whether the app looks for new Windows startup apps when it starts (on unless turned off).</summary>
    public bool DiscoverStartupApps
    {
        get => !string.Equals(_config?.GetValue(AppSettingKeys.DiscoverStartupApps), "false", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (_config is null || value == DiscoverStartupApps) return;
            _config.SetValue(AppSettingKeys.DiscoverStartupApps, value ? null : "false");
            OnPropertyChanged();
        }
    }

    public ICommand NewCommand { get; }
    public ICommand SaveCommand => _saveCommand;
    public ICommand DeleteCommand => _deleteCommand;
    public ICommand RunCommand => _runCommand;
    public ICommand PickIconCommand => _pickIconCommand;
    public ICommand AddActionCommand => _addActionCommand;
    public ICommand RemoveActionCommand => _removeActionCommand;
    public ICommand UndoRemoveCommand => _undoRemoveCommand;
    public ICommand BrowseTargetCommand => _browseTargetCommand;
    public ICommand MoveUpCommand => _moveUpCommand;
    public ICommand MoveDownCommand => _moveDownCommand;
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RefreshStartupAppsCommand { get; }

    /// <summary>
    /// Raised when what the side panel offers may have changed: another profile was selected, "Include base" was
    /// toggled, or the base or a library item changed.
    /// </summary>
    public event Action? PanelFilterChanged;

    /// <summary>
    /// Whether the side panel offers <paramref name="item"/> (a <see cref="StartupAppItem"/> or
    /// <see cref="LibraryItemRow"/>). While a profile that includes the base is being edited, anything the base
    /// already starts is hidden - it runs with that profile anyway.
    /// </summary>
    public bool IsOfferedInPanel(object item) => item switch
    {
        LibraryItemRow row when row.Id is { } id => !BaseCovers(id, row.ToItem().ApplyTo(new ProfileAction { Type = row.Type })),
        StartupAppItem app => !BaseCovers(null, app.Launch),
        _ => true,
    };

    /// <summary>
    /// Adds a Windows startup app to the profile or base being edited, at <paramref name="index"/> (the row it
    /// was dropped on) or at the end, and saves. The app goes into the library first, so the row links to it.
    /// </summary>
    public void AddStartupApp(StartupAppItem app, int? index = null)
    {
        if (Editor is null) return;
        if (Insert(Library.Keep(app.Name, app.Launch), index)) SaveEdits();
    }

    /// <summary>Adds a library item (dragged or double-clicked from the Created tab) as a linked row, and saves.</summary>
    public void AddLibraryRow(LibraryItemRow row, int? index = null)
    {
        if (row.Id is { } id && Library.Find(id) is { } item) AddLibraryItem(item, index);
    }

    /// <summary>Adds a row linked to <paramref name="item"/> at <paramref name="index"/> or at the end, and saves.</summary>
    public void AddLibraryItem(LibraryItem item, int? index = null)
    {
        if (Insert(item, index)) SaveEdits();
    }

    /// <summary>Adds files or folders dropped from Explorer: each goes into the library and is linked here.</summary>
    public void AddFiles(IEnumerable<string> paths, int? index = null)
    {
        if (Editor is null) return;

        var added = false;
        foreach (var path in paths)
        {
            var action = Startables.ActionForPath(path);
            added |= Insert(Library.Keep(Startables.NameFor(action), action), index);
            if (index is not null && SelectedAction is not null) index = Editor.Actions.IndexOf(SelectedAction) + 1;
        }

        if (added) SaveEdits();
    }

    /// <summary>Moves a row dragged within the table so it lands above the row at <paramref name="index"/> (or last), and saves.</summary>
    public void MoveAction(ActionEditor action, int? index)
    {
        if (Editor is null) return;
        var from = Editor.Actions.IndexOf(action);
        if (from < 0) return;

        var target = index is >= 0 and var i && i < Editor.Actions.Count ? i : Editor.Actions.Count;
        var to = target > from ? target - 1 : target;
        SelectedAction = action;
        if (to == from) return;

        Editor.Actions.Move(from, to);
        SaveEdits("Moved.");
    }

    /// <summary>
    /// Adds what was dropped on a sidebar row - a panel item, files from Explorer, or a table row - to that profile
    /// or the base without opening it, and saves it. Dropping on the open profile behaves like dropping on its table.
    /// </summary>
    public void DropOnProfile(ProfileListItem target, object payload)
    {
        if (Editor is not null && target.IsBase == Editor.IsBase && target.Id == Editor.Id)
        {
            switch (payload)
            {
                case StartupAppItem app: AddStartupApp(app); break;
                case LibraryItemRow row: AddLibraryRow(row); break;
                case string[] files: AddFiles(files); break;
                case ActionEditor: Status = "That row is already in this profile."; break;
            }
            return;
        }

        var incoming = payload switch
        {
            StartupAppItem app => [Link(Library.Keep(app.Name, app.Launch))],
            LibraryItemRow { Id: { } id } when Library.Find(id) is { } item => [Link(item)],
            string[] files => files.Select(path =>
            {
                var action = Startables.ActionForPath(path);
                return Link(Library.Keep(Startables.NameFor(action), action));
            }).ToList(),
            ActionEditor row => [row.ToAction()],
            _ => new List<ProfileAction>(),
        };
        if (incoming.Count == 0) return;

        var profile = target.IsBase ? null : _profiles.Find(target.Id);
        if (!target.IsBase && profile is null) return;

        var existing = target.IsBase ? _base.Load().Actions : profile!.Actions;
        var checksBase = profile is { IncludeBase: true };
        var added = new List<ProfileAction>();
        var skipped = new List<string>();
        foreach (var action in incoming)
        {
            if (Contains(existing.Concat(added), action) || (checksBase && BaseCovers(action.LibraryItemId, action)))
                skipped.Add(NameOf(action));
            else
                added.Add(action);
        }

        if (added.Count > 0)
        {
            var actions = (IReadOnlyList<ProfileAction>)[.. existing, .. added];
            if (target.IsBase) _base.Save(_base.Load() with { Actions = actions });
            else _profiles.Save(profile! with { Actions = actions });
            target.ActionCount = actions.Count;
            if (target.IsBase) RefreshPanelFilter();
        }

        Status = (added.Count, skipped.Count) switch
        {
            ( > 0, 0) => $"Added {string.Join(", ", added.Select(NameOf))} to {target.Name} and saved.",
            ( > 0, _) => $"Added {string.Join(", ", added.Select(NameOf))} to {target.Name}; {string.Join(", ", skipped)} already there or in Base.",
            _ => $"{string.Join(", ", skipped)} is already in {target.Name} or starts from Base.",
        };
    }

    /// <summary>
    /// Saves a table row dragged into the Created tab to the library (reusing an item that starts the same thing),
    /// links the row to it, and saves the profile.
    /// </summary>
    public void SaveActionToLibrary(ActionEditor action)
    {
        if (action.IsLinked && action.LibraryItemId is { } id)
        {
            Library.Selected = Library.Items.FirstOrDefault(r => r.Id == id);
            return;
        }

        if (string.IsNullOrWhiteSpace(action.Target))
        {
            Library.Status = "That row has no target to save.";
            return;
        }

        var snapshot = action.ToAction();
        var item = Library.Keep(Startables.NameFor(snapshot), snapshot);
        action.LinkTo(item);
        Library.Status = $"Saved '{item.Name}'. The row now links to it.";
        if (Editor?.Actions.Contains(action) == true) SaveEdits("Row linked to the library and saved.");
    }

    /// <summary>Saves a Windows startup app dragged into the Created tab to the library.</summary>
    public void SaveStartupAppToLibrary(StartupAppItem app)
    {
        var item = Library.Keep(app.Name, app.Launch);
        Library.Status = $"Saved '{item.Name}'.";
    }

    /// <summary>Saves files or folders dropped from Explorer into the Created tab to the library.</summary>
    public void SaveFilesToLibrary(IEnumerable<string> paths)
    {
        var saved = paths.Select(path =>
        {
            var action = Startables.ActionForPath(path);
            return Library.Keep(Startables.NameFor(action), action).Name;
        }).ToList();
        if (saved.Count > 0) Library.Status = $"Saved {string.Join(", ", saved)}.";
    }

    /// <summary>
    /// Inserts a row linked to <paramref name="item"/>. Returns false when nothing was added: a row already starts
    /// it (that row is linked and selected instead), or the base already starts it for this profile.
    /// </summary>
    private bool Insert(LibraryItem item, int? index)
    {
        if (Editor is null) return false;

        if (Editor.Actions.FirstOrDefault(a => a.LibraryItemId == item.Id || (!a.IsLinked && item.Starts(a.ToAction()))) is { } existing)
        {
            existing.LinkTo(item);
            SelectedAction = existing;
            Status = $"'{item.Name}' is already here.";
            return false;
        }

        if (BaseCovers(item.Id, Link(item)))
        {
            Status = $"'{item.Name}' already starts from Base with this profile.";
            return false;
        }

        var action = new ActionEditor();
        action.LinkTo(item);
        Editor.Actions.Insert(index is >= 0 and var i && i <= Editor.Actions.Count ? i : Editor.Actions.Count, action);
        SelectedAction = action;
        Status = $"Added '{item.Name}'.";
        return true;
    }

    /// <summary>Saves the open profile or base after a drag-and-drop change, so drops never need a separate Save.</summary>
    private void SaveEdits(string? status = null)
    {
        var detail = status ?? Status;
        Save();
        Status = detail is null ? "Saved." : $"{detail.TrimEnd('.')} - saved.";
    }

    /// <summary>
    /// True while a profile that includes the base is being edited and the base already starts
    /// <paramref name="action"/> (linked to <paramref name="itemId"/>, or starting the same target).
    /// </summary>
    private bool BaseCovers(string? itemId, ProfileAction action) =>
        Editor is { IsBase: false, IncludeBase: true } && BaseStarts(itemId, action);

    private bool BaseStarts(string? itemId, ProfileAction action) =>
        _base.Load().Actions.Any(b =>
            (itemId is not null && b.LibraryItemId == itemId) || SameStart(Resolve(b), action));

    private ProfileAction Resolve(ProfileAction action) =>
        action.LibraryItemId is { } id && Library.Find(id) is { } item ? item.ApplyTo(action) : action;

    private bool Contains(IEnumerable<ProfileAction> actions, ProfileAction action) =>
        actions.Any(a => (action.LibraryItemId is not null && a.LibraryItemId == action.LibraryItemId) ||
                         SameStart(Resolve(a), Resolve(action)));

    private static bool SameStart(ProfileAction a, ProfileAction b) =>
        a.Type == b.Type &&
        string.Equals(a.Target, b.Target, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Arguments ?? "", b.Arguments ?? "", StringComparison.OrdinalIgnoreCase);

    private static ProfileAction Link(LibraryItem item) => item.ApplyTo(new ProfileAction { Type = item.Type });

    private string NameOf(ProfileAction action) =>
        action.LibraryItemId is { } id && Library.Find(id) is { } item ? item.Name : Startables.NameFor(action);

    private void RefreshPanelFilter() => PanelFilterChanged?.Invoke();

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileEditor.IncludeBase)) RefreshPanelFilter();
    }

    private void RelinkRows(LibraryItem item)
    {
        foreach (var row in Editor?.Actions.Where(a => a.LibraryItemId == item.Id) ?? []) row.LinkTo(item);
        RefreshPanelFilter();
    }

    private void UnlinkRows(string id)
    {
        foreach (var row in Editor?.Actions.Where(a => a.LibraryItemId == id) ?? []) row.Unlink();
        RefreshPanelFilter();
    }

    private void LoadStartupApps()
    {
        StartupApps.Clear();
        var apps = _startupCatalog.GetEntries()
            .Where(e => e.Launch is not null && !IsOwnEntry(e))
            .Select(e => StartupAppItem.From(e, e.Launch!))
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var app in apps) StartupApps.Add(app);
    }

    private static bool IsOwnEntry(StartupEntry entry) =>
        entry.Source == StartupEntrySource.UserRunKey &&
        string.Equals(entry.Key, WindowsStartupRegistration.DefaultValueName, StringComparison.OrdinalIgnoreCase);

    private void LoadList()
    {
        Profiles.Clear();
        Profiles.Add(new ProfileListItem(ProfileExecutor.BaseRunId, "Base", _base.Load().Actions.Count, isBase: true));
        foreach (var profile in _profiles.GetAll())
            Profiles.Add(new ProfileListItem(profile.Id, profile.Name, profile.Actions.Count, icon: profile.Icon));
    }

    /// <summary>Writes any edit still waiting to be saved (the window calls this when it closes).</summary>
    public void FlushPendingSave() => _saves.Flush();

    private void OnEditorEdited()
    {
        if (Editor is not { } editor) return;
        SaveState = "Saving...";
        _saves.Schedule(() => Persist(editor));
    }

    private void LoadEditor()
    {
        _saves.Flush();
        if (Editor is not null) Editor.PropertyChanged -= OnEditorChanged;
        Editor = Selected switch
        {
            { IsBase: true } => ProfileEditor.FromBase(_base.Load(), Library.Find),
            { } item when _profiles.Find(item.Id) is { } profile => ProfileEditor.FromProfile(profile, Library.Find),
            _ => null,
        };
        if (Editor is not null) Editor.PropertyChanged += OnEditorChanged;
        SelectedAction = null;
        Status = null;
        RefreshPanelFilter();
    }

    private void NewProfile()
    {
        var name = _prompts.AskText("New profile", "Profile name");
        if (string.IsNullOrWhiteSpace(name)) return;

        var id = Slug(name);
        if (_profiles.Find(id) is not null)
        {
            _prompts.Info($"A profile with id '{id}' already exists.");
            return;
        }

        _profiles.Save(new Profile { Id = id, Name = name });
        var item = new ProfileListItem(id, name, 0);
        Profiles.Add(item);
        Selected = item;
    }

    private void Save()
    {
        if (Editor is null) return;
        _saves.Cancel();
        if (Persist(Editor)) Status = "Saved.";
    }

    /// <summary>Writes <paramref name="editor"/> to its store and refreshes its sidebar row. False when it cannot be saved yet.</summary>
    private bool Persist(ProfileEditor editor)
    {
        if (editor.IsProfile && string.IsNullOrWhiteSpace(editor.Name))
        {
            if (ReferenceEquals(editor, Editor)) SaveState = "Give the profile a name to save it.";
            return false;
        }

        if (editor.IsBase) _base.Save(editor.ToBase());
        else _profiles.Save(editor.ToProfile());

        if (FindListItem(editor) is { } item)
        {
            item.Name = editor.Name.Trim();
            item.Icon = string.IsNullOrEmpty(editor.Icon) ? null : editor.Icon;
            item.ActionCount = editor.Actions.Count;
        }

        if (ReferenceEquals(editor, Editor)) SaveState = "All changes saved";
        if (editor.IsBase) RefreshPanelFilter();
        return true;
    }

    private void Delete()
    {
        if (Editor is not { IsBase: false }) return;
        if (!_prompts.Confirm($"Delete profile '{Editor.Name}'? This cannot be undone.")) return;

        _saves.Cancel();
        _profiles.Remove(Editor.Id);
        if (FindListItem(Editor) is { } item) Profiles.Remove(item);
        Selected = null;
    }

    private ProfileListItem? FindListItem(ProfileEditor editor) =>
        Profiles.FirstOrDefault(p => p.IsBase == editor.IsBase && p.Id == editor.Id);

    private void PickIcon()
    {
        if (Editor is not { IsBase: false }) return;
        var picked = _prompts.PickIcon(Editor.Icon);
        if (picked is not null) Editor.Icon = picked; // empty string clears the icon; null means cancelled
    }

    private async Task RunAsync()
    {
        if (Editor is null) return;

        Task<ProfileRun> running;
        if (Editor.IsBase) running = _executor.RunBaseAndRecordAsync();
        else if (_profiles.Find(Editor.Id) is { } profile) running = _executor.RunAndRecordAsync(profile);
        else return;

        Status = "Running...";
        var run = await running;
        Status = run.Succeeded ? "Run finished." : "Run finished with failures.";
    }

    private void AddAction()
    {
        if (Editor is null) return;
        var action = new ActionEditor();
        Editor.Actions.Add(action);
        SelectedAction = action;
    }

    /// <summary>Removes <paramref name="row"/> (or the selected row) and saves; <see cref="UndoRemoveCommand"/> puts it back.</summary>
    private void RemoveAction(ActionEditor? row)
    {
        row ??= SelectedAction;
        if (Editor is null || row is null) return;
        var index = Editor.Actions.IndexOf(row);
        if (index < 0) return;

        Editor.Actions.RemoveAt(index);
        if (ReferenceEquals(SelectedAction, row)) SelectedAction = null;
        SetLastRemoved(new RemovedRow(Editor, row, index));
        Status = $"Removed '{row.DisplayName}'.";
    }

    private void UndoRemove()
    {
        if (!CanUndoRemove || _lastRemoved is not { } removed) return;
        removed.Editor.Actions.Insert(Math.Min(removed.Index, removed.Editor.Actions.Count), removed.Row);
        SelectedAction = removed.Row;
        SetLastRemoved(null);
        Status = $"Put '{removed.Row.DisplayName}' back.";
    }

    private void SetLastRemoved(RemovedRow? removed)
    {
        _lastRemoved = removed;
        OnPropertyChanged(nameof(CanUndoRemove));
        _undoRemoveCommand.NotifyCanExecuteChanged();
    }

    private void BrowseTarget(ActionEditor? row)
    {
        if (row is not { CanBrowse: true }) return;
        if (_prompts.PickTarget(row.Type, row.Target) is { } path) row.Target = path;
    }

    private void Move(int delta)
    {
        if (Editor is null || SelectedAction is null) return;
        var index = Editor.Actions.IndexOf(SelectedAction);
        var target = index + delta;
        if (target < 0 || target >= Editor.Actions.Count) return;
        Editor.Actions.Move(index, target);
        SaveEdits("Moved.");
    }

    private void Export()
    {
        var path = _prompts.PickSavePath("startup-profiles.json");
        if (path is null) return;
        File.WriteAllText(path, JsonSerializer.Serialize(_profiles.GetAll(), PortableJson));
        Status = "Exported.";
    }

    private void Import()
    {
        var path = _prompts.PickOpenPath();
        if (path is null) return;
        _saves.Flush();

        var imported = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(path), PortableJson);
        if (imported is null) return;

        foreach (var profile in imported) _profiles.Save(profile);
        LoadList();
        Editor = null;
        _selected = null;
        OnPropertyChanged(nameof(Selected));
        Status = $"Imported {imported.Count} profile(s).";
    }

    private static string Slug(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Length == 0 ? "profile" : slug;
    }

    /// <summary>The last row removed, kept so it can be put back where it was.</summary>
    private sealed record RemovedRow(ProfileEditor Editor, ActionEditor Row, int Index);
}
