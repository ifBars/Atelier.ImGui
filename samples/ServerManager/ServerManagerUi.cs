using System.Numerics;
using Atelier;
using ImGuiNET;

namespace ServerManager;

// All state is sample data in this process. Buttons never invoke a real server or external command.
public sealed class ServerManagerUi
{
    private readonly Ui ui;
    private bool running = true;
    private bool autoRestart = true;
    private bool backups = true;
    private bool notifications;
    private bool maintenance;
    private bool reducedMotion;
    private float playerLimit = 32;
    private float backupInterval = 30;
    private string serverName = "West Coast / Production";
    private int region;
    private Vector4 accent = UiTheme.Graphite.Accent;
    private string message = "";
    private static readonly string[] Regions = ["US West · Los Angeles", "US East · Virginia", "Europe · Frankfurt"];

    public ServerManagerUi(Ui ui)
    {
        this.ui = ui;
        Menu = new(ui, "server-manager", new MenuOptions
        {
            Title = "Server Manager",
            Subtitle = "YOUR WORKSPACE",
            Footer = "Local showcase   /   Changes are not persisted"
        },
        new("overview", "Overview", "A clear view of your local server workspace.", Overview),
        new("configuration", "Configuration", "Tune your server without leaving the workspace.", Configuration),
        new("appearance", "Appearance", "Make the workspace feel like your own.", Appearance),
        new("components", "Components", "The same building blocks, ready for your next application.", Components));
    }

    public Menu Menu { get; }

    private void Overview(Ui view)
    {
        view.Section("server", "West Coast", v =>
        {
            v.Badge(running ? "RUNNING" : "STOPPED", running ? null : ui.Theme.Warning);
            v.Text(serverName);
            v.MutedText("127.0.0.1:27015   ·   " + Regions[region]);
            v.Divider();
            if (v.Button("power", running ? "Stop server" : "Start server", running ? ButtonKind.Secondary : ButtonKind.Primary))
            {
                running = !running;
                message = running ? "Demo server started." : "Demo server stopped.";
            }
            ImGui.SameLine();
            using (v.Disabled(!running))
                if (v.Button("restart", "Restart")) message = "Demo restart completed.";
        });
        view.Section("automation", "Automation", v =>
        {
            v.Toggle("restart", "Automatic restart", ref autoRestart, "Recover when a process exits.");
            v.Divider();
            v.Toggle("backup", "Scheduled backups", ref backups, "Keep a regular recovery point.");
            v.Divider();
            v.Toggle("notifications", "Desktop notifications", ref notifications, "Let the workspace stay in the background.");
        });
        if (message.Length > 0) view.MutedText(message);
    }

    private void Configuration(Ui view)
    {
        view.Section("identity", "Server identity", v =>
        {
            v.InputText("name", "Display name", ref serverName);
            v.Combo("region", "Region", ref region, Regions);
        });
        view.Section("limits", "Capacity & recovery", v =>
        {
            v.Slider("players", "Player limit", ref playerLimit, 4, 128, "%.0f players");
            using (v.Disabled(!backups))
                v.Slider("interval", "Backup interval", ref backupInterval, 5, 120, "%.0f min");
            v.Toggle("maintenance", "Maintenance mode", ref maintenance, "Temporarily pause new connections.");
        });
        if (view.Button("apply", "Apply configuration", ButtonKind.Primary)) message = "Configuration applied to the demo state.";
        if (message.Length > 0) view.MutedText(message);
    }

    private void Appearance(Ui view)
    {
        view.Section("theme", "Workspace theme", v =>
        {
            if (v.ColorPicker("accent", "Accent color", ref accent, "Open the swatch for the full color picker."))
                ui.Theme = ui.Theme with { Accent = accent, AccentHover = Vector4.Lerp(accent, Vector4.One, .2f) };
            if (v.Toggle("motion", "Reduce motion", ref reducedMotion, "Switch immediately between toggle states."))
                ui.ReducedMotion = reducedMotion;
        });
        view.Section("preview", "Live preview", v =>
        {
            v.Badge("CONNECTED");
            v.Text("One palette. Every component.");
            v.MutedText("The accent is shared by controls, navigation, focus outlines and status labels.");
            v.Button("example", "Primary action", ButtonKind.Primary);
            ImGui.SameLine();
            v.Button("secondary", "Secondary action");
        });
    }

    private void Components(Ui view)
    {
        view.Section("controls", "Composed settings", v =>
        {
            v.Toggle("enabled", "Automatic restart", ref autoRestart, "Stable IDs keep control state independent of labels.");
            v.Slider("capacity", "Player limit", ref playerLimit, 4, 128, "%.0f");
            v.InputText("name", "Display name", ref serverName);
            v.Combo("region", "Region", ref region, Regions);
            v.ColorPicker("color", "Accent color", ref accent);
        });
        view.Section("actions", "Action hierarchy", v =>
        {
            v.Button("primary", "Primary", ButtonKind.Primary);
            ImGui.SameLine();
            v.Button("secondary", "Secondary");
            ImGui.SameLine();
            v.Button("danger", "Destructive", ButtonKind.Danger);
            using (v.Disabled()) v.Button("disabled", "Unavailable");
        });
    }
}
