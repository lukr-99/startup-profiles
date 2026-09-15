namespace StartupProfiles.Windows;

/// <summary>The application inside a package that declares a startup task, read from its AppxManifest.xml.</summary>
/// <param name="AppId">The manifest <c>Application Id</c>, used in <c>shell:AppsFolder\{family}!{AppId}</c>.</param>
/// <param name="DisplayName">The task's literal display name, or null when it is missing or a resource reference.</param>
internal sealed record PackagedStartupApp(string AppId, string? DisplayName);
