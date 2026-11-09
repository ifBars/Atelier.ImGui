using System.Numerics;
using ImGuiNET;

namespace Atelier;

public enum Easing { Linear, SmoothStep, CubicOut }

public sealed partial class Ui
{
    private readonly Dictionary<uint, MotionState> motions = [];
    private readonly Dictionary<uint, (int Key, double Start, int Frame)> transitions = [];
    private int lastMotionPrune;
    private readonly record struct MotionState(float From, float Target, double Start, float Duration, Easing Curve, int Frame);

    /// <summary>Retargetable animation scoped by the current ImGui ID stack. The first sample starts at the target.</summary>
    public float Animate(string id, float target, float duration = .18f, Easing easing = Easing.CubicOut)
        => AnimateValue(ImGui.GetID("##atelier-motion:" + id), target, duration, easing);

    private float AnimateValue(uint id, float target, float duration, Easing easing)
    {
        if (!float.IsFinite(target)) throw new ArgumentOutOfRangeException(nameof(target));
        ValidateDuration(duration);
        var time = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (!motions.TryGetValue(id, out var state) || ReducedMotion || duration == 0)
            state = new(target, target, time, duration, easing, frame);
        else if (state.Target != target || state.Duration != duration || state.Curve != easing)
            state = new(Sample(state, time), target, time, duration, easing, frame);
        motions[id] = state with { Frame = frame };
        PruneMotion(frame);
        return Sample(state, time);
    }

    /// <summary>Fades and slides incoming content when key changes. Only the current page is drawn; input waits for settling.</summary>
    public void Transition(string id, int key, Action draw, float duration = .18f, Vector2? offset = null)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ValidateDuration(duration);
        var nativeId = ImGui.GetID("##atelier-transition:" + id);
        var time = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (!transitions.TryGetValue(nativeId, out var state)) state = (key, time - duration, frame);
        if (state.Key != key) state = (key, time, frame);
        if (ReducedMotion) state.Start = time - duration;
        transitions[nativeId] = (key, state.Start, frame);
        var progress = duration == 0 ? 1 : Curve((float)((time - state.Start) / duration), Easing.CubicOut);
        var position = ImGui.GetCursorPos();
        ImGui.SetCursorPos(position + (offset ?? new Vector2(0, 7)) * (Scale * (1 - progress)));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (.15f + .85f * progress));
        ImGui.BeginDisabled(progress < 1);
        try { draw(); }
        finally { ImGui.EndDisabled(); ImGui.PopStyleVar(); }
        PruneMotion(frame);
    }

    /// <summary>Periodic opacity/intensity. Reduced motion returns the maximum without oscillating.</summary>
    public float Pulse(float period = 2, float minimum = .4f, float maximum = 1)
    {
        if (!float.IsFinite(period) || period <= 0 || !float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(period));
        return ReducedMotion ? maximum : minimum + (maximum - minimum) * (.5f + .5f * MathF.Sin((float)(ImGui.GetTime() % period) / period * MathF.Tau));
    }

    private static void ValidateDuration(float duration)
    {
        if (!float.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
    }

    private static float Curve(float t, Easing curve)
    {
        t = Math.Clamp(t, 0, 1);
        return curve switch { Easing.Linear => t, Easing.SmoothStep => t * t * (3 - 2 * t), _ => 1 - MathF.Pow(1 - t, 3) };
    }

    private static float Sample(MotionState state, double time)
        => state.Duration == 0 ? state.Target : float.Lerp(state.From, state.Target, Curve((float)((time - state.Start) / state.Duration), state.Curve));

    private void PruneMotion(int frame)
    {
        if (frame - lastMotionPrune < 600) return;
        foreach (var id in motions.Where(pair => frame - pair.Value.Frame > 600).Select(pair => pair.Key).ToArray()) motions.Remove(id);
        foreach (var id in transitions.Where(pair => frame - pair.Value.Frame > 600).Select(pair => pair.Key).ToArray()) transitions.Remove(id);
        lastMotionPrune = frame;
    }
}
