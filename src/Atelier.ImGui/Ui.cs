using System.Numerics;
using ImGuiNET;

namespace Atelier;

/// <summary>
/// Immediate-mode components. Create one instance per ImGui context and use it on that context's thread.
/// Values belong to the caller; IDs belong to the current ImGui ID scope.
/// </summary>
public sealed partial class Ui
{

    public Ui(UiTheme? theme = null, float scale = 1)
    {
        if (!float.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));
        Theme = theme ?? UiTheme.Graphite;
        Scale = scale;
    }

    public UiTheme Theme { get; set; }
    public UiFonts Fonts { get; set; }
    public float Scale { get; }
    public bool ReducedMotion { get; set; }
    /// <summary>Available width in design units below which setting rows stack. Zero keeps compact rows inline.</summary>
    public float StackFieldsBelow { get; set; } = 480;
    /// <summary>Optional observation hook for tests and tools. Bounds use ImGui screen coordinates.</summary>
    public event Action<UiItem>? ItemDrawn;
    public float S(float value) => value * Scale;
    internal static uint Color(Vector4 value) => ImGui.GetColorU32(value);

    /// <summary>Scopes all theme changes; disposing restores the host's style and font.</summary>
    public IDisposable Style() => new StyleScope(this);

    public void Heading(string title, string? description = null)
    {
        using (Font(Fonts.Heading)) ImGui.TextUnformatted(title);
        if (!string.IsNullOrEmpty(description)) MutedText(description);
        ImGui.Dummy(new Vector2(0, S(8)));
    }

    public void Text(string text) => ImGui.TextWrapped(text);

    public void MutedText(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Muted);
        try { ImGui.TextWrapped(text); }
        finally { ImGui.PopStyleColor(); }
    }

    /// <summary>Draws an automatically sized, padded group. Child IDs are scoped to this section.</summary>
    public void Section(string id, string title, Action<Ui> draw, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ImGui.PushID(id);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(20), S(16)));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.Surface);
        var visible = ImGui.BeginChild("##section", Vector2.Zero,
            ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        try
        {
            if (!visible) return;
            ImGui.TextUnformatted(title);
            if (!string.IsNullOrEmpty(description)) MutedText(description);
            ImGui.Spacing();
            draw(this);
        }
        finally
        {
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
            ImGui.PopID();
        }
        ImGui.Dummy(new Vector2(0, S(6)));
    }

    public void Divider()
    {
        ImGui.Separator();
    }

    public bool Button(string id, string label, ButtonKind kind = ButtonKind.Secondary, float width = 0)
    {
        var primary = kind == ButtonKind.Primary;
        var background = primary ? Theme.Accent : Theme.SurfaceHover;
        var foreground = primary ? Theme.OnAccent : kind == ButtonKind.Danger ? Theme.Danger : Theme.Text;
        ImGui.PushID(id);
        ImGui.PushStyleColor(ImGuiCol.Button, background);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, primary ? Theme.AccentHover : Vector4.Lerp(background, Theme.Text, .08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Lerp(background, Theme.Text, .15f));
        ImGui.PushStyleColor(ImGuiCol.Text, foreground);
        try
        {
            var changed = ImGui.Button(label + "###button", new Vector2(S(width), S(36)));
            Record(id, label + "###button");
            return changed;
        }
        finally { ImGui.PopStyleColor(4); ImGui.PopID(); }
    }

    public void Badge(string text, Vector4? color = null)
    {
        var tint = color ?? Theme.Accent;
        var position = ImGui.GetCursorScreenPos();
        var size = ImGui.CalcTextSize(text) + new Vector2(S(20), S(10));
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(position, position + size, Color(tint with { W = .10f }), S(5));
        draw.AddText(position + new Vector2(S(10), S(5)), Color(tint), text);
        ImGui.Dummy(size);
    }

    private float Animate(uint id, bool target)
        => AnimateValue(id, target ? 1 : 0, .18f, Easing.CubicOut);

    internal IDisposable Font(ImFontPtr font) => new FontScope(font);

    private void Record(string name, string nativeId = "##value")
    {
        if (ItemDrawn is { } record)
            record(new UiItem(ImGui.GetID(nativeId), name, ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));
    }

    private sealed unsafe class FontScope : IDisposable
    {
        private readonly bool pushed;
        public FontScope(ImFontPtr font)
        {
            pushed = font.NativePtr != null;
            if (pushed) ImGui.PushFont(font);
        }
        public void Dispose() { if (pushed) ImGui.PopFont(); }
    }

    private sealed class StyleScope : IDisposable
    {
        private readonly IDisposable font;
        private bool disposed;
        public StyleScope(Ui ui)
        {
            var t = ui.Theme;
            font = ui.Font(ui.Fonts.Body);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ui.S(24)));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ui.S(12), ui.S(8)));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ui.S(12), ui.S(10)));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(ui.S(8)));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, ui.S(12));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, ui.S(8));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ui.S(5));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, ui.S(7));
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, ui.S(4));
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, ui.S(7));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
            Set(ImGuiCol.Text, t.Text); Set(ImGuiCol.TextDisabled, t.Muted);
            Set(ImGuiCol.WindowBg, t.Background); Set(ImGuiCol.ChildBg, t.Surface);
            Set(ImGuiCol.PopupBg, t.Surface); Set(ImGuiCol.Border, t.Border);
            Set(ImGuiCol.FrameBg, t.Field); Set(ImGuiCol.FrameBgHovered, t.SurfaceHover);
            Set(ImGuiCol.FrameBgActive, t.SurfaceHover);
            Set(ImGuiCol.Button, t.SurfaceHover); Set(ImGuiCol.ButtonHovered, t.Border);
            Set(ImGuiCol.ButtonActive, t.Border); Set(ImGuiCol.CheckMark, t.Accent);
            Set(ImGuiCol.SliderGrab, t.Accent); Set(ImGuiCol.SliderGrabActive, t.AccentHover);
            Set(ImGuiCol.Header, t.SurfaceHover); Set(ImGuiCol.HeaderHovered, t.Border);
            Set(ImGuiCol.HeaderActive, t.Border); Set(ImGuiCol.Separator, t.Border);
            Set(ImGuiCol.ScrollbarBg, Vector4.Zero); Set(ImGuiCol.ScrollbarGrab, t.Border);
            Set(ImGuiCol.ScrollbarGrabHovered, t.Muted); Set(ImGuiCol.ScrollbarGrabActive, t.Muted);
            Set(ImGuiCol.TextSelectedBg, t.Accent with { W = .25f });
            Set(ImGuiCol.NavCursor, t.Accent); Set(ImGuiCol.ResizeGrip, t.Border);
            Set(ImGuiCol.ResizeGripHovered, t.Accent); Set(ImGuiCol.ResizeGripActive, t.Accent);
        }
        private static void Set(ImGuiCol index, Vector4 color) => ImGui.PushStyleColor(index, color);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ImGui.PopStyleColor(28);
            ImGui.PopStyleVar(13);
            font.Dispose();
        }
    }
}
