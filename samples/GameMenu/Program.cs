using Atelier;
using Atelier.Windows;
using GameMenu;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Nightshift", (ui, images) => new NightshiftMenu(ui,
            images.Load(Path.Combine(AppContext.BaseDirectory, "assets", "signal.png")))
        { ActiveTab = args.Contains("--effects") ? "EFFECTS" : "VISUALS" },
            new OverlayOptions { Size = new(800, 550), ExitAfterFrames = args.Contains("--smoke") ? 90 : 0 });
        app.Run();
    }
}
