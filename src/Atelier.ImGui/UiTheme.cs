using System.Numerics;

namespace Atelier;

/// <summary>Semantic colors. Use a record copy to customize a theme without editing widgets.</summary>
public sealed record UiTheme
{
    public Vector4 Background { get; init; } = Rgb(0x15181D);
    public Vector4 Sidebar { get; init; } = Rgb(0x111418);
    public Vector4 Surface { get; init; } = Rgb(0x1C2026);
    public Vector4 SurfaceHover { get; init; } = Rgb(0x272D35);
    public Vector4 Field { get; init; } = Rgb(0x13171C);
    public Vector4 Border { get; init; } = Rgb(0x303740);
    public Vector4 Text { get; init; } = Rgb(0xEDF0F3);
    public Vector4 Muted { get; init; } = Rgb(0x929CA9);
    public Vector4 Accent { get; init; } = Rgb(0x83D9B2);
    public Vector4 AccentHover { get; init; } = Rgb(0xA1E8C7);
    public Vector4 OnAccent { get; init; } = Rgb(0x10251C);
    public Vector4 Danger { get; init; } = Rgb(0xF28F95);
    public Vector4 Warning { get; init; } = Rgb(0xE4BC79);

    public static UiTheme Graphite { get; } = new();

    public static Vector4 Rgb(uint rgb) => new(
        ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
}
