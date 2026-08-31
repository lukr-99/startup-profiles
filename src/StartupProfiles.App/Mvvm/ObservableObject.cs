using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StartupProfiles.App.Mvvm;

/// <summary>
/// Base class for view models; implements <see cref="INotifyPropertyChanged"/>. Mirrors
/// dotnetlib's <c>ObservableObject</c> so it can be swapped for the shared package once a local
/// NuGet feed is wired up (see docs/adr/0001-local-wpf-mvvm.md).
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
