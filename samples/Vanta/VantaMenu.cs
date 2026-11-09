using System.Numerics;
using Atelier;
using ImGuiNET;
using StyledMenus;

namespace Vanta;

public sealed class VantaMenu : StyledMenuBase
{
    private int page;
    private bool enabled = true, names = true, trails = true, bloom, reducedMotion;
    private float range = 350, opacity = 80, intensity = 45;
    private Vector4 marker = UiTheme.Rgb(0x72DCED);
    private string profile = "Orbital";
    private (bool Enabled, bool Names, bool Trails, bool Bloom, float Range, float Opacity, float Intensity, Vector4 Marker)? saved;
    private static readonly string[] Pages = ["Visuals", "Navigation", "World", "Profiles"];
    protected override float Rounding => 12;
    public string SelectedPage { get => Pages[page]; set => page = Array.IndexOf(Pages, value) is var i && i >= 0 ? i : throw new ArgumentException("Unknown page."); }
    public bool OverlayEnabled => enabled;

    public VantaMenu(Ui ui, UiImage artwork) : base(ui, artwork, "##vanta", new(960, 620))
    {
        ui.Theme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0x10141B),
            Sidebar = UiTheme.Rgb(0x0C1016),
            Surface = UiTheme.Rgb(0x17212C),
            SurfaceHover = UiTheme.Rgb(0x203240),
            Field = UiTheme.Rgb(0x0C131B),
            Border = UiTheme.Rgb(0x293540),
            Accent = marker,
            AccentHover = UiTheme.Rgb(0xA6ECF7),
            OnAccent = UiTheme.Rgb(0x09222C),
            Muted = UiTheme.Rgb(0x8496A6)
        };
        ui.StackFieldsBelow = 0;
    }

    protected override void Paint()
    {
        var t = Ui.Theme;
        Rect(0, 0, 196, 620, t.Sidebar, 12);
        Rect(184, 0, 12, 620, t.Sidebar);
        Line(196, 0, 196, 620, t.Border with { W = .6f });
        Text(25, 23, "VANTA", 38, display: true);
        Text(27, 68, "O R B I T A L   T O O L K I T", 10, t.Muted);
        Line(24, 103, 172, 103, t.Border);
        Text(26, 125, "MODULES", 11, t.Muted);
        for (var i = 0; i < Pages.Length; i++)
        {
            var y = 153 + i * 55;
            if (Hit("nav." + Pages[i], new(17, y), new(162, 43))) page = i;
            var amount = Ui.Animate("nav:" + i, page == i ? 1 : 0);
            if (amount > 0) Rect(17, y, 162, 43, t.Accent with { W = .085f * amount }, 6);
            var color = Vector4.Lerp(t.Muted, t.Accent, amount);
            Icon(38, y + 21, i, color);
            Text(62, y + 12, Pages[i], 15, page == i ? t.Text : t.Muted);
        }
        Ui.DrawImage(Artwork, P(26, 548), new Vector2(32) * Ui.Scale, ImageFit.Cover, Ui.S(16));
        Text(69, 548, profile, 15);
        Text(69, 571, "LOCAL PROFILE", 10, t.Muted);

        Text(230, 25, Pages[page].ToUpperInvariant(), 34, display: true);
        Text(232, 68, page == 0 ? "Player & world overlay" : page == 1 ? "Signals & flight guidance" : page == 2 ? "Lighting & atmosphere" : "Your personal configuration", 13, t.Muted);
        Action("profile.save", "Save profile", new(770, 31), new(134, 37), false, Save);
        Line(230, 99, 929, 99, t.Border with { W = .6f });

        ImGui.SetCursorPos(new Vector2(230, 117) * Ui.Scale);
        Ui.Transition("vanta-page", page, DrawControls, offset: Vector2.Zero);
        DrawPreview();
        Line(230, 571, 929, 571, t.Border with { W = .6f });
        ImGui.GetWindowDrawList().AddCircleFilled(P(235, 594), Ui.S(2.5f), C(t.Accent), 12);
        Text(247, 586, Feedback, 12, t.Muted);
        Text(827, 586, "VANTA / 01", 11, t.Muted);
    }

    private void DrawControls()
    {
        var t = Ui.Theme;
        Text(230, 119, page switch { 0 => "RENDERING", 1 => "GUIDANCE", 2 => "ATMOSPHERE", _ => "CONFIGURATION" }, 11, t.Muted);
        if (page == 3)
        {
            Text(230, 164, "Profile name", 15);
            ImGui.SetCursorPos(new Vector2(230, 196) * Ui.Scale);
            ImGui.SetNextItemWidth(Ui.S(310));
            ImGui.InputText("##profile-name", ref profile, 64);
            Remember("profile.name");
            Switch("motion", "Reduce motion", ref reducedMotion, 230, 256, 310, "Stop continuous effects and transitions.");
            Ui.ReducedMotion = reducedMotion;
            Action("profile.load", "Load saved", new(230, 345), new(145, 42), false, Load);
            Action("profile.reset", "Reset", new(390, 345), new(150, 42), false, Reset);
            return;
        }
        if (page == 0)
        {
            if (Switch("overlay", "Object overlay", ref enabled, 230, 155, 310, "Outline nearby ships and stations.")) Feedback = "Unsaved changes";
            Switch("names", "Object labels", ref names, 230, 225, 310, "Show identity and relative distance.");
            Switch("trails", "Motion trails", ref trails, 230, 295, 310, "Leave a short flight-path trace.");
            Line(230, 369, 540, 369, t.Border with { W = .65f });
            Slider("range", "Render distance", ref range, 50, 500, 230, 393, 310, " m");
            Slider("opacity", "Overlay opacity", ref opacity, 10, 100, 230, 463, 310, "%");
            Color("marker", "Marker color", ref marker, 230, 532, 310);
        }
        else if (page == 1)
        {
            Switch("guide.trails", "Flight-path trace", ref trails, 230, 155, 310, "Track your approach to a waypoint.");
            Switch("guide.labels", "Waypoint labels", ref names, 230, 225, 310, "Keep your destination in view.");
            Slider("guide.range", "Detection range", ref range, 50, 500, 230, 331, 310, " m");
            Color("guide.color", "Signal color", ref marker, 230, 422, 310);
        }
        else
        {
            Switch("world.bloom", "Bloom", ref bloom, 230, 155, 310, "A soft halo around bright objects.");
            Switch("world.trails", "Ambient particles", ref trails, 230, 225, 310, "A quiet layer of drifting starlight.");
            Slider("world.intensity", "Light intensity", ref intensity, 0, 100, 230, 331, 310, "%");
        }
    }

    private void DrawPreview()
    {
        var t = Ui.Theme;
        Ui.DrawImage(Artwork, P(584, 118), new Vector2(344, 431) * Ui.Scale, ImageFit.Cover, Ui.S(9));
        Ui.Gradient(P(584, 118), new Vector2(344, 431) * Ui.Scale, new Vector4(.01f, .04f, .08f, .22f), new Vector4(.02f, .04f, .07f, .94f), vertical: true);
        Text(606, 140, "ORBITAL PREVIEW", 11, t.Accent);
        var dl = ImGui.GetWindowDrawList();
        var center = P(756, 316);
        var time = Ui.ReducedMotion ? 0 : ImGui.GetTime() * .1;
        for (var i = 0; i < 3; i++)
        {
            var radius = Ui.S(70 + i * 26);
            dl.AddCircle(center, radius, C(t.Accent with { W = .12f }), 96);
            var angle = (float)time + i * 2.2f;
            dl.AddCircleFilled(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, Ui.S(2), C(t.Accent), 12);
        }
        dl.AddTriangleFilled(P(756, 251), P(792, 346), P(756, 328), C(UiTheme.Rgb(0x152C3A)));
        dl.AddTriangleFilled(P(756, 251), P(756, 328), P(720, 346), C(UiTheme.Rgb(0x152C3A)));
        Line(756, 251, 792, 346, t.Accent, 1.5f); Line(792, 346, 756, 328, t.Accent, 1.5f);
        Line(756, 328, 720, 346, t.Accent, 1.5f); Line(720, 346, 756, 251, t.Accent, 1.5f);
        Line(756, 251, 756, 328, t.Accent with { W = .5f });
        if (trails)
        {
            Ui.Gradient(P(745, 334), new Vector2(22, 66) * Ui.Scale, t.Accent with { W = .3f }, t.Accent with { W = 0 }, vertical: true);
        }
        if (enabled)
        {
            var color = marker with { W = opacity / 100 };
            foreach (var corner in new[] { new Vector2(700, 237), new(812, 237), new(812, 372), new(700, 372) })
            {
                var signX = corner.X == 700 ? 1 : -1;
                var signY = corner.Y == 237 ? 1 : -1;
                Line(corner.X, corner.Y, corner.X + signX * 13, corner.Y, color, 1.5f);
                Line(corner.X, corner.Y, corner.X, corner.Y + signY * 13, color, 1.5f);
            }
        }
        if (names) { Text(706, 213, "AURORA / 128 m", 11, t.Accent); }
        if (bloom)
            for (var i = 4; i > 0; i--) dl.AddCircle(center, Ui.S(30 + i * 5), C(t.Accent with { W = intensity / 100 * .08f }), 48, Ui.S(4));
        Text(607, 452, "DEEP SPACE", 30, display: true);
        Text(608, 493, "Navigation profile / Orbital", 12, t.Muted with { X = .68f, Y = .75f, Z = .8f });
        Line(608, 526, 905, 526, t.Text with { W = .16f });
    }

    private void Icon(float x, float y, int kind, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        if (kind == 0) { dl.AddCircle(P(x, y), Ui.S(7), C(color), 20); Line(x - 10, y, x - 4, y, color); Line(x + 4, y, x + 10, y, color); Line(x, y - 10, x, y - 4, color); Line(x, y + 4, x, y + 10, color); }
        else if (kind == 1) { Line(x, y - 8, x + 7, y + 7, color); Line(x + 7, y + 7, x, y + 3, color); Line(x, y + 3, x - 7, y + 7, color); Line(x - 7, y + 7, x, y - 8, color); }
        else if (kind == 2) { dl.AddCircle(P(x, y), Ui.S(8), C(color), 24); Line(x - 7, y, x + 7, y, color); dl.AddEllipse(P(x, y), new(Ui.S(3), Ui.S(8)), C(color)); }
        else { Line(x - 7, y - 6, x + 7, y - 6, color); Line(x - 7, y, x + 7, y, color); Line(x - 7, y + 6, x + 7, y + 6, color); dl.AddCircleFilled(P(x - 2, y - 6), Ui.S(2.5f), C(color)); dl.AddCircleFilled(P(x + 4, y), Ui.S(2.5f), C(color)); }
    }

    private void Save() { saved = (enabled, names, trails, bloom, range, opacity, intensity, marker); Feedback = "Profile saved in memory"; }
    private void Load()
    {
        if (saved is not { } p) { Feedback = "No saved profile"; return; }
        (enabled, names, trails, bloom, range, opacity, intensity, marker) = p;
        Feedback = "Profile loaded";
    }
    private void Reset() { enabled = names = trails = true; bloom = false; range = 350; opacity = 80; intensity = 45; Feedback = "Defaults restored"; }
}
