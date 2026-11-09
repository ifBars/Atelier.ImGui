using System.Numerics;
using Atelier;

namespace ExistingHost;

/// <summary>
/// Example controller for an existing ImGui.NET host. No native window, renderer or input code.
/// Create once after deciding the host's UI scale; call Draw inside the host's existing frame.
/// </summary>
public sealed class ExistingHostMenu
{
    private readonly Ui ui;
    private bool autoRestart = true;
    private float capacity = 32;
    private string name = "Local server";
    private Vector4 accent = UiTheme.Graphite.Accent;

    public ExistingHostMenu(float scale = 1)
    {
        ui = new Ui(scale: scale);
        Menu = new Menu(ui, "local-tools", new MenuOptions
        {
            Title = "Local tools",
            Brand = "MY TOOLS",
            Monogram = "M",
            InitialSize = new(560, 408),
            MinimumSize = new(520, 360),
            SidebarWidth = 140,
            Footer = "Local workspace",
            SidebarFooter = "PERSONAL TOOLS"
        },
        new MenuPage("server", "Server", "Configure your local instance.", DrawServer),
        new MenuPage("appearance", "Appearance", "Personalize the menu.", DrawAppearance));
    }

    public Menu Menu { get; }

    public void Draw() => Menu.Draw(fillViewport: true);

    private void DrawServer(Ui view)
    {
        view.Section("server", "Server settings", controls =>
        {
            controls.InputText("name", "Name", ref name);
            controls.Toggle("restart", "Automatic restart", ref autoRestart);
            controls.Slider("capacity", "Capacity", ref capacity, 4, 128, "%.0f");
        });
    }

    private void DrawAppearance(Ui view)
    {
        view.Section("colors", "Colors", controls =>
        {
            if (controls.ColorPicker("accent", "Accent", ref accent))
                ui.Theme = ui.Theme with { Accent = accent, AccentHover = Vector4.Lerp(accent, Vector4.One, .2f) };
        });
    }
}
