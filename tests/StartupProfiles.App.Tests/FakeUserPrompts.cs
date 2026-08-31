using StartupProfiles.App.Interaction;

namespace StartupProfiles.App.Tests;

/// <summary>Deterministic <see cref="IUserPrompts"/> for view-model tests: returns canned answers.</summary>
internal sealed class FakeUserPrompts : IUserPrompts
{
    public bool ConfirmResult { get; set; } = true;
    public string? TextResult { get; set; }
    public string? SavePathResult { get; set; }
    public string? OpenPathResult { get; set; }
    public List<string> Infos { get; } = [];

    public bool Confirm(string message) => ConfirmResult;
    public string? AskText(string title, string prompt) => TextResult;
    public void Info(string message) => Infos.Add(message);
    public string? PickSavePath(string suggestedFileName) => SavePathResult;
    public string? PickOpenPath() => OpenPathResult;
}
