namespace StartupProfiles.App.Interaction;

/// <summary>
/// Dialog interactions the config view model needs. Behind an interface so the view models stay
/// testable (rule: isolate UI dialogs behind a controllable seam).
/// </summary>
public interface IUserPrompts
{
    /// <summary>Asks a yes/no question; true if the user confirms.</summary>
    bool Confirm(string message);

    /// <summary>Prompts for a line of text; null if cancelled.</summary>
    string? AskText(string title, string prompt);

    void Info(string message);

    /// <summary>Chooses a path to save to; null if cancelled.</summary>
    string? PickSavePath(string suggestedFileName);

    /// <summary>Chooses an existing file to open; null if cancelled.</summary>
    string? PickOpenPath();
}
