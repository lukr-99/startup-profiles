namespace StartupProfiles.App.Ui;

/// <summary>The profile icons the picker offers, grouped so a fitting one is quick to find.</summary>
public static class IconCatalog
{
    public static IReadOnlyList<(string Group, IReadOnlyList<string> Icons)> Groups { get; } =
    [
        ("Work", ["💼", "🏢", "💻", "⌨️", "🖥️", "📊", "📅", "📧", "📞", "💬", "🗂️", "📁"]),
        ("Code", ["🐛", "🧩", "🔧", "⚙️", "🚀", "🧪", "🛠️", "🖱️", "🔒", "🛡️", "☁️", "📦"]),
        ("Study", ["🎓", "📚", "✏️", "📝", "🧠", "🔬", "📐", "🌍", "🗒️", "📖", "🏫", "💡"]),
        ("Play", ["🎮", "🕹️", "🎯", "🎲", "🎧", "🎵", "🎬", "📺", "🎨", "📷", "🏆", "👾"]),
        ("Life", ["🏠", "☕", "🌙", "☀️", "🍕", "🏋️", "🩺", "💰", "🛒", "✈️", "🌿", "🐾"]),
        ("Symbols", ["⭐", "✨", "🔥", "✅", "🌟", "❤️", "⚡", "🔔", "📌", "🎉", "🌈", "🔵"]),
    ];

    public static IEnumerable<string> All => Groups.SelectMany(g => g.Icons);
}
