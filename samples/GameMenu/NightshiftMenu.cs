using System.Numerics;
using Atelier;
using ImGuiNET;

namespace GameMenu;

/// <summary>A game-menu composition over Atelier's scoped theme. All settings and previews are local.</summary>
public sealed class NightshiftMenu : IMenuSurface
{
    private readonly Ui ui;
    private readonly UiImage? banner;
    private bool particles = true, gradient = true;
    private float particleSpeed = 12;
    private int imageFit;
    private static readonly string[] ImageFits = ["Contain", "Cover", "Stretch"];
    private int tab;
    private bool boxes = true, skeleton = true, health = true, names = true, distance;
    private bool teamFilter = true, visibleOnly, crosshair = true, items = true, reducedMotion;
    private float range = 250, thickness = 1.5f, crosshairSize = 8;
    private Vector4 playerColor = UiTheme.Rgb(0xCBE985);
    private Vector4 hiddenColor = UiTheme.Rgb(0xD89588);
    private int boxStyle;
    private int profile;
    private string profileName = "default";
    private string feedback = "";
    private readonly Dictionary<int, SavedProfile> saved = [];
    private sealed record SavedProfile(bool Boxes, bool Skeleton, bool Health, bool Names, bool Distance,
        bool TeamFilter, bool VisibleOnly, bool Crosshair, bool Items, float Range, float Thickness,
        float CrosshairSize, Vector4 Color, Vector4 HiddenColor, int BoxStyle);
    private static readonly string[] Tabs = ["VISUALS", "GAMEPLAY", "WORLD", "CONFIG", "EFFECTS"];
    private static readonly string[] BoxStyles = ["Corners", "Full box"];
    private readonly string[] profiles = ["Default", "Low profile", "Recording"];

