using System.Numerics;
using Atelier;
using AtelierTests;
using ImGuiNET;
using ServerManager;
using Atelier.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

if (args.Contains("--host"))
{
    var captured = false;
    var game = args.Contains("--game");
    var vanta = args.Contains("--vanta");
    var redline = args.Contains("--redline");
    var cinder = args.Contains("--cinder");
    var kestrel = args.Contains("--kestrel");
    OverlayImages? hostImages = null;
    using var host = new OverlayApp("Atelier runtime check", (ui, images) =>
    {
        hostImages = images;
        if (cinder) return (IMenuSurface)new Cinder.CinderMenu(ui);
        if (kestrel) return new Kestrel.KestrelMenu(ui);
        if (vanta) return (IMenuSurface)new Vanta.VantaMenu(ui, images.Load(Path.Combine(AppContext.BaseDirectory, "vanta", "nebula.jpg")));
        if (redline) return new Redline.RedlineMenu(ui, images.Load(Path.Combine(AppContext.BaseDirectory, "redline", "porsche.jpg")));
        if (!game) return (IMenuSurface)new ServerManagerUi(ui).Menu;
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "signal.png");
        var image = images.Load(path);
        Check(image == images.Load(path), "Image cache did not reuse its texture.");
        Check(image.Width == 240 && image.Height == 80, "PNG dimensions did not survive upload.");
        return new GameMenu.NightshiftMenu(ui, image) { ActiveTab = args.Contains("--effects") ? "EFFECTS" : "VISUALS" };
    },
        new OverlayOptions
        {
            ExitAfterFrames = 45,
            Size = cinder ? Cinder.CinderMenu.CanvasSize : kestrel ? Kestrel.KestrelMenu.CanvasSize : vanta ? new(960, 620) : redline ? new(900, 600) : game ? new(800, 550) : new(960, 720),
            ConfigureFonts = cinder ? Cinder.CinderMenu.ConfigureFonts : kestrel ? ui => Kestrel.KestrelMenu.ConfigureFonts(ui, Path.Combine(AppContext.BaseDirectory, "kestrel")) : vanta ? ui => StyledMenus.StyledMenuBase.ConfigureDisplayFont(ui, Path.Combine(AppContext.BaseDirectory, "vanta", "Rajdhani-SemiBold.ttf")) :
                redline ? ui => StyledMenus.StyledMenuBase.ConfigureDisplayFont(ui, Path.Combine(AppContext.BaseDirectory, "redline", "BarlowCondensed-Bold.ttf")) : null,
            CaptureFrame = (width, height, bytes) =>
            {
                // OpenGL has a bottom-left origin and RGBA bytes; Bitmap expects top-left BGRA.
                var pixels = new int[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var offset = ((height - y - 1) * width + x) * 4;
                        pixels[y * width + x] = unchecked((int)0xFF000000) | bytes[offset] << 16 | bytes[offset + 1] << 8 | bytes[offset + 2];
                    }
                Check(pixels.Distinct().Take(100).Count() == 100, "OpenGL frame is empty.");
                using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var bits = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, image.PixelFormat);
                for (var y = 0; y < height; y++) Marshal.Copy(pixels, y * width, bits.Scan0 + y * bits.Stride, width);
                image.UnlockBits(bits);
                Directory.CreateDirectory("artifacts/screenshots");
                image.Save(cinder ? "artifacts/screenshots/cinder-native.png" : kestrel ? "artifacts/screenshots/kestrel-native.png" : vanta ? "artifacts/screenshots/vanta-native.png" : redline ? "artifacts/screenshots/redline-native.png" : game ? args.Contains("--effects") ? "artifacts/screenshots/nightshift-effects-native.png" : "artifacts/screenshots/nightshift-native.png" : "artifacts/screenshots/windows-host.png", ImageFormat.Png);
                captured = true;
            }
        });
    host.Run();
    Throws<ObjectDisposedException>(() => hostImages!.CreateRgba(1, 1, new byte[4]));
    Check(captured, "Host closed without a framebuffer capture.");
    Console.WriteLine("PASS Windows/OpenGL host rendered 45 frames and closed cleanly.");
    return;
}

