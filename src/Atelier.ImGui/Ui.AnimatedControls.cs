using System.Numerics;
using ImGuiNET;

namespace Atelier;

public sealed partial class Ui
{
    /// <summary>A native checkbox with an animated fill and check stroke. IDs are independent of visible text.</summary>
    public bool Checkbox(string id, string label, ref bool value)
    {
        using var scope = Id(id);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Vector4.Zero);
        bool changed;
        try { changed = ImGui.Checkbox(label + "###value", ref value); }
        finally { ImGui.PopStyleColor(); }
        Record(id, label + "###value");
        var amount = Animate("check", value ? 1 : 0);
        var p = ImGui.GetItemRectMin();
        var side = ImGui.GetFrameHeight();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(p + new Vector2(1), p + new Vector2(side - 1), Color(Theme.Accent with { W = .12f * amount }), ImGui.GetStyle().FrameRounding);
        var a = p + new Vector2(side * .23f, side * .5f);
        var b = p + new Vector2(side * .43f, side * .7f);
        var c = p + new Vector2(side * .78f, side * .3f);
        var color = Color(Theme.Accent with { W = amount });
        if (amount > 0) draw.AddLine(a, Vector2.Lerp(a, b, Math.Min(1, amount * 2)), color, S(2));
        if (amount > .5f) draw.AddLine(b, Vector2.Lerp(b, c, amount * 2 - 1), color, S(2));
        return changed;
    }

    /// <summary>A native tab button with animated selection and hover tint. Size is in design units; caller owns selection.</summary>
    public bool Tab(string id, string label, bool selected, Vector2 size, bool drawIndicator = true)
    {
        using var scope = Id(id);
        var amount = Animate("selected", selected ? 1 : 0);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Lerp(Theme.Muted, Theme.Accent, amount));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        bool pressed;
        try { pressed = ImGui.Button(label + "###tab", size * Scale); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(4); }
        Record(id, label + "###tab");
        var hover = Animate("hover", ImGui.IsItemHovered() ? 1 : 0, .12f);
        var a = ImGui.GetItemRectMin();
        var b = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(a, b, Color(Theme.Accent with { W = hover * .045f }));
        if (drawIndicator && amount > 0)
            draw.AddLine(new(a.X + S(6), b.Y), new(b.X - S(6), b.Y), Color(Theme.Accent with { W = amount }), S(2));
        return pressed;
    }
}
