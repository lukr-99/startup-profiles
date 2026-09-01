using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Config;

/// <summary>Drives the configuration window: profile list plus the editor for the selected profile.</summary>
public sealed class ConfigViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions PortableJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IProfileStore _profiles;
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

    public ConfigViewModel(IProfileStore profiles, ProfileExecutor executor, IUserPrompts prompts)
    {
        _profiles = profiles;
        _executor = executor;
        _prompts = prompts;

        NewCommand = new RelayCommand(_ => NewProfile());
        ExportCommand = new RelayCommand(_ => Export());
        ImportCommand = new RelayCommand(_ => Import());
        _saveCommand = new RelayCommand(_ => Save(), _ => Editor is not null);
        _deleteCommand = new RelayCommand(_ => Delete(), _ => Editor is not null);
        _runCommand = new RelayCommand(_ => _ = RunAsync(), _ => Editor is not null);
        _pickIconCommand = new RelayCommand(_ => PickIcon(), _ => Editor is not null);
        _addActionCommand = new RelayCommand(_ => AddAction(), _ => Editor is not null);
        _removeActionCommand = new RelayCommand(_ => RemoveAction(), _ => SelectedAction is not null);
        _moveUpCommand = new RelayCommand(_ => Move(-1), _ => SelectedAction is not null);
        _moveDownCommand = new RelayCommand(_ => Move(1), _ => SelectedAction is not null);

        LoadList();
    }

    public ObservableCollection<ProfileListItem> Profiles { get; } = [];

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

    private void LoadList()
    {
        Profiles.Clear();
        foreach (var profile in _profiles.GetAll())
            Profiles.Add(new ProfileListItem(profile.Id, profile.Name, profile.Actions.Count));
    }

    private void LoadEditor()
    {
        Editor = Selected is not null && _profiles.Find(Selected.Id) is { } profile
            ? ProfileEditor.FromProfile(profile)
            : null;
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
        _profiles.Save(Editor.ToProfile());
        if (Profiles.FirstOrDefault(p => p.Id == Editor.Id) is { } item)
        {
            item.Name = Editor.Name;
            item.ActionCount = Editor.Actions.Count;
        }
        Status = "Saved.";
    }

    private void Delete()
    {
        if (Editor is null) return;
        if (!_prompts.Confirm($"Delete profile '{Editor.Name}'?")) return;

        _profiles.Remove(Editor.Id);
        if (Profiles.FirstOrDefault(p => p.Id == Editor.Id) is { } item) Profiles.Remove(item);
        Selected = null;
    }

    private void PickIcon()
    {
        if (Editor is null) return;
        var picked = _prompts.PickIcon(Editor.Icon);
        if (picked is not null) Editor.Icon = picked; // empty string clears the icon; null means cancelled
    }

    private async Task RunAsync()
    {
        if (Editor is null || _profiles.Find(Editor.Id) is not { } profile) return;
        Status = "Running...";
        var run = await _executor.RunAndRecordAsync(profile);
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
