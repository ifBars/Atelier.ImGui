using System.Numerics;
using Atelier;
using ImGuiNET;
using Kestrel;

namespace AtelierTests;

internal static class KestrelMenuTests
{
    private static readonly string Assets = Path.Combine(AppContext.BaseDirectory, "kestrel");

    public static void Gallery()
    {
        foreach (var scale in new[] { 1f, 1.25f, 1.5f })
        {
            using var h = new HeadlessHarness(scale, ui => KestrelMenu.ConfigureFonts(ui, Assets));
            ImGui.GetIO().DisplaySize = KestrelMenu.CanvasSize * scale;
            var menu = new KestrelMenu(h.Ui);
            void Draw() => menu.Draw(fillViewport: true);
            var suffix = scale == 1 ? "" : "-" + (int)(scale * 100);
            foreach (var page in menu.PageIds)
            {
                menu.SelectedPage = page;
                Settle(h, Draw);
                Check(ImGui.GetDrawData().TotalVtxCount > 500, $"Kestrel page {page} has no content.");
                foreach (var item in menu.Items.Values)
                    Check(item.Minimum.X >= 0 && item.Minimum.Y >= 0 && item.Maximum.X <= KestrelMenu.CanvasSize.X * scale + .5f &&
                        item.Maximum.Y <= KestrelMenu.CanvasSize.Y * scale + .5f, $"{item.Name} lies outside the canvas on {page} at {scale}.");
                h.Capture($"artifacts/screenshots/kestrel-{page.Replace('.', '-')}{suffix}.png");
            }
            // Popups: a single-select combo and a multi-select list.
            menu.SelectedPage = "visuals.players";
            Settle(h, Draw);
            h.Click(Center(menu, "chams.material"), Draw);
            h.Frame(Draw); h.Frame(Draw);
            h.Capture($"artifacts/screenshots/kestrel-combo-open{suffix}.png");
            Escape(h, Draw);
            menu.SelectedPage = "visuals.world";
            Settle(h, Draw);
            h.Click(Center(menu, "items.filter"), Draw);
            h.Frame(Draw); h.Frame(Draw);
            h.Capture($"artifacts/screenshots/kestrel-multi-open{suffix}.png");
            Escape(h, Draw);
            menu.SelectedPage = "config.profiles";
            Settle(h, Draw);
            h.Click(Center(menu, "profile.row.1"), Draw);
            Settle(h, Draw);
            h.Capture($"artifacts/screenshots/kestrel-profiles-diff{suffix}.png");
        }
    }

