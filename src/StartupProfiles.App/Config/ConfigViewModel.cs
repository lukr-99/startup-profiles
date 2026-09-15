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

    private readonly RelayCommand _saveCommand;
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

    public ConfigViewModel(
        IProfileStore profiles,
        IBaseStore baseStore,
        LibraryService library,
        IStartupAppCatalog startupCatalog,
        ProfileExecutor executor,
        IUserPrompts prompts)
    {
        _profiles = profiles;
        _base = baseStore;
        _startupCatalog = startupCatalog;
        _executor = executor;
        _prompts = prompts;

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
        _removeActionCommand = new RelayCommand(_ => RemoveAction(), _ => SelectedAction is not null);
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
            if (!SetProperty(ref _editor, value)) return;
            OnPropertyChanged(nameof(HasEditor));
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

    public ICommand NewCommand { get; }
    public ICommand SaveCommand => _saveCommand;
    public ICommand DeleteCommand => _deleteCommand;
    public ICommand RunCommand => _runCommand;
    public ICommand PickIconCommand => _pickIconCommand;
    public ICommand AddActionCommand => _addActionCommand;
    public ICommand RemoveActionCommand => _removeActionCommand;
    public ICommand MoveUpCommand => _moveUpCommand;
    public ICommand MoveDownCommand => _moveDownCommand;
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RefreshStartupAppsCommand { get; }

    /// <summary>
    /// Adds a Windows startup app to the profile or base being edited, at <paramref name="index"/> (the row it
    /// was dropped on) or at the end. The app goes into the library first, so the row links to it.
    /// </summary>
    public void AddStartupApp(StartupAppItem app, int? index = null)
    {
        if (Editor is null) return;
        AddLibraryItem(Library.Keep(app.Name, app.Launch), index);
    }

    /// <summary>Adds a library item (dragged from the Created tab) as a linked row.</summary>
    public void AddLibraryRow(LibraryItemRow row, int? index = null)
    {
        if (row.Id is { } id && Library.Find(id) is { } item) AddLibraryItem(item, index);
    }

    /// <summary>
    /// Adds a row linked to <paramref name="item"/> at <paramref name="index"/> or at the end. If a row already
    /// starts the same thing it is linked and selected instead of added twice. Kept once the editor is saved.
    /// </summary>
    public void AddLibraryItem(LibraryItem item, int? index = null)
    {
        if (Editor is null) return;

        if (Editor.Actions.FirstOrDefault(a => a.LibraryItemId == item.Id || (!a.IsLinked && item.Starts(a.ToAction()))) is { } existing)
        {
            existing.LinkTo(item);
            SelectedAction = existing;
            Status = $"'{item.Name}' is already here.";
            return;
        }

        var action = new ActionEditor();
        action.LinkTo(item);
        Editor.Actions.Insert(index is >= 0 and var i && i <= Editor.Actions.Count ? i : Editor.Actions.Count, action);
        SelectedAction = action;
        Status = $"Added '{item.Name}'. Save to keep it.";
    }

    /// <summary>Adds files or folders dropped from Explorer: each goes into the library and is linked here.</summary>
    public void AddFiles(IEnumerable<string> paths, int? index = null)
    {
        if (Editor is null) return;
        foreach (var path in paths)
        {
            var action = Startables.ActionForPath(path);
            AddLibraryItem(Library.Keep(Startables.NameFor(action), action), index);
            if (index is not null) index = Editor.Actions.IndexOf(SelectedAction!) + 1;
        }
    }

    /// <summary>Moves a row dragged within the table so it lands above the row at <paramref name="index"/> (or last).</summary>
    public void MoveAction(ActionEditor action, int? index)
    {
        if (Editor is null) return;
        var from = Editor.Actions.IndexOf(action);
        if (from < 0) return;

        var target = index is >= 0 and var i && i < Editor.Actions.Count ? i : Editor.Actions.Count;
        var to = target > from ? target - 1 : target;
        if (to != from) Editor.Actions.Move(from, to);
        SelectedAction = action;
    }

    /// <summary>
    /// Saves a table row dragged into the Created tab to the library (reusing an item that starts the same thing)
    /// and links the row to it.
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
        if (Editor?.Actions.Contains(action) == true) Status = "Row linked to the library. Save to keep the link.";
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

    private void RelinkRows(LibraryItem item)
    {
        foreach (var row in Editor?.Actions.Where(a => a.LibraryItemId == item.Id) ?? []) row.LinkTo(item);
    }

    private void UnlinkRows(string id)
    {
        foreach (var row in Editor?.Actions.Where(a => a.LibraryItemId == id) ?? []) row.Unlink();
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
            Profiles.Add(new ProfileListItem(profile.Id, profile.Name, profile.Actions.Count));
    }

    private void LoadEditor()
    {
        Editor = Selected switch
        {
            { IsBase: true } => ProfileEditor.FromBase(_base.Load(), Library.Find),
            { } item when _profiles.Find(item.Id) is { } profile => ProfileEditor.FromProfile(profile, Library.Find),
            _ => null,
        };
        SelectedAction = null;
        Status = null;
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
        if (Editor.IsBase) _base.Save(Editor.ToBase());
        else _profiles.Save(Editor.ToProfile());

        if (FindListItem(Editor) is { } item)
        {
            item.Name = Editor.Name;
            item.ActionCount = Editor.Actions.Count;
        }
        Status = "Saved.";
    }

    private void Delete()
    {
        if (Editor is not { IsBase: false }) return;
        if (!_prompts.Confirm($"Delete profile '{Editor.Name}'?")) return;

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

    private void RemoveAction()
    {
        if (Editor is null || SelectedAction is null) return;
        Editor.Actions.Remove(SelectedAction);
        SelectedAction = null;
    }

    private void Move(int delta)
    {
        if (Editor is null || SelectedAction is null) return;
        var index = Editor.Actions.IndexOf(SelectedAction);
        var target = index + delta;
        if (target < 0 || target >= Editor.Actions.Count) return;
        Editor.Actions.Move(index, target);
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
}
