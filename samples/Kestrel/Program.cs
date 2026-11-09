using Atelier.Windows;
using Kestrel;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "assets");
        using var app = new OverlayApp("Kestrel", ui => new KestrelMenu(ui), new OverlayOptions
        {
            Size = KestrelMenu.CanvasSize,
            ConfigureFonts = ui => KestrelMenu.ConfigureFonts(ui, assets),
            ExitAfterFrames = args.Contains("--smoke") ? 90 : 0
        });
        app.Run();
    }
}