var passed = 0;
Test("Cinder compact controls, profiles and scaled gallery", StyledMenuTests.Cinder);
Test("Kestrel controls, keybinds, profiles, clipboard and host hooks", KestrelMenuTests.Controls);
Test("Kestrel pages and popups at 100%, 125% and 150%", KestrelMenuTests.Gallery);
Test("Kestrel repairs or rejects invalid host and imported values", KestrelMenuTests.Validation);
Test("Vanta pages, native controls and scaled gallery", StyledMenuTests.Vanta);
Test("Redline pages, native controls and scaled gallery", StyledMenuTests.Redline);
Test("Animation timing, retargeting and reduced motion", EffectsTests.Motion);
Test("Page transition input gating and exception cleanup", EffectsTests.Transition);
Test("Background animation and reduced-motion freeze", EffectsTests.Background);
Test("Image contain/cover/stretch preserve aspect and UV contracts", EffectsTests.Images);
Test("Animated checkbox keeps native mouse and keyboard behavior", EffectsTests.Checkbox);
Test("Game menu tabs, close and scaled gallery", () =>
{
    foreach (var scale in new[] { 1f, 1.5f })
    {
        using var h = new HeadlessHarness(scale);
        ImGui.GetIO().DisplaySize = new Vector2(800, 550) * scale;
        var menu = new GameMenu.NightshiftMenu(h.Ui, h.LoadImage(Path.Combine(AppContext.BaseDirectory, "assets", "signal.png")));
        void Draw() => menu.Draw(fillViewport: true);
        foreach (var tab in new[] { "VISUALS", "GAMEPLAY", "WORLD", "CONFIG", "EFFECTS" })
        {
            menu.ActiveTab = tab;
            for (var i = 0; i < 16; i++) h.Frame(Draw);
            h.Capture($"artifacts/screenshots/nightshift-{tab.ToLowerInvariant()}{(scale == 1 ? "" : "-150")}.png");
        }
        h.Click(new Vector2(70, 77) * scale, Draw);
        Check(menu.ActiveTab == "VISUALS", "Game menu tab input failed.");
        var drag = false;
        menu.DragStarted += () => drag = true;
        h.Click(new Vector2(250, 25) * scale, Draw);
        Check(drag, "Game menu drag notification failed.");
        h.Click(new Vector2(772, 27) * scale, Draw);
        Check(!menu.IsOpen, "Game menu close input failed.");
    }
});
Test("Reject invalid scale and duplicate menu IDs", () =>
{
    Throws<ArgumentOutOfRangeException>(() => new Ui(scale: 0));
    Throws<ArgumentException>(() => new Menu(new Ui(), "test", new(),
        new("same", "One", "", _ => { }), new("same", "Two", "", _ => { })));
});

Test("Native mouse and keyboard toggle; disabled controls ignore clicks", () =>
{
    using var h = new HeadlessHarness();
    var value = false;
    var disabled = false;
    UiItem item = default;
    h.Ui.ItemDrawn += entry => item = entry;
    void Draw() => Window(h.Ui, () =>
    {
        using (h.Ui.Disabled(disabled)) h.Ui.Toggle("toggle", "Service enabled", ref value);
    });
    h.Frame(Draw); h.Frame(Draw);
    h.Click((item.Minimum + item.Maximum) / 2, Draw);
    Check(value, "Mouse click did not toggle.");
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Tab, true); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Tab, false); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, true); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Space, false); h.Frame(Draw);
    Check(!value, "Space did not activate the focused toggle.");
    disabled = true;
    h.Click((item.Minimum + item.Maximum) / 2, Draw);
    Check(!value, "Disabled toggle changed.");
});

Test("Native text entry and slider mutation", () =>
{
    using var h = new HeadlessHarness();
    var items = new Dictionary<string, UiItem>();
    h.Ui.ItemDrawn += item => items[item.Name] = item;
    var text = "";
    var value = 0f;
    void Draw() => Window(h.Ui, () =>
    {
        h.Ui.InputText("text", "Name", ref text);
        h.Ui.Slider("slider", "Capacity", ref value, 0, 100);
    });
    h.Frame(Draw); h.Frame(Draw);
    h.Click((items["text"].Minimum + items["text"].Maximum) / 2, Draw);
    foreach (var character in "Atelier") ImGui.GetIO().AddInputCharacter(character);
    h.Frame(Draw);
    Check(text == "Atelier", "Native input did not update caller state.");
    var slider = items["slider"];
    h.Click(Vector2.Lerp(slider.Minimum, slider.Maximum, .75f), Draw);
    Check(value > 50 && value < 100, "Slider did not update caller state.");
});

Test("Repeated labels retain independent scoped identity", () =>
{
    using var h = new HeadlessHarness();
    var first = false; var second = false;
    var items = new List<UiItem>();
    h.Ui.ItemDrawn += items.Add;
    void Draw() => Window(h.Ui, () =>
    {
        items.Clear();
        using (h.Ui.Id("first")) h.Ui.Toggle("enabled", "Enabled", ref first);
        using (h.Ui.Id("second")) h.Ui.Toggle("enabled", "Enabled", ref second);
    });
    h.Frame(Draw); h.Frame(Draw);
    Check(items[0].Id != items[1].Id, "Scoped IDs collided.");
    h.Click((items[0].Minimum + items[0].Maximum) / 2, Draw);
    Check(first && !second, "One control altered another control.");
});