    public NightshiftMenu(Ui ui, UiImage? banner = null)
    {
        this.ui = ui;
        this.banner = banner;
        ui.StackFieldsBelow = 0;
        ui.Theme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0x161719),
            Sidebar = UiTheme.Rgb(0x111214),
            Surface = UiTheme.Rgb(0x1B1C1F),
            Field = UiTheme.Rgb(0x121315),
            Border = UiTheme.Rgb(0x35373C),
            Muted = UiTheme.Rgb(0x9A9DA5),
            Accent = playerColor,
            AccentHover = UiTheme.Rgb(0xDFEFB5),
            OnAccent = UiTheme.Rgb(0x1D2414)
        };
    }

    public bool IsOpen { get; private set; } = true;
    public event Action? DragStarted;
    public string ActiveTab { get => Tabs[tab]; set => tab = Array.IndexOf(Tabs, value) is var i && i >= 0 ? i : throw new ArgumentException("Unknown tab."); }

    public void Draw(bool fillViewport = false)
    {
        if (!IsOpen) return;
        var accent = playerColor with { W = 1 };
        if (ui.Theme.Accent != accent)
            ui.Theme = ui.Theme with { Accent = accent, AccentHover = Vector4.Lerp(accent, Vector4.One, .2f) };
        using var style = ui.Style();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, ui.S(3));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ui.S(2));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ui.S(7), ui.S(4)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ui.S(8), ui.S(6)));
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(fillViewport ? viewport.Size : new Vector2(800, 550) * ui.Scale, fillViewport ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(fillViewport ? viewport.Pos : viewport.WorkPos + (viewport.WorkSize - new Vector2(800, 550) * ui.Scale) / 2,
            fillViewport ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        var visible = ImGui.Begin("##nightshift", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        try
        {
            if (!visible) return;
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var draw = ImGui.GetWindowDrawList();
            var t = ui.Theme;
            draw.AddRectFilled(pos, pos + new Vector2(size.X, ui.S(53)), C(t.Sidebar));
            if (gradient) ui.Gradient(pos, new(size.X, ui.S(53)), t.Sidebar, Vector4.Lerp(t.Sidebar, t.Accent, .09f));
            draw.AddRectFilled(pos, pos + new Vector2(size.X, ui.S(2)), C(t.Accent));
            Label(new(22, 18), "nightshift", t.Text);
            Label(new(150, 23), "/  overlay", t.Muted, small: true);
            Label(new(size.X / ui.Scale - 170, 21), "LOCAL PREVIEW", t.Muted, small: true);
            ImGui.SetCursorPos(new(ui.S(190), ui.S(5)));
            ImGui.InvisibleButton("##drag", new(size.X - ui.S(380), ui.S(45)));
            if (ImGui.IsItemActivated()) DragStarted?.Invoke();
            if (!fillViewport && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(pos + ImGui.GetIO().MouseDelta);
            ImGui.SetCursorPos(new(size.X - ui.S(40), ui.S(15)));
            if (ImGui.Button("x##close", new(ui.S(24), ui.S(24)))) IsOpen = false;

            for (var i = 0; i < Tabs.Length; i++)
            {
                ImGui.SetCursorPos(new(ui.S(16 + i * 125), ui.S(61)));
                if (ui.Tab(Tabs[i], Tabs[i], tab == i, new(115, 32), drawIndicator: false)) tab = i;
            }
            var indicator = ui.Animate("tab-position", tab * 125);
            draw.AddLine(pos + new Vector2(ui.S(22 + indicator), ui.S(99)), pos + new Vector2(ui.S(119 + indicator), ui.S(99)), C(t.Accent), ui.S(2));
            draw.AddLine(pos + new Vector2(0, ui.S(100)), pos + new Vector2(size.X, ui.S(100)), C(t.Border));
            var left = MathF.Floor((size.X - ui.S(60)) * .52f);
            var contentHeight = size.Y - ui.S(166);
            Panel("settings", new(ui.S(20), ui.S(116)), new(left, contentHeight), () => ui.Transition("settings-page", tab, DrawSettings));
            Panel("preview", new(left + ui.S(40), ui.S(116)), new(size.X - left - ui.S(60), contentHeight), () => ui.Transition("preview-page", tab, DrawPreview));
            var bottom = size.Y - ui.S(38);
            draw.AddLine(pos + new Vector2(0, bottom - ui.S(10)), pos + new Vector2(size.X, bottom - ui.S(10)), C(t.Border));
            ImGui.SetCursorPos(new(ui.S(20), bottom));
            ImGui.SetNextItemWidth(ui.S(145));
            if (ImGui.Combo("##profile", ref profile, profiles, profiles.Length)) profileName = profiles[profile];
            ImGui.SameLine();
            if (ImGui.Button("Load", new(ui.S(55), 0))) Load();
            ImGui.SameLine();
            if (ImGui.Button("Save", new(ui.S(55), 0))) { Save(); feedback = "Saved in memory"; }
            ImGui.SameLine();
            if (ImGui.Button("Reset", new(ui.S(55), 0))) { boxes = skeleton = health = names = teamFilter = true; distance = visibleOnly = false; range = 250; thickness = 1.5f; feedback = "Visuals reset"; }
            ImGui.SameLine();
            ui.MutedText(feedback);
        }
        finally { ImGui.End(); ImGui.PopStyleVar(6); }
    }

    private void DrawSettings()
    {
        Caption(tab switch { 0 => "PLAYER OVERLAY", 1 => "CROSSHAIR", 2 => "WORLD OVERLAY", 3 => "CONFIGURATION", _ => "EFFECTS" });
        switch (tab)
        {
            case 0:
                ui.Checkbox("boxes", "Bounding box", ref boxes);
                ui.Checkbox("skeleton", "Skeleton", ref skeleton);
                ui.Checkbox("health", "Health bar", ref health);
                ui.Checkbox("names", "Player name", ref names);
                ui.Checkbox("distance", "Distance", ref distance);
                ImGui.Separator();
                ui.Checkbox("team", "Ignore teammates", ref teamFilter);
                ui.Checkbox("visible", "Visible only", ref visibleOnly);
                ImGui.Spacing();
                ui.Slider("range", "Draw distance", ref range, 25, 500, "%.0f m");
                ui.Combo("style", "Box style", ref boxStyle, BoxStyles);
                ui.Slider("weight", "Line weight", ref thickness, 1, 3, "%.1f px");
                break;
            case 1:
                ui.Checkbox("crosshair", "Show crosshair", ref crosshair);
                ui.Slider("crosshair-size", "Size", ref crosshairSize, 3, 24, "%.0f px");
                ui.Slider("stroke", "Line weight", ref thickness, 1, 3, "%.1f px");
                break;
            case 2:
                ui.Checkbox("items", "Item markers", ref items);
                ui.Slider("world-range", "Draw distance", ref range, 25, 500, "%.0f m");
                ui.ColorPicker("item-color", "Marker color", ref hiddenColor);
                break;
            case 3:
                ui.InputText("name", "Profile name", ref profileName);
                ui.ColorPicker("accent", "Menu accent", ref playerColor);
                if (ui.Checkbox("motion", "Reduce motion", ref reducedMotion)) ui.ReducedMotion = reducedMotion;
                ImGui.Spacing();
                ui.MutedText("Profiles are stored in memory for this session.");
                break;
            case 4:
                ui.Toggle("particles", "Moving background", ref particles);
                ui.Toggle("gradient", "Header gradient", ref gradient);
                ui.Slider("speed", "Particle speed", ref particleSpeed, 0, 35, "%.0f");
                ui.Combo("fit", "Image fit", ref imageFit, ImageFits);
                if (ui.Toggle("reduced", "Reduce motion", ref reducedMotion)) ui.ReducedMotion = reducedMotion;
                ImGui.Spacing();
                ui.Text("Animated controls");
                ui.Checkbox("example-check", "Draw player name", ref names);
                ui.Toggle("example-toggle", "Draw bounding box", ref boxes);
                break;
        }
    }

    private void DrawPreview()
    {
        if (tab == 4) { DrawEffectsPreview(); return; }
        Caption(tab == 1 ? "CROSSHAIR PREVIEW" : "PLAYER PREVIEW");
        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (particles) ui.Particles(start, new(width, ui.S(241)), new ParticleOptions { Count = 24, Speed = particleSpeed, Color = ui.Theme.Accent with { W = .13f } });
        var center = start + new Vector2(width / 2, ui.S(135));
        var tint = C(playerColor);
        // A small schematic stage and a neutral mannequin. No game data or external process is read.
        for (var i = -3; i <= 3; i++)
            draw.AddLine(center + new Vector2(ui.S(i * 30), ui.S(85)), center + new Vector2(ui.S(i * 16), ui.S(56)), C(ui.Theme.Border with { W = .45f }));
        draw.AddLine(center + new Vector2(ui.S(-110), ui.S(85)), center + new Vector2(ui.S(110), ui.S(85)), C(ui.Theme.Border));
        if (tab == 1)
        {
            if (crosshair)
            {
                var length = ui.S(crosshairSize);
                draw.AddLine(center - new Vector2(length, 0), center + new Vector2(length, 0), tint, ui.S(thickness));
                draw.AddLine(center - new Vector2(0, length), center + new Vector2(0, length), tint, ui.S(thickness));
            }
        }
        else
        {
            var a = center + new Vector2(ui.S(-47), ui.S(-105));
            var b = center + new Vector2(ui.S(47), ui.S(65));
            var boneColor = skeleton ? tint : C(ui.Theme.Border);
            var head = center + new Vector2(0, ui.S(-78));
            draw.AddCircle(head, ui.S(12), boneColor, 24, ui.S(thickness));
            Bone(new(0, -65), new(0, 0)); Bone(new(-28, -48), new(28, -48));
            Bone(new(-28, -48), new(-35, -10)); Bone(new(28, -48), new(35, -10));
            Bone(new(0, 0), new(-23, 58)); Bone(new(0, 0), new(23, 58));
            if (boxes)
            {
                if (boxStyle == 1) draw.AddRect(a, b, tint, 0, ImDrawFlags.None, ui.S(thickness));
                else foreach (var corner in new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y) })
                {
                    var direction = new Vector2(corner.X == a.X ? 1 : -1, corner.Y == a.Y ? 1 : -1);
                    draw.AddLine(corner, corner + new Vector2(direction.X * ui.S(19), 0), tint, ui.S(thickness));
                    draw.AddLine(corner, corner + new Vector2(0, direction.Y * ui.S(19)), tint, ui.S(thickness));
                }
            }
            if (health)
            {
                draw.AddRectFilled(a - new Vector2(ui.S(9), 0), new Vector2(a.X - ui.S(5), b.Y), C(ui.Theme.Border));
                draw.AddRectFilled(new Vector2(a.X - ui.S(9), a.Y + ui.S(34)), new Vector2(a.X - ui.S(5), b.Y), tint);
            }
            if (names) draw.AddText(new(center.X - ImGui.CalcTextSize("player_01").X / 2, a.Y - ui.S(23)), C(ui.Theme.Text), "player_01");
            if (distance) draw.AddText(new(center.X - ui.S(14), b.Y + ui.S(8)), C(ui.Theme.Muted), "42 m");
            if (tab == 2 && items) draw.AddText(center + new Vector2(ui.S(58), ui.S(25)), C(hiddenColor), "+ supply");
            void Bone(Vector2 from, Vector2 to) => draw.AddLine(center + from * ui.Scale, center + to * ui.Scale, boneColor, ui.S(thickness));
        }
        ImGui.Dummy(new(width, ui.S(241)));
        ImGui.Separator();
        ui.ColorPicker("visible-color", "Visible", ref playerColor);
        ui.ColorPicker("hidden-color", "Occluded", ref hiddenColor);
    }

    private void DrawEffectsPreview()
    {
        Caption("LIVE PREVIEW");
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, ui.S(200));
        ui.Gradient(p, size, ui.Theme.Field, Vector4.Lerp(ui.Theme.Field, ui.Theme.Accent, .12f), vertical: true);
        if (particles) ui.Particles(p, size, new ParticleOptions { Count = 40, Speed = particleSpeed, Color = ui.Theme.Accent with { W = .42f }, ConnectionDistance = 75 });
        ImGui.GetWindowDrawList().AddCircle(p + size / 2, ui.S(24), C(ui.Theme.Accent with { W = ui.Pulse(3, .25f, .8f) }), 40, ui.S(1.5f));
        ImGui.Dummy(size);
        ImGui.Spacing();
        if (banner is { } image) ui.Image(image, new(size.X / ui.Scale, 73), (ImageFit)imageFit, rounding: 2);
        ImGui.Spacing();
        ui.MutedText("Image / RGBA texture");
    }

    private void Panel(string id, Vector2 position, Vector2 size, Action draw)
    {
        ImGui.SetCursorPos(position);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ui.S(15), ui.S(12)));
        var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.Borders | ImGuiChildFlags.AlwaysUseWindowPadding);
        try { if (visible) draw(); }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(); }
    }

    private void Caption(string title)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ui.Theme.Muted);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void Label(Vector2 position, string text, Vector4 color, bool small = false)
    {
        ImGui.SetCursorPos(position * ui.Scale);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        unsafe
        {
            var font = small ? ui.Fonts.Small : ui.Fonts.Heading;
            if (font.NativePtr != null) ImGui.PushFont(font);
            ImGui.TextUnformatted(text);
            if (font.NativePtr != null) ImGui.PopFont();
        }
        ImGui.PopStyleColor();
    }

    private static uint C(Vector4 value) => ImGui.GetColorU32(value);
    private void Save()
    {
        if (!string.IsNullOrWhiteSpace(profileName)) profiles[profile] = profileName;
        saved[profile] = new(boxes, skeleton, health, names, distance, teamFilter, visibleOnly,
            crosshair, items, range, thickness, crosshairSize, playerColor, hiddenColor, boxStyle);
    }
    private void Load()
    {
        if (!saved.TryGetValue(profile, out var p)) { feedback = "No saved profile"; return; }
        (boxes, skeleton, health, names, distance, teamFilter, visibleOnly, crosshair, items, range, thickness,
            crosshairSize, playerColor, hiddenColor, boxStyle) = (p.Boxes, p.Skeleton, p.Health, p.Names, p.Distance,
            p.TeamFilter, p.VisibleOnly, p.Crosshair, p.Items, p.Range, p.Thickness, p.CrosshairSize, p.Color, p.HiddenColor, p.BoxStyle);
        feedback = "Profile loaded";
    }
}
