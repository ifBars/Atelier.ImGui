using Atelier.Windows;
using StyledMenus;
using Vanta;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Vanta", (ui, images) => new VantaMenu(ui,
            images.Load(Path.Combine(AppContext.BaseDirectory, "assets", "nebula.jpg"))), new OverlayOptions
            {
                Size = new(960, 620),
                ExitAfterFrames = args.Contains("--smoke") ? 90 : 0,
                ConfigureFonts = ui => StyledMenuBase.ConfigureDisplayFont(ui, Path.Combine(AppContext.BaseDirectory, "assets", "Rajdhani-SemiBold.ttf"))
            });
        app.Run();
    }
}
