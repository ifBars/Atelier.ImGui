namespace Atelier.Windows;

// Default UI faces for the standalone host and the test harness. Menus that need a specific face bundle it.
internal static class HostFonts
{
    private static readonly (string Regular, string Bold)[] Candidates =
    [
        ("Inter-Regular.otf", "Inter-SemiBold.otf"),
        ("Inter-Regular.ttf", "Inter-SemiBold.ttf"),
        ("NotoSans-Regular.ttf", "NotoSans-SemiBold.ttf"),
        ("NotoSans-Regular.ttf", "NotoSans-Bold.ttf"),
        ("LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf"),
        ("DejaVuSans.ttf", "DejaVuSans-Bold.ttf")
    ];

    private static readonly Lazy<(string Regular, string Bold)?> Found = new(Search);

    /// <summary>Segoe UI on Windows, otherwise the first installed candidate; null when the host should use ImGui's built-in font.</summary>
    public static (string Regular, string Bold)? Find() => Found.Value;

    private static (string Regular, string Bold)? Search()
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var regular = Path.Combine(path, "segoeui.ttf");
            var bold = Path.Combine(path, "seguisb.ttf");
            return File.Exists(regular) ? (regular, File.Exists(bold) ? bold : regular) : null;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var search = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var root in new[]
                 {
                     Path.Combine(home, ".local/share/fonts"), Path.Combine(home, ".fonts"), Path.Combine(home, "Library/Fonts"),
                     "/usr/local/share/fonts", "/usr/share/fonts", "/Library/Fonts"
                 })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.?tf", search)) files.TryAdd(Path.GetFileName(file), file);
        }
        foreach (var (regular, bold) in Candidates)
            if (files.TryGetValue(regular, out var regularPath))
                return (regularPath, files.GetValueOrDefault(bold, regularPath));
        return null;
    }
}
