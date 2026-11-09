using Atelier.Windows;
using ServerManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var app = new OverlayApp("Server Manager", ui => new ServerManagerUi(ui).Menu,
            new OverlayOptions { ExitAfterFrames = args.Contains("--smoke") ? 90 : 0 });
        app.Run();
    }
}