    public static void Controls()
    {
        foreach (var scale in new[] { 1f, 1.5f })
        {
            using var h = new HeadlessHarness(scale, ui => KestrelMenu.ConfigureFonts(ui, Assets));
            ImGui.GetIO().DisplaySize = KestrelMenu.CanvasSize * scale;
            var settings = new KestrelSettings();
            var menu = new KestrelMenu(h.Ui, settings);
            Check(ReferenceEquals(menu.Settings, settings), "Kestrel must bind the caller's settings instance.");
            void Draw() => menu.Draw(fillViewport: true);
            Settle(h, Draw);

            // Rail and tabs: the rail remembers each category's last tab.
            h.Click(Center(menu, "rail.camera"), Draw); Settle(h, Draw);
            Check(menu.SelectedPage == "camera.view", "Rail did not open the category's first page.");
            h.Click(Center(menu, "tab.camera.motion"), Draw); Settle(h, Draw);
            Check(menu.SelectedPage == "camera.motion", "Header tab did not select its page.");
            h.Click(Center(menu, "rail.visuals"), Draw); Settle(h, Draw);
            h.Click(Center(menu, "rail.camera"), Draw); Settle(h, Draw);
            Check(menu.SelectedPage == "camera.motion", "Rail did not restore the last tab for the category.");
            h.Click(Center(menu, "rail.visuals"), Draw); Settle(h, Draw);
            Check(menu.SelectedPage == "visuals.players", "Rail navigation failed.");

            // Checkbox and dependent rows.
            h.Click(Center(menu, "esp.enabled"), Draw);
            Check(!settings.Esp, "Checkbox did not change caller state.");
            h.Click(Center(menu, "esp.box"), Draw);
            Check(settings.Box, "A dependent row accepted input while its master option was off.");
            h.Click(Center(menu, "esp.enabled"), Draw);
            Check(settings.Esp, "Checkbox did not toggle back.");

            // Keyboard activation through native navigation.
            h.Frame(Draw);
            var name = settings.Names;
            Key(h, Draw, ImGuiKey.Tab);
            for (var i = 0; i < 40 && Focused(menu) != "esp.names"; i++) Key(h, Draw, ImGuiKey.Tab);
            Check(Focused(menu) == "esp.names", "Keyboard navigation did not reach the Name checkbox.");
            Key(h, Draw, ImGuiKey.Space);
            Check(settings.Names != name, "Space did not activate the focused checkbox.");

            // Segmented control, slider and color chip.
            h.Click(Center(menu, "esp.style.1"), Draw);
            Check(settings.BoxStyle == 1, "Segmented control did not select.");
            var slider = menu.Items["esp.range"];
            h.Click(Vector2.Lerp(slider.Minimum, slider.Maximum, .95f), Draw);
            Check(settings.EspDistance > 450, $"Slider did not take native input ({settings.EspDistance}).");
            h.Click(Vector2.Lerp(slider.Minimum, slider.Maximum, .05f), Draw);
            Check(settings.EspDistance < 75, $"Slider did not map the left edge ({settings.EspDistance}).");
            h.Click(Center(menu, "esp.box.color"), Draw);
            Check(ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel), "Color chip did not open its picker.");
            Escape(h, Draw);

            // Keybind chip: assign, cancel and clear.
            h.Click(Center(menu, "esp.key"), Draw);
            Check(menu.IsCapturingKey, "Keybind chip did not start listening.");
            Key(h, Draw, ImGuiKey.F);
            Check(settings.EspKey == ImGuiKey.F && !menu.IsCapturingKey, $"Keybind was not assigned ({settings.EspKey}).");
            h.Click(Center(menu, "esp.key"), Draw);
            Key(h, Draw, ImGuiKey.Escape);
            Check(settings.EspKey == ImGuiKey.F && !menu.IsCapturingKey, "Escape did not cancel key capture.");
            h.Click(Center(menu, "esp.key"), Draw);
            Key(h, Draw, ImGuiKey.Backspace);
            Check(settings.EspKey == ImGuiKey.None, "Backspace did not clear the keybind.");
            Check(menu.IsOpen, "Escape used by key capture or a popup closed the menu.");

            // Single-select dropdown through native keyboard selection.
            var material = settings.Material;
            h.Click(Center(menu, "chams.material"), Draw);
            Check(ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel), "Dropdown did not open.");
            h.Frame(Draw);
            Key(h, Draw, ImGuiKey.DownArrow);
            Key(h, Draw, ImGuiKey.Enter);
            Check(settings.Material == material + 1, $"Dropdown keyboard selection failed ({settings.Material}).");

