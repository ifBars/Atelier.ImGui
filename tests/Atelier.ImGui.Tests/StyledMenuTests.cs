using System.Numerics;
using ImGuiNET;
using StyledMenus;

namespace AtelierTests;

internal static class StyledMenuTests
{
    public static void Cinder()
    {
        foreach (var scale in new[] { 1f, 1.25f, 1.5f })
        {
            using var h = new HeadlessHarness(scale, global::Cinder.CinderMenu.ConfigureFonts);
            ImGui.GetIO().DisplaySize = global::Cinder.CinderMenu.CanvasSize * scale;
            var menu = new global::Cinder.CinderMenu(h.Ui);
            void Draw() => menu.Draw(fillViewport: true);
            foreach (var page in new[] { "Players", "World", "Config" })
            {
                menu.SelectedPage = page;
                for (var i = 0; i < 4; i++) h.Frame(Draw);
                h.Capture($"artifacts/screenshots/cinder-{page.ToLowerInvariant()}{(scale == 1 ? "" : "-" + (int)(scale * 100))}.png");
            }
            var footer = menu.Items["profile.slot"];
            Check(Vector2.Distance(footer.Maximum - footer.Minimum, new Vector2(MathF.Floor(270 * scale), 23 * scale)) < .01f, $"Cinder footer dimensions at {scale}: {footer.Maximum - footer.Minimum}.");
            foreach (var action in new[] { "load", "save", "reset" })
                Check(Math.Abs(menu.Items[action].Minimum.Y - footer.Minimum.Y) < .01f &&
                    Math.Abs(menu.Items[action].Maximum.Y - footer.Maximum.Y) < .01f, "Footer control heights/baselines do not align.");
            h.Click(footer.Maximum - new Vector2(11.5f) * scale, Draw);
            h.Frame(Draw); h.Frame(Draw);
            h.Capture($"artifacts/screenshots/cinder-profile-open-{(int)(scale * 100)}.png");
            ImGui.GetIO().AddKeyEvent(ImGuiKey.DownArrow, true); h.Frame(Draw);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.DownArrow, false); h.Frame(Draw);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, true); h.Frame(Draw);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, false); h.Frame(Draw);
            Check(menu.ProfileSlot == 1, "Styled dropdown lost native keyboard selection.");
            h.Click(menu.Items["tab.Players"].Center, Draw);
            Check(menu.SelectedPage == "Players", "Cinder tab navigation failed.");
            h.Click(menu.Items["enabled"].Center, Draw);
            Check(!menu.OverlayEnabled, "Cinder checkbox did not mutate state.");
            h.Click(menu.Items["box"].Center, Draw);
            Check(menu.BoundingBoxEnabled, "Disabled Cinder controls accepted input.");
            h.Click(menu.Items["save"].Center, Draw);
            h.Click(menu.Items["enabled"].Center, Draw);
            h.Click(menu.Items["load"].Center, Draw);
            Check(!menu.OverlayEnabled, "Cinder profile load did not restore settings.");
            h.Click(menu.Items["reset"].Center, Draw);
            Check(menu.OverlayEnabled, "Cinder reset failed.");
            var slider = menu.Items["range"];
            h.Click(Vector2.Lerp(slider.Minimum, slider.Maximum, .9f), Draw);
            Check(menu.RenderDistance > 400, "Cinder slider did not receive native input.");
            DragAndClose(h, menu, Draw);
        }
    }

    public static void Vanta()
    {
        foreach (var scale in new[] { 1f, 1.5f })
        {
            using var h = new HeadlessHarness(scale, ui => StyledMenuBase.ConfigureDisplayFont(ui, Asset("vanta", "Rajdhani-SemiBold.ttf")));
            ImGui.GetIO().DisplaySize = new Vector2(960, 620) * scale;
            var menu = new global::Vanta.VantaMenu(h.Ui, h.LoadImage(Asset("vanta", "nebula.jpg")));
            void Draw() => menu.Draw(fillViewport: true);
            foreach (var page in new[] { "Visuals", "Navigation", "World", "Profiles" })
            {
                menu.SelectedPage = page;
                for (var i = 0; i < 18; i++) h.Frame(Draw);
                h.Capture($"artifacts/screenshots/vanta-{page.ToLowerInvariant()}{(scale == 1 ? "" : "-150")}.png");
            }
            h.Click(menu.Items["nav.Visuals"].Center, Draw);
            for (var i = 0; i < 18; i++) h.Frame(Draw);
            Check(menu.SelectedPage == "Visuals", "Vanta navigation failed.");
            h.Click(menu.Items["overlay"].Center, Draw);
            Check(!menu.OverlayEnabled, "Vanta switch did not mutate state.");
            h.Click(menu.Items["profile.save"].Center, Draw);
            Check(menu.Feedback == "Profile saved in memory", "Vanta save action did not run.");
            h.Click(menu.Items["overlay"].Center, Draw);
            h.Click(menu.Items["nav.Profiles"].Center, Draw);
            for (var i = 0; i < 18; i++) h.Frame(Draw);
            h.Click(menu.Items["profile.load"].Center, Draw);
            Check(!menu.OverlayEnabled, "Vanta load did not restore saved state.");
            DragAndClose(h, menu, Draw);
        }
    }

    public static void Redline()
    {
        foreach (var scale in new[] { 1f, 1.5f })
        {
            using var h = new HeadlessHarness(scale, ui => StyledMenuBase.ConfigureDisplayFont(ui, Asset("redline", "BarlowCondensed-Bold.ttf")));
            ImGui.GetIO().DisplaySize = new Vector2(900, 600) * scale;
            var menu = new global::Redline.RedlineMenu(h.Ui, h.LoadImage(Asset("redline", "porsche.jpg")));
            void Draw() => menu.Draw(fillViewport: true);
            foreach (var page in new[] { "DRIVING", "CAMERA", "HUD" })
            {
                menu.SelectedPage = page;
                for (var i = 0; i < 18; i++) h.Frame(Draw);
                h.Capture($"artifacts/screenshots/redline-{page.ToLowerInvariant()}{(scale == 1 ? "" : "-150")}.png");
            }
            h.Click(menu.Items["nav.DRIVING"].Center, Draw);
            for (var i = 0; i < 18; i++) h.Frame(Draw);
            Check(menu.SelectedPage == "DRIVING", "Redline navigation failed.");
            h.Click(menu.Items["traction"].Center, Draw);
            Check(!menu.TractionEnabled, "Redline switch did not mutate state.");
            var slider = menu.Items["steering"];
            h.Click(Vector2.Lerp(slider.Minimum, slider.Maximum, .9f), Draw);
            Check(menu.Steering > 80, "Redline slider did not receive native input.");
            h.Click(menu.Items["apply"].Center, Draw);
            Check(menu.Feedback == "Setup applied to preview", "Redline apply action did not run.");
            h.Click(menu.Items["reset"].Center, Draw);
            Check(menu.TractionEnabled && menu.Steering == 65, "Redline reset did not restore state.");
            DragAndClose(h, menu, Draw);
        }
    }

    private static void DragAndClose(HeadlessHarness h, StyledMenuBase menu, Action draw)
    {
        var dragged = false;
        menu.DragStarted += () => dragged = true;
        h.Click(menu.Items["window.drag"].Center, draw);
        Check(dragged, "Native host drag notification failed.");
        h.Click(menu.Items["window.close"].Center, draw);
        Check(!menu.IsOpen, "Close failed.");
    }

    private static string Asset(string folder, string name) => Path.Combine(AppContext.BaseDirectory, folder, name);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
