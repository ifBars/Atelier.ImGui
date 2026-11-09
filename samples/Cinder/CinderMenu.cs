using System.Numerics;
using Atelier;
using ImGuiNET;
using StyledMenus;

namespace Cinder;

/// <summary>A compact classic menu. All values and profile slots are local to this sample.</summary>
public sealed class CinderMenu : StyledMenuBase
{
    private sealed class Settings
    {
        public bool Enabled = true, Box = true, Name = true, Health = true, Skeleton, Weapon, Distance = true;
        public bool Team, VisibleOnly, Model = true, Occluded, Fog = true, Night, Props = true, ReducedMotion;
        public float Range = 250, Opacity = 85, ModelOpacity = 65, Fov = 90, PropOpacity = 80;
        public int BoxStyle, Material;
        public Vector4 Accent = UiTheme.Rgb(0xC6A66D), Visible = UiTheme.Rgb(0xC6A66D), Hidden = UiTheme.Rgb(0x8C9CA7);
        public Settings Copy() => (Settings)MemberwiseClone();
    }
    private Settings state = new();
    private readonly Dictionary<int, Settings> profiles = [];
    private static readonly string[] Pages = ["Players", "World", "Config"];
    private static readonly string[] PageLabels = ["Players", "World & view", "Config"];
    private static readonly string[] BoxStyles = ["Corners", "Full"];
    private static readonly string[] Materials = ["Flat", "Shaded", "Wireframe"];
    private readonly string[] profileNames = ["Default", "Practice", "Capture"];
    private int page, slot;
    private string profileName = "Default";
    private string notice = "";
    private static readonly Vector4 Outer = UiTheme.Rgb(0x090A0B);
    private static readonly Vector4 GroupFill = UiTheme.Rgb(0x1B1B1E);
    protected override float Rounding => 0;
    public static Vector2 CanvasSize => new(690, 568);
    public bool OverlayEnabled => state.Enabled;
    public bool BoundingBoxEnabled => state.Box;
    public float RenderDistance => state.Range;
    public int ProfileSlot => slot;
    public string SelectedPage { get => Pages[page]; set => page = Array.IndexOf(Pages, value) is var i && i >= 0 ? i : throw new ArgumentException("Unknown page."); }

