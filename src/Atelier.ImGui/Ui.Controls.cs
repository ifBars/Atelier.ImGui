using System.Numerics;
using ImGuiNET;

namespace Atelier;

public sealed partial class Ui
{
    /// <summary>A native button supplies mouse, keyboard, focus and disabled behavior; the switch is custom drawn.</summary>
    public bool Toggle(string id, string label, ref bool value, string? description = null)
    {
        using var field = new FieldScope(this, id, label, description);
        if (!field.Visible) return false;
        var size = new Vector2(S(42), S(24));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - size.X));
        var position = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        var pressed = ImGui.Button("##value", size);
        ImGui.PopStyleColor(3);
        Record(id);
        if (pressed) value = !value;
        var amount = Animate(ImGui.GetID("##value"), value);
        var draw = ImGui.GetWindowDrawList();
        var track = Vector4.Lerp(Theme.Border, Theme.Accent, amount);
        if (ImGui.IsItemHovered()) track = Vector4.Lerp(track, Theme.Text, .1f);
        draw.AddRectFilled(position, position + size, Color(track), size.Y / 2);
        var center = position + new Vector2(S(12 + 18 * amount), S(12));
        draw.AddCircleFilled(center, S(8), Color(Vector4.Lerp(Theme.Text, Theme.OnAccent, amount)), 24);
        if (ImGui.IsItemFocused())
            draw.AddRect(position - new Vector2(S(3)), position + size + new Vector2(S(3)), Color(Theme.Accent), S(15));
        return pressed;
    }

    public bool Slider(string id, string label, ref float value, float minimum, float maximum,
        string format = "%.0f", string? description = null)
    {
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum >= maximum)
            throw new ArgumentException("The slider requires finite bounds with minimum < maximum.");
        using var field = new FieldScope(this, id, label, description);
        if (!field.Visible) return false;
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.SliderFloat("##value", ref value, minimum, maximum, format, ImGuiSliderFlags.AlwaysClamp);
        Record(id);
        return changed;
    }

    public bool InputText(string id, string label, ref string value, uint maximumLength = 256, string? description = null)
    {
        using var field = new FieldScope(this, id, label, description);
        if (!field.Visible) return false;
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.InputText("##value", ref value, maximumLength);
        Record(id);
        return changed;
    }

    public bool Combo(string id, string label, ref int selected, string[] items, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length == 0 || selected < 0 || selected >= items.Length)
            throw new ArgumentOutOfRangeException(nameof(selected), "Select an item from a nonempty collection.");
        using var field = new FieldScope(this, id, label, description);
        if (!field.Visible) return false;
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.Combo("##value", ref selected, items, items.Length);
        Record(id);
        return changed;
    }

    public bool ColorPicker(string id, string label, ref Vector4 value, string? description = null)
    {
        using var field = new FieldScope(this, id, label, description);
        if (!field.Visible) return false;
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.ColorEdit4("##value", ref value,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar);
        Record(id);
        return changed;
    }

    /// <summary>Scope repeated IDs (for example, a server ID in a list).</summary>
    public IDisposable Id(string id) => new IdScope(id);

    /// <summary>Scopes native ImGui disabled input behavior and dimming.</summary>
    public IDisposable Disabled(bool disabled = true) => new DisabledScope(disabled);

    private sealed class IdScope : IDisposable
    {
        public IdScope(string id) => ImGui.PushID(id);
        public void Dispose() => ImGui.PopID();
    }

    private sealed class DisabledScope : IDisposable
    {
        public DisabledScope(bool disabled) => ImGui.BeginDisabled(disabled);
        public void Dispose() => ImGui.EndDisabled();
    }

    private sealed class FieldScope : IDisposable
    {
        public bool Visible { get; }
        public FieldScope(Ui ui, string id, string label, string? description)
        {
            ImGui.PushID(id);
            Visible = ImGui.BeginTable("##row", 2, ImGuiTableFlags.SizingStretchProp);
            if (!Visible) return;
            var stacked = ImGui.GetContentRegionAvail().X < ui.S(ui.StackFieldsBelow);
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch, stacked ? .01f : 1.1f);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(stacked ? 1 : 0);
            if (string.IsNullOrEmpty(description)) ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped(label);
            if (!string.IsNullOrEmpty(description))
            {
                using (ui.Font(ui.Fonts.Small)) ui.MutedText(description);
            }
            if (stacked) ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(1);
        }
        public void Dispose()
        {
            if (Visible) ImGui.EndTable();
            ImGui.PopID();
        }
    }
}