Test("Native combo and color picker popups open", () =>
{
    using var h = new HeadlessHarness();
    var items = new Dictionary<string, UiItem>();
    h.Ui.ItemDrawn += item => items[item.Name] = item;
    var selected = 0;
    var color = UiTheme.Graphite.Accent;
    var popup = false;
    void Draw() => Window(h.Ui, () =>
    {
        h.Ui.Combo("combo", "Region", ref selected, ["West", "East", "Europe"]);
        h.Ui.ColorPicker("color", "Accent", ref color);
        popup = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
    });
    h.Frame(Draw); h.Frame(Draw);
    h.Click((items["combo"].Minimum + items["combo"].Maximum) / 2, Draw);
    Check(popup, "Combo popup did not open.");
    ImGui.GetIO().AddKeyEvent(ImGuiKey.DownArrow, true); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.DownArrow, false); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, true); h.Frame(Draw);
    ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, false); h.Frame(Draw);
    Check(selected == 1, "Combo keyboard selection did not reach the caller.");
    h.Click((items["color"].Minimum + items["color"].Maximum) / 2, Draw);
    Check(popup, "Color picker popup did not open.");
    h.Frame(Draw);
    var original = color;
    h.Click(items["color"].Minimum + new Vector2(200, 220), Draw);
    Check(Vector4.Distance(original, color) > .1f, "Color picker did not update caller state.");
    h.Capture("artifacts/screenshots/color-picker.png");
});

Test("Embedded shell navigation, host drag notification and close", () =>
{
    using var h = new HeadlessHarness();
    var showcase = new ServerManagerUi(h.Ui);
    var drags = 0;
    showcase.Menu.DragStarted += () => drags++;
    void Draw() => showcase.Menu.Draw(fillViewport: true);
    h.Frame(Draw); h.Frame(Draw);
    h.Click(new Vector2(100, 205), Draw);
    Check(showcase.Menu.SelectedPage == "configuration", "Sidebar did not select its page.");
    h.Click(new Vector2(350, 35), Draw);
    Check(drags == 1, "The native host did not receive exactly one drag notification.");
    h.Click(new Vector2(1246, 33), Draw);
    Check(!showcase.Menu.IsOpen, "Close did not update shell state.");
});

Test("Theme scopes restore host style, including after section exceptions", () =>
{
    using var h = new HeadlessHarness();
    var beforeColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
    var beforePadding = ImGui.GetStyle().FramePadding;
    h.Frame(() =>
    {
        using (h.Ui.Style())
        {
            using (new Ui(UiTheme.Graphite with { Text = Vector4.One }).Style()) { }
            ImGui.Begin("test");
            try { Throws<InvalidOperationException>(() => h.Ui.Section("error", "Error", _ => throw new InvalidOperationException())); }
            finally { ImGui.End(); }
        }
        Check(ImGui.GetStyle().Colors[(int)ImGuiCol.Text] == beforeColor, "Text style leaked.");
        Check(ImGui.GetStyle().FramePadding == beforePadding, "Padding leaked.");
    });
    h.Frame(() => Window(h.Ui, () => h.Ui.Text("The next frame still renders.")));
});

Test("All showcase pages render at 100% and 150%; narrow embedded hosts scroll", () =>
{
    foreach (var scale in new[] { 1f, 1.5f })
    {
        using var h = new HeadlessHarness(scale);
        var showcase = new ServerManagerUi(h.Ui);
        foreach (var page in new[] { "overview", "configuration", "appearance", "components" })
        {
            showcase.Menu.SelectedPage = page;
            for (var i = 0; i < 4; i++) h.Frame(() => showcase.Menu.Draw());
            Check(ImGui.GetDrawData().TotalVtxCount > 100, "Page has no content.");
            h.Capture(Path.Combine("artifacts", "screenshots", page + (scale == 1 ? "" : "-150") + ".png"));
        }
        ImGui.GetIO().DisplaySize = new Vector2(560, 408) * scale;
        showcase.Menu.SelectedPage = "components";
        for (var i = 0; i < 4; i++) h.Frame(() => showcase.Menu.Draw(fillViewport: true));
        h.Capture(Path.Combine("artifacts", "screenshots", "embedded" + (scale == 1 ? "" : "-150") + ".png"));
        var existing = new ExistingHost.ExistingHostMenu(scale);
        for (var i = 0; i < 4; i++) h.Frame(existing.Draw);
        h.Capture(Path.Combine("artifacts", "screenshots", "existing-host" + (scale == 1 ? "" : "-150") + ".png"));
    }
});

Test("Closed menus emit no window draw data", () =>
{
    using var h = new HeadlessHarness();
    var showcase = new ServerManagerUi(h.Ui);
    showcase.Menu.IsOpen = false;
    h.Frame(() => showcase.Menu.Draw());
    Check(ImGui.GetDrawData().TotalVtxCount == 0, "Closed menu submitted geometry.");
});

Console.WriteLine($"PASS: {passed} behavior groups; screenshots in artifacts/screenshots.");
return;

void Test(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine("PASS " + name);
}

static void Window(Ui ui, Action content)
{
    using var style = ui.Style();
    ImGui.SetNextWindowPos(new(10, 10));
    ImGui.SetNextWindowSize(new(700, 500));
    ImGui.Begin("test", ImGuiWindowFlags.NoSavedSettings);
    try { content(); }
    finally { ImGui.End(); }
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
