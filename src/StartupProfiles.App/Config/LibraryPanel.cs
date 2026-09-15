using System.Collections.ObjectModel;
using System.Windows.Input;
using StartupProfiles.App.Interaction;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// The side panel's Created tab: the global library of startable items, plus the form for the selected or new
/// item. Saving or deleting an item raises an event so the open profile editor refreshes the rows linked to it.
/// </summary>
public sealed class LibraryPanel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly IUserPrompts _prompts;
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _deleteCommand;

    private LibraryItemRow? _selected;
    private LibraryItemRow? _draft;
    private string? _usedBy;
    private string? _status;

    public LibraryPanel(LibraryService library, IUserPrompts prompts)
    {
        _library = library;
        _prompts = prompts;

        NewCommand = new RelayCommand(_ => New());
        _saveCommand = new RelayCommand(_ => Save(), _ => Draft is not null);
        _deleteCommand = new RelayCommand(_ => Delete(), _ => Draft is { IsNew: false });

        foreach (var item in library.GetAll().OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            Items.Add(LibraryItemRow.From(item));
    }

    /// <summary>Raised after an item is saved, with its new values.</summary>
    public event Action<LibraryItem>? ItemSaved;

    /// <summary>Raised after an item is deleted, with its id.</summary>
    public event Action<string>? ItemRemoved;

    public ObservableCollection<LibraryItemRow> Items { get; } = [];

    public LibraryItemRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            Draft = value?.Clone();
            Status = null;
        }
    }

    /// <summary>The form's working copy of the selected item (or a new one); changes apply on Save.</summary>
    public LibraryItemRow? Draft
    {
        get => _draft;
        private set
        {
            if (!SetProperty(ref _draft, value)) return;
            OnPropertyChanged(nameof(HasDraft));
            UsedBy = value is { Id: { } id } ? Describe(_library.UsedBy(id)) : null;
            _saveCommand.NotifyCanExecuteChanged();
            _deleteCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasDraft => Draft is not null;

    public string? UsedBy { get => _usedBy; private set => SetProperty(ref _usedBy, value); }

    public string? Status { get => _status; set => SetProperty(ref _status, value); }

    public ICommand NewCommand { get; }
    public ICommand SaveCommand => _saveCommand;
    public ICommand DeleteCommand => _deleteCommand;

    public LibraryItem? Find(string id) => _library.Find(id);

    /// <summary>
    /// The library item for something startable - reusing one that starts the same thing, else adding it under
    /// <paramref name="name"/> - selected in the list.
    /// </summary>
    public LibraryItem Keep(string name, ProfileAction action)
    {
        var item = _library.FindOrAdd(name, action with { LibraryItemId = null });
        var row = Items.FirstOrDefault(r => r.Id == item.Id);
        if (row is null)
        {
            row = LibraryItemRow.From(item);
            InsertSorted(row);
        }

        Selected = row;
        return item;
    }

    private void New()
    {
        _selected = null;
        OnPropertyChanged(nameof(Selected));
        Draft = new LibraryItemRow { Name = "New item" };
        Status = "Fill in a target, then Save.";
    }

    private void Save()
    {
        if (Draft is null) return;

        var item = Draft.ToItem();
        if (item.Name.Length == 0 || item.Target.Length == 0)
        {
            Status = "A name and a target are required.";
            return;
        }

        if (Draft.IsNew)
        {
            item = _library.Add(item);
            Draft.Update(item);
            var row = LibraryItemRow.From(item);
            InsertSorted(row);
            _selected = row;
            OnPropertyChanged(nameof(Selected));
            _deleteCommand.NotifyCanExecuteChanged();
        }
        else
        {
            _library.Save(item);
            if (Items.FirstOrDefault(r => r.Id == item.Id) is { } row) row.Update(item);
        }

        UsedBy = Describe(_library.UsedBy(item.Id));
        Status = "Saved.";
        ItemSaved?.Invoke(item);
    }

    private void Delete()
    {
        if (Draft is not { Id: { } id } draft) return;

        var users = _library.UsedBy(id);
        var message = users.Count == 0
            ? $"Delete '{draft.Name}' from the library?"
            : $"Delete '{draft.Name}' from the library?\n\nIt is used by {string.Join(", ", users)}. Those rows keep starting it, as standalone actions.";
        if (!_prompts.Confirm(message)) return;

        _library.Remove(id);
        if (Items.FirstOrDefault(r => r.Id == id) is { } row) Items.Remove(row);
        Selected = null;
        Status = "Deleted.";
        ItemRemoved?.Invoke(id);
    }

    private void InsertSorted(LibraryItemRow row)
    {
        var index = 0;
        while (index < Items.Count && StringComparer.CurrentCultureIgnoreCase.Compare(Items[index].Name, row.Name) <= 0) index++;
        Items.Insert(index, row);
    }

    private static string Describe(IReadOnlyList<string> users) =>
        users.Count == 0 ? "Not used by any profile yet." : $"Used by {string.Join(", ", users)}.";
}
