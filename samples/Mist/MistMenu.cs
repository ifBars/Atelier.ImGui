using System.Numerics;
using Atelier;
using ImGuiNET;
using StyledMenus;

namespace Mist;

/// <summary>A quiet, borderless menu: one accent, hairline rules, text navigation and pill controls. State is local to this sample.</summary>
public sealed class MistMenu : StyledMenuBase
{
    private sealed record Accent(string Name, Vector4 Color, Vector4 Hover);
    private static readonly string[] Pages = ["Overview", "Display", "Input"];
    private static readonly Accent[] Accents =
    [
        new("Indigo", UiTheme.Rgb(0x5B5BD6), UiTheme.Rgb(0x4A4AC4)),
        new("Teal", UiTheme.Rgb(0x0E9F8E), UiTheme.Rgb(0x0B8A7B)),
        new("Rose", UiTheme.Rgb(0xE5484D), UiTheme.Rgb(0xD13A40))
    ];
    private const float ContentX = 250, ContentWidth = 480;

    private int page, accent;
    private bool enabled = true, notifications = true, autoStart;
    private bool blur = true, reducedMotion, smooth = true, haptics;
    private float volume = 70, brightness = 80, scale = 100, sensitivity = 50, repeatDelay = 35;

    protected override float Rounding => 14;
    public static Vector2 CanvasSize => new(780, 520);
    public bool Enabled => enabled;
    public float Volume => volume;
    public int AccentIndex => accent;
    public string SelectedPage { get => Pages[page]; set => page = Array.IndexOf(Pages, value) is var i && i >= 0 ? i : throw new ArgumentException("Unknown page."); }

