using System.Numerics;
using Atelier;
using ImGuiNET;

namespace StyledMenus;

public readonly record struct MenuBounds(Vector2 Minimum, Vector2 Maximum)
{
    public Vector2 Center => (Minimum + Maximum) / 2;
}

/// <summary>Sample-only drawing helpers shared by two art-directed layouts. No native host dependency.</summary>
public abstract class StyledMenuBase : IMenuSurface
{
    protected readonly Ui Ui;
    protected readonly UiImage Artwork;
    protected Vector2 Origin;
    protected readonly Vector2 DesignSize;
    private readonly string id;
    private readonly Dictionary<string, MenuBounds> bounds = [];
    public IReadOnlyDictionary<string, MenuBounds> Items => bounds;
    public bool IsOpen { get; protected set; } = true;
    public event Action? DragStarted;
    public string Feedback { get; protected set; } = "Local preview";
    protected abstract float Rounding { get; }
    protected abstract void Paint();

    protected StyledMenuBase(Ui ui, UiImage artwork, string id, Vector2 size)
        => (Ui, Artwork, this.id, DesignSize) = (ui, artwork, id, size);

    public void Draw(bool fillViewport = false)
    {
        if (!IsOpen) return;
        bounds.Clear();
        using var style = Ui.Style();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Ui.S(Rounding));
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(fillViewport ? viewport.Size : DesignSize * Ui.Scale,
            fillViewport ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(fillViewport ? viewport.Pos : viewport.WorkPos + (viewport.WorkSize - DesignSize * Ui.Scale) / 2,
            fillViewport ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        var visible = ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove);
        try
        {
            if (!visible) return;
            Origin = ImGui.GetWindowPos();
            Paint();
            Hit("window.drag", new(210, 8), new(DesignSize.X - 280, 48));
            if (ImGui.IsItemActivated()) DragStarted?.Invoke();
            if (!fillViewport && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(Origin + ImGui.GetIO().MouseDelta);
            if (Hit("window.close", new(DesignSize.X - 46, 20), new(26, 26))) IsOpen = false;
            var close = Origin + new Vector2(Ui.S(DesignSize.X - 38), Ui.S(28));
            var dl = ImGui.GetWindowDrawList();
            dl.AddLine(close, close + new Vector2(Ui.S(10)), C(Ui.Theme.Muted), Ui.S(1.5f));
            dl.AddLine(close + new Vector2(Ui.S(10), 0), close + new Vector2(0, Ui.S(10)), C(Ui.Theme.Muted), Ui.S(1.5f));
        }
        finally { ImGui.End(); ImGui.PopStyleVar(2); }
    }

    protected Vector2 P(float x, float y) => Origin + new Vector2(x, y) * Ui.Scale;
    protected static uint C(Vector4 color) => ImGui.GetColorU32(color);
    protected void Rect(float x, float y, float w, float h, Vector4 color, float rounding = 0)
        => ImGui.GetWindowDrawList().AddRectFilled(P(x, y), P(x + w, y + h), C(color), Ui.S(rounding));
    protected void Line(float x, float y, float x2, float y2, Vector4 color, float thickness = 1)
        => ImGui.GetWindowDrawList().AddLine(P(x, y), P(x2, y2), C(color), Ui.S(thickness));

    protected unsafe void Text(float x, float y, string text, float size = 16, Vector4? color = null, bool display = false)
    {
        var font = display ? Ui.Fonts.Heading : size <= 13 ? Ui.Fonts.Small : Ui.Fonts.Body;
        if (font.NativePtr == null) font = ImGui.GetFont();
        ImGui.GetWindowDrawList().AddText(font, Ui.S(size), P(x, y), C(color ?? Ui.Theme.Text), text);
    }

    protected bool Hit(string key, Vector2 position, Vector2 size)
    {
        ImGui.SetCursorPos(position * Ui.Scale);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        bool pressed;
        try { pressed = ImGui.Button("##" + key, size * Ui.Scale); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(3); }
        Remember(key);
        if (ImGui.GetIO().NavVisible && ImGui.IsItemFocused())
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), C(Ui.Theme.Accent), Ui.S(3));
        return pressed;
    }

    protected void Remember(string key) => bounds[key] = new(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
    protected void Remember(string key, MenuBounds itemBounds) => bounds[key] = itemBounds;

    protected bool Switch(string id, string label, ref bool value, float x, float y, float width, string? detail = null, bool textSwitch = false)
    {
        var pressed = Hit(id, new(x, y), new(width, detail is null ? 42 : 58));
        if (pressed) { value = !value; Feedback = "Unsaved changes"; }
        var hover = ImGui.IsItemHovered();
        var amount = Ui.Animate(id + ":on", value ? 1 : 0);
        if (hover) Rect(x - 8, y, width + 16, detail is null ? 42 : 58, Ui.Theme.Text with { W = .035f }, 4);
        Text(x, y + 8, label, 16);
        if (detail is not null) Text(x, y + 31, detail, 12, Ui.Theme.Muted);
        if (textSwitch)
        {
            Rect(x + width - 63, y + 4, 63, 30, value ? Ui.Theme.Accent : Ui.Theme.Muted);
            Text(x + width - 49, y + 10, value ? "ON" : "OFF", 16, Vector4.One, display: true);
        }
        else
        {
            Rect(x + width - 34, y + 10, 34, 18, Vector4.Lerp(Ui.Theme.Border, Ui.Theme.Accent, amount), 9);
            ImGui.GetWindowDrawList().AddCircleFilled(P(x + width - 25 + amount * 16, y + 19), Ui.S(5.5f),
                C(Vector4.Lerp(Ui.Theme.Text, Ui.Theme.OnAccent, amount)), 24);
        }
        return pressed;
    }

    protected void Slider(string id, string label, ref float value, float min, float max, float x, float y, float width, string suffix)
    {
        Text(x, y, label, 15);
        Text(x + width - 54, y, $"{value:0}{suffix}", 13, Ui.Theme.Accent);
        ImGui.SetCursorPos(new Vector2(x, y + 23) * Ui.Scale);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        ImGui.SetNextItemWidth(Ui.S(width));
        bool changed;
        try { changed = ImGui.SliderFloat("##" + id, ref value, min, max, "%.0f", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.NoInput); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(6); }
        Remember(id);
        if (changed) Feedback = "Unsaved changes";
        var fraction = Math.Clamp((value - min) / (max - min), 0, 1);
        var right = x + 5 + (width - 10) * fraction;
        Rect(x + 5, y + 38, width - 10, 3, Ui.Theme.Border, 2);
        Rect(x + 5, y + 38, (width - 10) * fraction, 3, Ui.Theme.Accent, 2);
        ImGui.GetWindowDrawList().AddCircleFilled(P(right, y + 39), Ui.S(5), C(Ui.Theme.Accent), 24);
    }

    protected void Color(string id, string label, ref Vector4 value, float x, float y, float width)
    {
        Text(x, y + 4, label, 15);
        ImGui.SetCursorPos(new Vector2(x + width - 28, y) * Ui.Scale);
        ImGui.ColorEdit4("##" + id, ref value, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        Remember(id);
    }

    protected void Action(string id, string label, Vector2 position, Vector2 size, bool primary, System.Action action)
    {
        var pressed = Hit(id, position, size);
        var hover = ImGui.IsItemHovered();
        Rect(position.X, position.Y, size.X, size.Y, primary ? Ui.Theme.Accent : hover ? Ui.Theme.SurfaceHover : Ui.Theme.Surface, 3);
        Text(position.X + 18, position.Y + 12, label, primary ? 20 : 15, primary ? Ui.Theme.OnAccent : Ui.Theme.Text, display: primary);
        if (ImGui.GetIO().NavVisible && ImGui.IsItemFocused())
            ImGui.GetWindowDrawList().AddRect(Origin + position * Ui.Scale + new Vector2(2), Origin + (position + size) * Ui.Scale - new Vector2(2), C(Ui.Theme.Accent), Ui.S(2));
        if (pressed) action();
    }

    /// <summary>Called by the host before atlas upload. Existing hosts can retain their own fonts instead.</summary>
    public static void ConfigureDisplayFont(Ui ui, string path)
    {
        var font = ImGui.GetIO().Fonts.AddFontFromFileTTF(path, 64 * ui.Scale);
        ui.Fonts = ui.Fonts with { Heading = font };
    }
}
