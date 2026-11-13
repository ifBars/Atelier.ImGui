using Atelier.Windows;
using Mist;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Mist", ui => new MistMenu(ui), new OverlayOptions
        {
            Size = MistMenu.CanvasSize,
            ExitAfterFrames = args.Contains("--smoke") ? 90 : 0
        });
        app.Run();
    }
}