    public MistMenu(Ui ui) : base(ui, default, "##mist", CanvasSize)
    {
        ui.Theme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0xFAFAF9),
            Surface = UiTheme.Rgb(0xF1F1EF),
            SurfaceHover = UiTheme.Rgb(0xE8E8E5),
            Field = UiTheme.Rgb(0xF1F1EF),
            Border = UiTheme.Rgb(0xE2E2DE),
            Text = UiTheme.Rgb(0x1C1C1A),
            Muted = UiTheme.Rgb(0x8A8A85),
            Accent = Accents[0].Color,
            AccentHover = Accents[0].Hover,
            OnAccent = Vector4.One
        };
        ui.StackFieldsBelow = 0;
    }

    protected override void Paint()
    {
        Ui.Theme = Ui.Theme with { Accent = Accents[accent].Color, AccentHover = Accents[accent].Hover };
        Ui.ReducedMotion = reducedMotion;
        var t = Ui.Theme;

        Text(36, 36, "mist", 22);
        Rect(36, 66, 18, 2, t.Accent, 1);
        for (var i = 0; i < Pages.Length; i++)
        {
            var y = 122 + i * 40;
            if (Hit("nav." + Pages[i], new(24, y), new(168, 34))) page = i;
            var hover = ImGui.IsItemHovered();
            Text(52, y + 8, Pages[i], 15, page == i ? t.Text : hover ? t.Text with { W = .75f } : t.Muted);
        }
        var dotY = Ui.Animate("nav", page * 40);
        Rect(36, 122 + 14 + dotY, 6, 6, t.Accent, 3);
        Line(212, 36, 212, 484, t.Border);

        Text(ContentX, 40, Pages[page], 26);
        Text(ContentX, 78, Subtitle(), 13, t.Muted);

        ImGui.SetCursorPos(new Vector2(ContentX, 120) * Ui.Scale);
        Ui.Transition("mist-page", page, DrawControls, offset: Vector2.Zero);

        Text(ContentX, 470, Feedback, 12, t.Muted);
        Pill("reset", "Reset", new(560, 454), new(80, 36), false, Reset);
        Pill("save", "Save", new(650, 454), new(80, 36), true, () => Feedback = "Saved to this preview");
    }

    private string Subtitle() => page switch
    {
        0 => "Core behavior and sound.",
        1 => "Appearance and motion.",
        _ => "Pointer and keyboard response.",
    };

    private void DrawControls()
    {
        if (page == 0)
        {
            Row("enabled", "Enabled", "Turn the module on or off.", ref enabled, 0);
            using (Ui.Disabled(!enabled))
            {
                Row("notify", "Notifications", "Show a quiet toast on changes.", ref notifications, 1);
                Row("autostart", "Launch at startup", "Start with the host application.", ref autoStart, 2);
                Slider("volume", "Volume", ref volume, 0, 100, ContentX, 330, ContentWidth, "%");
            }
        }
        else if (page == 1)
        {
            Row("blur", "Backdrop blur", "Soften what sits behind the menu.", ref blur, 0);
            Row("motion", "Reduce motion", "Skip transitions and easing.", ref reducedMotion, 1);
            Slider("brightness", "Brightness", ref brightness, 10, 100, ContentX, 258, ContentWidth, "%");
            Slider("scale", "Interface scale", ref scale, 75, 150, ContentX, 324, ContentWidth, "%");
            Text(ContentX, 398, "Accent", 15);
            Segmented("accent", ref accent, [.. Accents.Select(a => a.Name)], ContentX + ContentWidth - 240, 390, 240);
        }
        else
        {
            Row("smooth", "Smooth scrolling", "Ease long lists into place.", ref smooth, 0);
            Row("haptics", "Haptic feedback", "Pulse a connected controller.", ref haptics, 1);
            Slider("sensitivity", "Sensitivity", ref sensitivity, 0, 100, ContentX, 258, ContentWidth, "%");
            Slider("repeat", "Repeat delay", ref repeatDelay, 10, 100, ContentX, 330, ContentWidth, " ms");
        }
    }

    private void Row(string id, string label, string detail, ref bool value, int index)
    {
        var y = 120 + index * 66;
        if (index > 0) Line(ContentX, y - 4, ContentX + ContentWidth, y - 4, Ui.Theme.Border);
        Switch(id, label, ref value, ContentX, y + 2, ContentWidth, detail);
    }

    private void Segmented(string id, ref int value, string[] options, float x, float y, float width)
    {
        var t = Ui.Theme;
        var segment = width / options.Length;
        Rect(x, y, width, 32, t.Surface, 16);
        var thumb = Ui.Animate(id + ":thumb", value);
        Rect(x + 3 + thumb * segment, y + 3, segment - 6, 26, t.Accent, 13);
        for (var i = 0; i < options.Length; i++)
        {
            if (Hit($"{id}.{i}", new(x + i * segment, y), new(segment, 32))) value = i;
            var textWidth = ImGui.CalcTextSize(options[i]).X / Ui.Scale * 13 / (ImGui.GetFontSize() / Ui.Scale);
            Text(x + i * segment + (segment - textWidth) / 2, y + 8, options[i], 13, value == i ? t.OnAccent : t.Muted);
        }
    }

    private void Pill(string id, string label, Vector2 position, Vector2 size, bool primary, System.Action action)
    {
        var t = Ui.Theme;
        var pressed = Hit(id, position, size);
        var hover = ImGui.IsItemHovered();
        Rect(position.X, position.Y, size.X, size.Y, primary ? hover ? t.AccentHover : t.Accent : hover ? t.SurfaceHover : t.Surface, size.Y / 2);
        var textWidth = ImGui.CalcTextSize(label).X / Ui.Scale * 14 / (ImGui.GetFontSize() / Ui.Scale);
        Text(position.X + (size.X - textWidth) / 2, position.Y + 10, label, 14, primary ? t.OnAccent : t.Text);
        if (ImGui.GetIO().NavVisible && ImGui.IsItemFocused())
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), C(t.Accent), Ui.S(size.Y / 2), ImDrawFlags.None, Ui.S(2));
        if (pressed) action();
    }

    private void Reset()
    {
        enabled = notifications = blur = smooth = true;
        autoStart = reducedMotion = haptics = false;
        volume = 70; brightness = 80; scale = 100; sensitivity = 50; repeatDelay = 35; accent = 0;
        Feedback = "Defaults restored";
    }
}