            // Multi-select stays open while toggling options.
            h.Click(Center(menu, "tab.visuals.world"), Draw); Settle(h, Draw);
            h.Click(Center(menu, "items.filter"), Draw);
            h.Frame(Draw);
            var ammo = settings.ItemFilter[1];
            var armor = settings.ItemFilter[3];
            h.Click(Center(menu, "items.filter.1"), Draw);
            h.Click(Center(menu, "items.filter.3"), Draw);
            Check(settings.ItemFilter[1] != ammo && settings.ItemFilter[3] != armor, "Multi-select did not toggle options.");
            Check(ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel), "Multi-select closed after a toggle.");
            Escape(h, Draw);

            // Profiles: save, load, create, delete and dirty state.
            menu.SelectedPage = "config.profiles";
            Settle(h, Draw);
            Check(menu.HasUnsavedChanges, "Edits did not mark the active profile as modified.");
            settings.Fov = 101;
            h.Click(Center(menu, "profile.save"), Draw);
            Check(!menu.HasUnsavedChanges && menu.Feedback == "Saved to Default", "Save did not snapshot the active profile.");
            settings.Fov = 70;
            h.Click(Center(menu, "profile.load"), Draw);
            Check(settings.Fov == 101, "Load did not restore the saved snapshot.");
            var filter = settings.ItemFilter;
            h.Click(Center(menu, "profile.row.1"), Draw);
            h.Click(Center(menu, "profile.load"), Draw);
            Check(menu.ActiveProfile == "Ranked" && settings.Fov == 103 && ReferenceEquals(filter, settings.ItemFilter),
                "Loading another profile did not update the caller's instance in place.");
            h.Click(Center(menu, "profile.name"), Draw);
            foreach (var character in "Scrims") ImGui.GetIO().AddInputCharacter(character);
            h.Frame(Draw);
            Key(h, Draw, ImGuiKey.Enter);
            Check(menu.Profiles.Count == 4 && menu.ActiveProfile == "Scrims", "Create did not add and activate a profile.");
            h.Click(Center(menu, "profile.delete"), Draw);
            Check(menu.Profiles.Count == 3 && !menu.Profiles.Contains("Scrims"), "Delete did not remove the profile.");
            Check(menu.IsOpen, "Escape used by a text field closed the menu.");
            h.Click(Center(menu, "profile.row.1"), Draw);
            h.Click(Center(menu, "profile.copy"), Draw);
            Check(KestrelSettings.TryImport(ImGui.GetClipboardText(), out var shared) && shared.Fov == 103 && shared.Skeleton,
                "Copy did not place the selected profile on the clipboard.");
            var active = menu.ActiveProfile;
            h.Click(Center(menu, "profile.import"), Draw);
            Check(menu.Profiles.Count == 4 && menu.Profiles[3] == "Imported" && menu.ActiveProfile == active,
                $"Import did not add a separate profile ({string.Join(", ", menu.Profiles)}; active {menu.ActiveProfile}).");
            ImGui.SetClipboardText("not a profile");
            h.Click(Center(menu, "profile.import"), Draw);
            Check(menu.Profiles.Count == 4 && menu.Feedback == "Clipboard has no profile", "Invalid clipboard text was imported.");
            h.Click(Center(menu, "profile.reset"), Draw);
            Check(settings.Fov == 90 && settings.Esp, "Reset did not restore defaults.");

            // Interface: accent presets restyle the theme; reduced motion reaches Ui.
            h.Click(Center(menu, "tab.config.interface"), Draw); Settle(h, Draw);
            h.Click(Center(menu, "accent.2"), Draw); h.Frame(Draw);
            Check(Vector4.Distance(h.Ui.Theme.Accent, UiTheme.Rgb(0x5CD1A0)) < .01f, "Accent preset did not update the theme.");
            h.Click(Center(menu, "motion"), Draw); h.Frame(Draw);
            Check(settings.ReducedMotion && h.Ui.ReducedMotion, "Reduce motion did not reach the UI.");
            Settle(h, Draw);
            h.Capture($"artifacts/screenshots/kestrel-interface-mint{(scale == 1 ? "" : "-150")}.png");

            // Host integration: drag notification, close and closed geometry.
            var dragged = false;
            menu.DragStarted += () => dragged = true;
            h.Click(Center(menu, "window.drag"), Draw);
            Check(dragged, "Header drag did not notify the host.");
            h.Click(Center(menu, "window.close"), Draw);
            Check(!menu.IsOpen, "Close button failed.");
            menu.IsOpen = true;
            Settle(h, Draw);
            Key(h, Draw, ImGuiKey.Escape);
            Check(!menu.IsOpen, "Escape did not close the menu.");
            h.Frame(Draw);
            Check(ImGui.GetDrawData().TotalVtxCount == 0, "A closed Kestrel menu submitted geometry.");
        }
    }

    private static void Settle(HeadlessHarness h, Action draw)
    {
        for (var i = 0; i < 16; i++) h.Frame(draw);
    }

    private static void Key(HeadlessHarness h, Action draw, ImGuiKey key)
    {
        ImGui.GetIO().AddKeyEvent(key, true); h.Frame(draw);
        ImGui.GetIO().AddKeyEvent(key, false); h.Frame(draw);
    }

    private static void Escape(HeadlessHarness h, Action draw)
    {
        Key(h, draw, ImGuiKey.Escape);
        h.Frame(draw);
    }

    private static string? Focused(KestrelMenu menu) => menu.FocusedControl;

    private static Vector2 Center(KestrelMenu menu, string key)
    {
        Check(menu.Items.TryGetValue(key, out var item), $"Kestrel control '{key}' was not drawn.");
        return (item.Minimum + item.Maximum) / 2;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
