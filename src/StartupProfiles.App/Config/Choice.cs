namespace StartupProfiles.App.Config;

/// <summary>One option of a combo box: the stored <see cref="Value"/> and the plain words shown for it.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    // The themed combo box shows the selected item as text, so the label is the item's text.
    public override string ToString() => Label;
}
