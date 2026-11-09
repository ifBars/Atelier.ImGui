using System.Numerics;
using Atelier;
using ImGuiNET;

namespace AtelierTests;

internal static class EffectsTests
{
    public static void Motion()
    {
        foreach (var fps in new[] { 30, 120 })
        {
            using var h = new HeadlessHarness();
            ImGui.GetIO().DeltaTime = 1f / fps;
            var target = 0f;
            var value = 0f;
            void Draw() => Window(h.Ui, () =>
            {
                value = h.Ui.Animate("value", target, .2f);
                Check(value == h.Ui.Animate("value", target, .2f), "Sampling twice advanced time.");
                using (h.Ui.Id("other")) Check(h.Ui.Animate("value", 42) == 42, "Scoped animation IDs collided.");
            });
            h.Frame(Draw);
            target = 1;
            h.Frame(Draw);
            for (var i = 0; i < fps / 10; i++) h.Frame(Draw);
            Check(Math.Abs(value - .875f) < .01f, "Timing depends on frame rate.");
            target = 0;
            h.Frame(Draw);
            Check(value > .8f && value < 1, "Retargeting snapped to an endpoint.");
            for (var i = 0; i < fps / 3; i++) h.Frame(Draw);
            Check(value == 0, "Animation did not settle exactly.");
            h.Ui.ReducedMotion = true;
            target = 1;
            h.Frame(Draw);
            Check(value == 1 && h.Ui.Pulse() == 1, "Reduced motion did not snap/stop oscillation.");
        }
    }

    public static void Transition()
    {
        using var h = new HeadlessHarness();
        var key = 0;
        var alpha = 0f;
        var activations = 0;
        UiItem button = default;
        h.Ui.ItemDrawn += item => button = item;
        void Draw() => Window(h.Ui, () => h.Ui.Transition("page", key, () =>
        {
            alpha = ImGui.GetStyle().Alpha;
            if (h.Ui.Button("action", "Action")) activations++;
        }, duration: .3f));
        h.Frame(Draw); h.Frame(Draw);
        Check(alpha == 1, "Initial content should be visible immediately.");
        key = 1;
        h.Frame(Draw);
        Check(alpha < .2f, "Incoming content was not faded.");
        h.Click((button.Minimum + button.Maximum) / 2, Draw);
        Check(activations == 0, "Transitioning content accepted input.");
        for (var i = 0; i < 22; i++) h.Frame(Draw);
        h.Click((button.Minimum + button.Maximum) / 2, Draw);
        Check(activations == 1 && alpha == 1, "Settled page did not regain normal input/opacity.");
        h.Ui.ReducedMotion = true;
        key = 2;
        h.Frame(Draw);
        Check(alpha == 1, "Reduced motion still faded the page.");
        h.Frame(() => Window(h.Ui, () =>
        {
            try { h.Ui.Transition("throws", 0, () => throw new InvalidOperationException()); }
            catch (InvalidOperationException) { }
            Check(ImGui.GetStyle().Alpha == 1, "Transition leaked opacity after an exception.");
        }));
    }

    public static void Background()
    {
        using var h = new HeadlessHarness();
        void Draw() => Window(h.Ui, () =>
        {
            h.Ui.Gradient(new(40, 70), new(300, 200), Vector4.One, Vector4.Zero);
            h.Ui.Particles(new(40, 70), new(300, 200));
        });
        h.Ui.ReducedMotion = true;
        h.Frame(Draw); h.Frame(Draw);
        var frozen = Fingerprint();
        for (var i = 0; i < 15; i++) h.Frame(Draw);
        Check(frozen == Fingerprint(), "Reduced-motion background changed with time.");
        h.Ui.ReducedMotion = false;
        h.Frame(Draw);
        var before = Fingerprint();
        for (var i = 0; i < 10; i++) h.Frame(Draw);
        Check(before != Fingerprint(), "Enabled background did not move.");
    }

    public static void Images()
    {
        using var h = new HeadlessHarness();
        var image = h.RegisterImage(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]);
        foreach (var fit in Enum.GetValues<ImageFit>())
        {
            void Draw() => Window(h.Ui, () => h.Ui.DrawImage(image, new(40, 70), new(100, 100), fit));
            h.Frame(Draw); h.Frame(Draw);
            var vertices = ImageVertices(image.TextureId);
            var minPos = vertices.Select(v => v.Position).Aggregate(Vector2.Min);
            var maxPos = vertices.Select(v => v.Position).Aggregate(Vector2.Max);
            var minUv = vertices.Select(v => v.Uv).Aggregate(Vector2.Min);
            var maxUv = vertices.Select(v => v.Uv).Aggregate(Vector2.Max);
            if (fit == ImageFit.Contain)
                Check(minPos == new Vector2(40, 95) && maxPos == new Vector2(140, 145), "Contain did not center/letterbox.");
            else Check(minPos == new Vector2(40, 70) && maxPos == new Vector2(140, 170), "Image bounds changed.");
            if (fit == ImageFit.Cover)
                Check(minUv == new Vector2(.25f, 0) && maxUv == new Vector2(.75f, 1), "Cover did not crop UVs.");
            else Check(minUv == Vector2.Zero && maxUv == Vector2.One, "Full-image UVs changed.");
            h.Capture($"artifacts/screenshots/image-fit-{fit}.png");
        }
    }

    public static void Checkbox()
    {
        using var h = new HeadlessHarness();
        var value = false;
        UiItem item = default;
        h.Ui.ItemDrawn += v => item = v;
        void Draw() => Window(h.Ui, () => h.Ui.Checkbox("check", "Animated checkbox", ref value));
        h.Frame(Draw); h.Frame(Draw);
        h.Click((item.Minimum + item.Maximum) / 2, Draw);
        Check(value, "Animated checkbox did not mutate caller state.");
        ImGui.GetIO().AddKeyEvent(ImGuiKey.Tab, true); h.Frame(Draw);
        ImGui.GetIO().AddKeyEvent(ImGuiKey.Tab, false); h.Frame(Draw);
        ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, true); h.Frame(Draw);
        ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, false); h.Frame(Draw);
        Check(!value, "Animated checkbox lost native keyboard navigation.");
    }

    private static List<(Vector2 Position, Vector2 Uv)> ImageVertices(IntPtr texture)
    {
        var result = new List<(Vector2, Vector2)>();
        var data = ImGui.GetDrawData();
        for (var l = 0; l < data.CmdListsCount; l++)
        {
            var list = data.CmdLists[l];
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var command = list.CmdBuffer[c];
                if (command.TextureId != texture) continue;
                for (var i = 0; i < command.ElemCount; i++)
                {
                    var vertex = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[(int)command.IdxOffset + i]];
                    result.Add((vertex.pos, vertex.uv));
                }
            }
        }
        return result;
    }

    private static int Fingerprint()
    {
        var hash = new HashCode();
        var data = ImGui.GetDrawData();
        for (var l = 0; l < data.CmdListsCount; l++)
            for (var i = 0; i < data.CmdLists[l].VtxBuffer.Size; i++)
            {
                var vertex = data.CmdLists[l].VtxBuffer[i];
                hash.Add(vertex.pos); hash.Add(vertex.col);
            }
        return hash.ToHashCode();
    }

    private static void Window(Ui ui, Action draw)
    {
        using var style = ui.Style();
        ImGui.SetNextWindowPos(new(10, 10));
        ImGui.SetNextWindowSize(new(700, 500));
        ImGui.Begin("effects-test", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
        try { draw(); }
        finally { ImGui.End(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
