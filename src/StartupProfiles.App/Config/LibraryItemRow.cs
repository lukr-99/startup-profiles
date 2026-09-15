using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// A library item in the side panel's Created tab, and the editable draft behind its form. A draft with no
/// <see cref="Id"/> is a new item that does not exist until saved.
/// </summary>
public sealed class LibraryItemRow : ObservableObject
{
    private string _name = "";
    private ActionType _type = ActionType.LaunchApp;
    private string _target = "";
    private string _arguments = "";
    private bool _runAsAdmin;

    public string? Id { get; private set; }
    public bool IsNew => Id is null;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ActionType Type { get => _type; set => SetProperty(ref _type, value); }
    public string Target { get => _target; set => SetProperty(ref _target, value); }
    public string Arguments { get => _arguments; set => SetProperty(ref _arguments, value); }
    public bool RunAsAdmin { get => _runAsAdmin; set => SetProperty(ref _runAsAdmin, value); }

    public static LibraryItemRow From(LibraryItem item)
    {
        var row = new LibraryItemRow();
        row.Update(item);
        return row;
    }

    /// <summary>Takes the saved values (including the id of a newly added item).</summary>
    public void Update(LibraryItem item)
    {
        Id = item.Id;
        Name = item.Name;
        Type = item.Type;
        Target = item.Target;
        Arguments = item.Arguments ?? "";
        RunAsAdmin = item.RunAsAdmin;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsNew));
    }

    public LibraryItemRow Clone()
    {
        var copy = new LibraryItemRow { Name = Name, Type = Type, Target = Target, Arguments = Arguments, RunAsAdmin = RunAsAdmin };
        copy.Id = Id;
        return copy;
    }

    public LibraryItem ToItem() => new()
    {
        Id = Id ?? "",
        Name = Name.Trim(),
        Type = Type,
        Target = Target.Trim(),
        Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
        RunAsAdmin = RunAsAdmin,
    };
}
