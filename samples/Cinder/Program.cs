using Atelier.Windows;
using Cinder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Cinder", ui => new CinderMenu(ui), new OverlayOptions
        {
            Size = CinderMenu.CanvasSize,
            ConfigureFonts = CinderMenu.ConfigureFonts,
            ExitAfterFrames = args.Contains("--smoke") ? 90 : 0
        });
        app.Run();
    }
}
