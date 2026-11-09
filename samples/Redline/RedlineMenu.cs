using System.Numerics;
using Atelier;
using ImGuiNET;
using StyledMenus;

namespace Redline;

public sealed class RedlineMenu : StyledMenuBase
{
    private int page;
    private bool traction = true, abs = true, racingLine, names = true, map = true, reducedMotion;
    private float steering = 65, camera = 80, shake = 25, hud = 90;
    private int units;
    private static readonly string[] Pages = ["DRIVING", "CAMERA", "HUD"];
    protected override float Rounding => 0;
    public string SelectedPage { get => Pages[page]; set => page = Array.IndexOf(Pages, value) is var i && i >= 0 ? i : throw new ArgumentException("Unknown page."); }
    public bool TractionEnabled => traction;
    public float Steering => steering;

    public RedlineMenu(Ui ui, UiImage artwork) : base(ui, artwork, "##redline", new(900, 600))
    {
        ui.Theme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0xF0EBDF),
            Sidebar = UiTheme.Rgb(0x161714),
            Surface = UiTheme.Rgb(0xE4DED1),
            SurfaceHover = UiTheme.Rgb(0xDBD4C6),
            Field = UiTheme.Rgb(0xE4DED1),
            Border = UiTheme.Rgb(0xCEC7BA),
            Text = UiTheme.Rgb(0x191A17),
            Muted = UiTheme.Rgb(0x68685D),
            Accent = UiTheme.Rgb(0xD72E27),
            AccentHover = UiTheme.Rgb(0xBD211E),
            OnAccent = Vector4.One
        };
        ui.StackFieldsBelow = 0;
    }

    protected override void Paint()
    {
        var t = Ui.Theme;
        Rect(0, 0, 900, 7, t.Accent);
        Text(24, 21, "REDLINE", 60, display: true);
        Text(27, 88, "D R I V E R ' S   T O O L K I T", 11, t.Muted);
        Text(573, 35, "VEHICLE MOD / LOCAL SETUP", 12, t.Muted);
        Line(24, 112, 876, 112, t.Text, 1.5f);

        Ui.DrawImage(Artwork, P(24, 133), new Vector2(317, 350) * Ui.Scale, ImageFit.Cover);
        Ui.Gradient(P(24, 133), new Vector2(317, 350) * Ui.Scale, new Vector4(0, 0, 0, .05f), new Vector4(.025f, .03f, .025f, .88f), vertical: true);
        Slant(42, 151, 116, 27, t.Accent);
        Text(53, 156, "STREET / 03", 15, Vector4.One, display: true);
        Text(45, 329, "AFTER", 55, Vector4.One, display: true);
        Text(45, 385, "HOURS.", 65, Vector4.One, display: true);
        Text(46, 457, "FREE DRIVE / PERSONAL PRESET", 10, Vector4.One with { W = .75f });
        for (var i = 0; i < 3; i++) Slant(259 + i * 23, 455, 13, 28, t.Accent);

        for (var i = 0; i < Pages.Length; i++)
        {
            var x = 378 + i * 166;
            if (Hit("nav." + Pages[i], new(x, 133), new(154, 37))) page = i;
            Text(x + 12, 141, Pages[i], 22, page == i ? t.Accent : t.Muted, display: true);
        }
        var selectedX = Ui.Animate("tab", page * 166);
        Rect(378 + selectedX, 176, 154, 3, t.Accent);
        ImGui.SetCursorPos(new Vector2(378, 200) * Ui.Scale);
        Ui.Transition("redline-page", page, DrawControls, offset: Vector2.Zero);
        Text(379, 486, Feedback, 12, t.Muted);
        Line(24, 512, 876, 512, t.Text, 1.5f);
        Text(25, 531, "MAKE IT YOURS.", 29, display: true);
        Text(27, 568, "Settings apply to this local preview.", 11, t.Muted);
        Action("reset", "RESET", new(599, 535), new(108, 42), false, Reset);
        Action("apply", "APPLY SETUP", new(721, 535), new(155, 42), true, () => Feedback = "Setup applied to preview");
    }

    private void DrawControls()
    {
        var t = Ui.Theme;
        if (page == 0)
        {
            Switch("traction", "Traction control", ref traction, 390, 197, 473, textSwitch: true);
            Rule(248);
            Switch("abs", "Anti-lock braking", ref abs, 390, 259, 473, textSwitch: true);
            Rule(310);
            Switch("line", "Racing line", ref racingLine, 390, 321, 473, textSwitch: true);
            Rule(372);
            Slider("steering", "Steering sensitivity", ref steering, 0, 100, 390, 396, 473, "%");
        }
        else if (page == 1)
        {
            Slider("camera", "Field of view", ref camera, 50, 120, 390, 211, 473, "°");
            Rule(276);
            Slider("shake", "Camera shake", ref shake, 0, 100, 390, 301, 473, "%");
            Rule(366);
            Switch("motion", "Reduce motion", ref reducedMotion, 390, 390, 473, textSwitch: true);
            Ui.ReducedMotion = reducedMotion;
        }
        else
        {
            Switch("names", "Driver names", ref names, 390, 197, 473, textSwitch: true);
            Rule(248);
            Switch("map", "Mini-map", ref map, 390, 259, 473, textSwitch: true);
            Rule(310);
            Slider("hud", "HUD opacity", ref hud, 20, 100, 390, 333, 473, "%");
            Text(390, 422, "Distance units", 16);
            if (Hit("units", new(732, 414), new(131, 33))) units = 1 - units;
            Slant(732, 414, 131, 33, t.Text);
            Text(747, 421, units == 0 ? "KM / H" : "MPH", 19, Vector4.One, display: true);
        }
    }

    private void Rule(float y) => Line(390, y, 863, y, Ui.Theme.Border);
    private void Slant(float x, float y, float width, float height, Vector4 color)
    {
        Span<Vector2> corners = stackalloc Vector2[] { P(x + 6, y), P(x + width, y), P(x + width - 6, y + height), P(x, y + height) };
        ImGui.GetWindowDrawList().AddConvexPolyFilled(ref corners[0], corners.Length, C(color));
    }
    private void Reset()
    {
        traction = abs = names = map = true; racingLine = false;
        steering = 65; camera = 80; shake = 25; hud = 90; units = 0;
        Feedback = "Factory setup restored";
    }
}
