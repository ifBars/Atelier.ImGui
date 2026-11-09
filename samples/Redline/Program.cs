using Atelier.Windows;
using StyledMenus;
using Redline;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Redline", (ui, images) => new RedlineMenu(ui,
            images.Load(Path.Combine(AppContext.BaseDirectory, "assets", "porsche.jpg"))), new OverlayOptions
            {
                Size = new(900, 600),
                ExitAfterFrames = args.Contains("--smoke") ? 90 : 0,
                ConfigureFonts = ui => StyledMenuBase.ConfigureDisplayFont(ui, Path.Combine(AppContext.BaseDirectory, "assets", "BarlowCondensed-Bold.ttf"))
            });
        app.Run();
    }
}