    public CinderMenu(Ui ui) : base(ui, default, "##cinder", CanvasSize)
    {
        ui.Theme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0x222226),
            Surface = GroupFill,
            Field = UiTheme.Rgb(0x141416),
            SurfaceHover = UiTheme.Rgb(0x303035),
            Border = UiTheme.Rgb(0x414146),
            Muted = UiTheme.Rgb(0x909094),
            Text = UiTheme.Rgb(0xD4D3D1),
            Accent = state.Accent,
            AccentHover = UiTheme.Rgb(0xD6BD93),
            OnAccent = UiTheme.Rgb(0x171717)
        };
    }

    public static void ConfigureFonts(Ui ui)
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(path, "tahoma.ttf");
        var bold = Path.Combine(path, "tahomabd.ttf");
        if (!File.Exists(regular)) return;
        var fonts = ImGui.GetIO().Fonts;
        ui.Fonts = new(fonts.AddFontFromFileTTF(regular, 13 * ui.Scale),
            fonts.AddFontFromFileTTF(File.Exists(bold) ? bold : regular, 17 * ui.Scale), fonts.AddFontFromFileTTF(regular, 12 * ui.Scale));
    }

    protected override void Paint()
    {
        Ui.Theme = Ui.Theme with { Accent = state.Accent };
        Ui.ReducedMotion = state.ReducedMotion;
        var t = Ui.Theme;
        // Fine bevels and inset headers preserve the classic style without crossing text and borders.
        Rect(1, 1, 688, 566, Outer);
        Rect(4, 4, 682, 560, t.Border);
        Rect(5, 5, 680, 558, t.Background);
        for (var y = 9; y < 559; y += 3) Line(8, y, 682, y, Vector4.One with { W = .006f });
        Ui.Gradient(P(8, 8), new Vector2(674, 2) * Ui.Scale, state.Accent, state.Accent with { X = .47f, Y = .52f, Z = .58f });
        Text(20, 20, "cinder", 17, t.Text, display: true);
        Line(18, 49, 672, 49, Outer);
        Line(18, 50, 672, 50, t.Border with { W = .5f });
        Tabs();

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(Ui.S(7), (Ui.S(23) - ImGui.GetFontSize()) / 2));
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Ui.S(7), Ui.S(4)));
        try
        {
            if (page == 0) Players();
            else if (page == 1) World();
            else Config();
            if (Combo("profile.slot", ref slot, profileNames, 18, 523, 270)) profileName = profileNames[slot];
            Button("load", "Load", 300, Load);
            Button("save", "Save", 428, Save);
            Button("reset", "Reset", 556, () => { state = new(); notice = "Reset"; });
            if (notice.Length > 0) Label(573, 23, notice, t.Muted);
        }
        finally { ImGui.PopStyleVar(5); }
    }

    private void Players()
    {
        Group("Player ESP", 18, 99, 300, 244);
        Check("enabled", "Enabled", ref state.Enabled, 34, 134);
        using (Ui.Disabled(!state.Enabled))
        {
            Check("box", "Bounding box", ref state.Box, 34, 158); Chip("box.color", ref state.Visible, 278, 158);
            Check("name", "Name", ref state.Name, 34, 182);
            Check("health", "Health bar", ref state.Health, 34, 206);
            Check("skeleton", "Skeleton", ref state.Skeleton, 34, 230);
            Check("weapon", "Weapon", ref state.Weapon, 34, 254);
            Check("distance", "Distance", ref state.Distance, 34, 278);
            Label(34, 306, "Box style");
            Combo("box.style", ref state.BoxStyle, BoxStyles, 135, 301, 166);
        }
        Group("Filter", 18, 359, 300, 145);
        Check("team", "Teammates", ref state.Team, 34, 394);
        Check("visible", "Visible only", ref state.VisibleOnly, 34, 418);
        MiniSlider("range", "Draw distance", ref state.Range, 50, 500, 34, 453, 267, "m");
        Group("Preview", 334, 99, 338, 244);
        Preview();
        Group("Materials", 334, 359, 338, 145);
        Check("model", "Colored model", ref state.Model, 350, 394); Chip("model.color", ref state.Visible, 631, 394);
        Check("occluded", "Occluded", ref state.Occluded, 350, 418); Chip("hidden.color", ref state.Hidden, 631, 418);
        Combo("material", ref state.Material, Materials, 350, 438, 305);
        MiniSlider("model.opacity", "Opacity", ref state.ModelOpacity, 0, 100, 350, 468, 305, "%");
    }

    private void World()
    {
        Group("Environment", 18, 99, 300, 244);
        Check("fog", "Fog", ref state.Fog, 34, 134);
        Check("night", "Night mode", ref state.Night, 34, 162);
        Check("props", "Props", ref state.Props, 34, 190);
        MiniSlider("props.opacity", "Prop opacity", ref state.PropOpacity, 0, 100, 34, 234, 267, "%");
        MiniSlider("world.range", "Draw distance", ref state.Range, 50, 500, 34, 288, 267, "m");
        Group("View", 18, 359, 300, 145);
        MiniSlider("fov", "Field of view", ref state.Fov, 60, 120, 34, 394, 267, "°");
        MiniSlider("opacity", "Overlay opacity", ref state.Opacity, 0, 100, 34, 453, 267, "%");
        Group("Preview", 334, 99, 338, 405);
        Preview(55);
    }

    private void Config()
    {
        Group("Profile", 18, 99, 300, 244);
        Label(34, 134, "Name");
        ImGui.SetCursorPos(new Vector2(34, 155) * Ui.Scale);
        ImGui.SetNextItemWidth(Ui.S(267));
        ImGui.InputText("##name", ref profileName, 24);
        Remember("profile.name");
        Label(34, 198, "Slot");
        if (Combo("slot", ref slot, profileNames, 34, 222, 267)) profileName = profileNames[slot];
        Group("Menu", 334, 99, 338, 244);
        Label(350, 134, "Accent"); Chip("accent", ref state.Accent, 631, 132);
        Check("motion", "Reduce motion", ref state.ReducedMotion, 350, 168);
        Group("Style", 334, 359, 338, 145);
        Check("sample.box", "Bounding box", ref state.Box, 350, 394);
        MiniSlider("sample.opacity", "Opacity", ref state.Opacity, 0, 100, 350, 446, 305, "%");
    }

    private void Group(string title, float x, float y, float width, float height)
    {
        Rect(x, y, width, height, Outer);
        Rect(x + 1, y + 1, width - 2, height - 2, Ui.Theme.Border with { W = .65f });
        Rect(x + 2, y + 2, width - 4, height - 4, GroupFill);
        Rect(x + 2, y + 2, width - 4, 23, Vector4.Lerp(GroupFill, Ui.Theme.Text, .02f));
        Line(x + 2, y + 26, x + width - 2, y + 26, Outer with { W = .7f });
        Label(x + 16, y + 8, title);
    }

    private unsafe void Label(float x, float y, string text, Vector4? color = null)
    {
        var font = Ui.Fonts.Body;
        if (font.NativePtr == null) font = ImGui.GetFont();
        ImGui.GetWindowDrawList().AddText(font, ImGui.GetFontSize(), P(x, y), C(color ?? Ui.Theme.Text), text);
    }

    private void Tabs()
    {
        const float tabWidth = 218;
        Rect(18, 60, tabWidth * 3, 27, Outer);
        Rect(19, 61, tabWidth * 3 - 2, 25, GroupFill);
        for (var i = 0; i < Pages.Length; i++)
        {
            var x = 18 + i * tabWidth;
            if (Hit("tab." + Pages[i], new(x, 60), new(tabWidth, 27))) page = i;
            var hover = ImGui.IsItemHovered();
            var focused = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
            if (page == i || hover) Rect(x + 1, 61, tabWidth - 2, 25, page == i ? Ui.Theme.SurfaceHover : Ui.Theme.Text with { W = .035f });
            if (i > 0) Line(x, 61, x, 86, Outer);
            var textSize = ImGui.CalcTextSize(PageLabels[i]) / Ui.Scale;
            Label(x + (tabWidth - textSize.X) / 2, 60 + (27 - textSize.Y) / 2, PageLabels[i], page == i ? Ui.Theme.Accent : Ui.Theme.Muted);
            if (page == i) Rect(x + 1, 85, tabWidth - 2, 1, Ui.Theme.Accent);
            if (focused) ImGui.GetWindowDrawList().AddRect(P(x + 2, 62), P(x + tabWidth - 2, 85), C(Ui.Theme.Accent));
        }
    }

    private void Check(string id, string label, ref bool value, float x, float y)
    {
        ImGui.SetCursorPos(new Vector2(x, y) * Ui.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        try { Ui.Checkbox(id, label, ref value); }
        finally { ImGui.PopStyleVar(); }
        Remember(id);
    }

    private bool Combo(string id, ref int value, string[] choices, float x, float y, float width)
    {
        ImGui.SetCursorPos(new Vector2(x, y) * Ui.Scale);
        var pixelWidth = MathF.Floor(Ui.S(width));
        ImGui.SetNextItemWidth(pixelWidth);
        var draw = ImGui.GetWindowDrawList();
        var position = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var open = ImGui.BeginCombo("##" + id, choices[value], ImGuiComboFlags.NoArrowButton);
        // BeginCombo may switch the current window to its popup. Keep the parent frame's bounds.
        Remember(id, new MenuBounds(position, position + new Vector2(pixelWidth, height)));
        var hovered = ImGui.IsItemHovered();
        var changed = false;
        if (open)
        {
            try
            {
                for (var i = 0; i < choices.Length; i++)
                {
                    var selected = value == i;
                    ImGui.PushID(i);
                    try
                    {
                        if (ImGui.Selectable(choices[i], selected)) { value = i; changed = true; }
                        if (selected) ImGui.SetItemDefaultFocus();
                    }
                    finally { ImGui.PopID(); }
                }
            }
            finally { ImGui.EndCombo(); }
        }
        var right = position.X + pixelWidth;
        draw.AddRectFilled(new(right - height, position.Y + 1), new(right - 1, position.Y + height - 1), C(hovered || open ? Ui.Theme.SurfaceHover : Ui.Theme.Surface));
        draw.AddLine(new(right - height, position.Y + 1), new(right - height, position.Y + height - 1), C(Outer));
        var center = new Vector2(right - height / 2, position.Y + height / 2);
        var chevron = C(Ui.Theme.Muted);
        var direction = open ? -1 : 1;
        draw.AddLine(center + new Vector2(Ui.S(-3), Ui.S(-1.5f * direction)), center + new Vector2(0, Ui.S(1.5f * direction)), chevron, Ui.S(1));
        draw.AddLine(center + new Vector2(0, Ui.S(1.5f * direction)), center + new Vector2(Ui.S(3), Ui.S(-1.5f * direction)), chevron, Ui.S(1));
        return changed;
    }

    private void Chip(string id, ref Vector4 color, float x, float y)
    {
        ImGui.SetCursorPos(new Vector2(x, y) * Ui.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        try { ImGui.ColorEdit4("##" + id, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar); }
        finally { ImGui.PopStyleVar(); }
        Remember(id);
    }

    private void MiniSlider(string id, string label, ref float value, float min, float max, float x, float y, float width, string unit)
    {
        Label(x, y, label);
        ImGui.SetCursorPos(new Vector2(x, y + 17) * Ui.Scale);
        ImGui.SetNextItemWidth(Ui.S(width));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        try { ImGui.SliderFloat("##" + id, ref value, min, max, "%.0f", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.NoInput); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(6); }
        Remember(id);
        Rect(x, y + 24, width, 5, Outer);
        Rect(x + 1, y + 25, width - 2, 3, Ui.Theme.Border);
        var fraction = Math.Clamp((value - min) / (max - min), 0, 1);
        if (fraction > 0) Ui.Gradient(P(x + 1, y + 25), new Vector2((width - 2) * fraction, 3) * Ui.Scale,
            Ui.Theme.Accent, Ui.Theme.Accent with { X = Ui.Theme.Accent.X * .68f, Y = Ui.Theme.Accent.Y * .68f, Z = Ui.Theme.Accent.Z * .68f });
        Label(x + width - 47, y - 1, $"{value:0}{unit}", Ui.Theme.Muted);
    }

    private void Button(string id, string label, float x, Action action)
    {
        ImGui.SetCursorPos(new Vector2(x, 523) * Ui.Scale);
        if (ImGui.Button(label + "###" + id, new Vector2(116, 23) * Ui.Scale)) action();
        Remember(id);
    }

    private void Preview(float offset = 0)
    {
        var dl = ImGui.GetWindowDrawList();
        var accent = state.Visible with { W = state.Opacity / 100 };
        var cx = 505f;
        var top = 151 + offset;
        Vector2 V(float x, float y) => P(cx + x, top + y);
        var body = state.Model ? Vector4.Lerp(GroupFill, state.Visible, state.ModelOpacity / 100 * .45f) : UiTheme.Rgb(0x3C3E42);
        if (state.Night) Rect(345, 128, 316, page == 1 ? 361 : 203, UiTheme.Rgb(0x101113));
        if (state.Fog && page == 1) Ui.Gradient(P(347, 300), new Vector2(312, 177) * Ui.Scale, Vector4.Zero, new Vector4(.22f, .23f, .25f, .22f), vertical: true);
        if (page != 1 || state.Props)
            for (var i = -3; i <= 3; i++) Line(cx + i * 24, top + 157, cx + i * 10, top + 131, Ui.Theme.Border with { W = .35f });
        if (state.Enabled && state.Name) Label(cx - 18, top - 20, "player");
        var outline = state.Model ? accent : Ui.Theme.Muted;
        Poly([new(-14, 30), new(14, 30), new(24, 43), new(17, 89), new(-17, 89), new(-24, 43)], body, outline);
        Poly([new(-14, 93), new(-1, 93), new(-3, 139), new(-18, 139)], body, outline);
        Poly([new(1, 93), new(14, 93), new(18, 139), new(3, 139)], body, outline);
        Poly([new(-25, 37), new(-35, 43), new(-34, 83), new(-25, 89), new(-22, 65)], body, outline);
        Poly([new(25, 37), new(35, 43), new(34, 83), new(25, 89), new(22, 65)], body, outline);
        Rect(cx - 20, top + 139, 18, 8, UiTheme.Rgb(0x242529)); Rect(cx + 2, top + 139, 18, 8, UiTheme.Rgb(0x242529));
        dl.AddCircleFilled(V(0, 15), Ui.S(12), C(body), 24);
        dl.AddCircle(V(0, 15), Ui.S(12), C(outline), 24);
        Line(cx - 10, top + 12, cx + 10, top + 12, outline);
        // Armor seams keep the figure readable without relying on a game asset.
        Line(cx - 14, top + 43, cx + 14, top + 43, outline with { W = .45f });
        Line(cx, top + 43, cx, top + 78, outline with { W = .45f });
        Line(cx - 15, top + 77, cx + 15, top + 77, outline with { W = .45f });
        if (state.Model && state.Occluded && !state.VisibleOnly)
            Poly([new(2, 45), new(13, 45), new(13, 74), new(2, 74)], state.Hidden with { W = .35f }, state.Hidden);
        if (state.Enabled)
        {
            if (state.Box)
            {
                if (state.BoxStyle == 1) dl.AddRect(V(-44, -3), V(44, 151), C(accent));
                else foreach (var c in new[] { new Vector2(-44, -3), new(44, -3), new(44, 151), new(-44, 151) })
                {
                    var a = c.X < 0 ? 1 : -1; var b = c.Y < 0 ? 1 : -1;
                    dl.AddLine(V(c.X, c.Y), V(c.X + a * 11, c.Y), C(accent));
                    dl.AddLine(V(c.X, c.Y), V(c.X, c.Y + b * 11), C(accent));
                }
            }
            if (state.Health) { Rect(cx - 52, top - 3, 3, 154, Outer); Rect(cx - 51, top + 28, 1, 122, state.Visible); }
            if (state.Distance) Label(cx - 14, top + 158, "24m", Ui.Theme.Muted);
            if (state.Weapon) Label(cx - 13, top + 174, "rifle", Ui.Theme.Muted);
            if (state.Skeleton)
            {
                Line(cx, top + 28, cx, top + 88, Ui.Theme.Text);
                Line(cx - 31, top + 75, cx, top + 36, Ui.Theme.Text); Line(cx + 31, top + 75, cx, top + 36, Ui.Theme.Text);
                Line(cx - 10, top + 135, cx, top + 88, Ui.Theme.Text); Line(cx + 10, top + 135, cx, top + 88, Ui.Theme.Text);
            }
        }
        void Poly(Vector2[] points, Vector4 fill, Vector4 stroke)
        {
            var transformed = points.Select(point => V(point.X, point.Y)).ToArray();
            if (state.Material != 2) dl.AddConvexPolyFilled(ref transformed[0], transformed.Length, C(fill));
            dl.AddPolyline(ref transformed[0], transformed.Length, C(stroke), ImDrawFlags.Closed, Ui.S(1));
            if (state.Material == 1) dl.AddLine(transformed[0], transformed[transformed.Length / 2], C(stroke with { W = .3f }));
        }
    }

    private void Save() { profiles[slot] = state.Copy(); profileNames[slot] = profileName; notice = "Saved"; }
    private void Load() { if (profiles.TryGetValue(slot, out var saved)) { state = saved.Copy(); notice = "Loaded"; } else notice = "Empty"; }
}
