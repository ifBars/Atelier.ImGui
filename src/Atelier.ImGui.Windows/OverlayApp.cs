using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Atelier.Windows;

public sealed record OverlayOptions
{
    public UiTheme Theme { get; init; } = UiTheme.Graphite;
    public float? Scale { get; init; }
    public Vector2 Size { get; init; } = new(960, 720);
    public int FramesPerSecond { get; init; } = 60;
    /// <summary>Optional bounded runtime check; zero runs until the menu closes.</summary>
    public int ExitAfterFrames { get; init; }
    /// <summary>Optional font-atlas setup on the render thread, before GPU upload. Assign any new handles to Ui.Fonts.</summary>
    public Action<Ui>? ConfigureFonts { get; init; }
    internal Action<int, int, byte[]>? CaptureFrame { get; init; }
}

/// <summary>
/// Optional Windows/OpenGL host. Creates a topmost borderless native window containing only the menu.
/// Existing hosts need only Atelier.ImGui and should keep their renderer and input backend.
/// </summary>
public sealed class OverlayApp : IDisposable
{
    private readonly IWindow window;
    private readonly Func<Ui, OverlayImages, IMenuSurface> createMenu;
    private readonly OverlayOptions options;
    private IInputContext? input;
    private GlRenderer? renderer;
    private OverlayImages? images;
    private IMenuSurface? menu;
    private IntPtr context;
    private int frames;
    private bool dragging;
    private Point dragStart;
    private Vector2D<int> windowStart;
    private bool disposed;

    public OverlayApp(string title, Func<Ui, Menu> createMenu, OverlayOptions? options = null)
        : this(title, (Func<Ui, IMenuSurface>)createMenu, options)
    {
    }

    public OverlayApp(string title, Func<Ui, IMenuSurface> createMenu, OverlayOptions? options = null)
        : this(title, Adapt(createMenu), options)
    {
    }

    public OverlayApp(string title, Func<Ui, OverlayImages, IMenuSurface> createMenu, OverlayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(createMenu);
        this.createMenu = createMenu;
        this.options = options ?? new();
        if (this.options.FramesPerSecond <= 0 || this.options.ExitAfterFrames < 0 ||
            !float.IsFinite(this.options.Size.X) || !float.IsFinite(this.options.Size.Y) ||
            this.options.Size.X <= 0 || this.options.Size.Y <= 0 ||
            this.options.Scale is { } requested && (!float.IsFinite(requested) || requested <= 0))
            throw new ArgumentOutOfRangeException(nameof(options));
        var scale = this.options.Scale ?? Native.GetDpiForSystem() / 96f;
        var size = new Vector2D<int>((int)(this.options.Size.X * scale), (int)(this.options.Size.Y * scale));
        var settings = WindowOptions.Default;
        settings.Title = title;
        settings.Size = size;
        settings.Position = new((Native.GetSystemMetrics(0) - size.X) / 2, (Native.GetSystemMetrics(1) - size.Y) / 2);
        settings.WindowBorder = WindowBorder.Hidden;
        settings.TopMost = true;
        settings.TransparentFramebuffer = true;
        settings.FramesPerSecond = this.options.FramesPerSecond;
        settings.UpdatesPerSecond = this.options.FramesPerSecond;
        settings.VSync = false;
        settings.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
        window = Window.Create(settings);
        window.Load += () => Load(scale);
        window.Render += Render;
        window.FocusChanged += focused => { if (context != IntPtr.Zero) ImGui.GetIO().AddFocusEvent(focused); };
        window.Closing += ReleaseGraphics;
    }

    /// <summary>Runs on the calling thread until closed. Use an STA entry point on Windows.</summary>
    public void Run() => window.Run();
    public void Close() => window.Close();

    private unsafe void Load(float scale)
    {
        context = ImGui.CreateContext();
        var ui = new Ui(options.Theme, scale);
        var io = ImGui.GetIO();
        io.NativePtr->IniFilename = null;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        var path = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(path, "segoeui.ttf");
        var bold = Path.Combine(path, "seguisb.ttf");
        if (File.Exists(regular))
        {
            var body = io.Fonts.AddFontFromFileTTF(regular, 16 * scale);
            ui.Fonts = new(body, io.Fonts.AddFontFromFileTTF(File.Exists(bold) ? bold : regular, 25 * scale),
                io.Fonts.AddFontFromFileTTF(regular, 13 * scale));
            io.NativePtr->FontDefault = body.NativePtr;
        }
        else io.Fonts.AddFontDefault();
        options.ConfigureFonts?.Invoke(ui);
        renderer = new GlRenderer(GL.GetApi(window));
        input = window.CreateInput();
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += (k, key, _) => KeyEvent(k, key, true);
            keyboard.KeyUp += (k, key, _) => KeyEvent(k, key, false);
            keyboard.KeyChar += (_, character) => ImGui.GetIO().AddInputCharacter(character);
        }
        foreach (var mouse in input.Mice)
            mouse.Scroll += (_, wheel) => ImGui.GetIO().AddMouseWheelEvent(wheel.X, wheel.Y);
        images = new OverlayImages(renderer);
        menu = createMenu(ui, images);
        menu.DragStarted += () =>
        {
            Native.GetCursorPos(out dragStart);
            windowStart = window.Position;
            dragging = true;
        };
    }

    private void Render(double delta)
    {
        if (renderer is null || menu is null) return;
        ImGui.SetCurrentContext(context);
        if (dragging)
        {
            if ((Native.GetAsyncKeyState(1) & 0x8000) == 0) dragging = false;
            else if (Native.GetCursorPos(out var cursor))
                window.Position = windowStart + new Vector2D<int>(cursor.X - dragStart.X, cursor.Y - dragStart.Y);
        }
        var io = ImGui.GetIO();
        io.DisplaySize = new(window.Size.X, window.Size.Y);
        if (window.Size.X <= 0 || window.Size.Y <= 0) return;
        io.DisplayFramebufferScale = new((float)window.FramebufferSize.X / window.Size.X, (float)window.FramebufferSize.Y / window.Size.Y);
        io.DeltaTime = (float)Math.Clamp(delta, .00001, .1);
        if (input?.Mice.FirstOrDefault() is { } mouse)
        {
            io.AddMousePosEvent(mouse.Position.X, mouse.Position.Y);
            io.AddMouseButtonEvent(0, mouse.IsButtonPressed(MouseButton.Left));
            io.AddMouseButtonEvent(1, mouse.IsButtonPressed(MouseButton.Right));
            io.AddMouseButtonEvent(2, mouse.IsButtonPressed(MouseButton.Middle));
        }
        ImGui.NewFrame();
        menu.Draw(fillViewport: true);
        ImGui.Render();
        renderer.Render(ImGui.GetDrawData(), window.FramebufferSize);
        frames++;
        if (!menu.IsOpen || options.ExitAfterFrames > 0 && frames >= options.ExitAfterFrames)
        {
            if (options.CaptureFrame is { } capture)
                capture(window.FramebufferSize.X, window.FramebufferSize.Y, renderer.ReadPixels(window.FramebufferSize));
            window.Close();
        }
    }

    private static void KeyEvent(IKeyboard keyboard, Key key, bool down)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight));
        if (KeyMap.TryGetValue(key, out var mapped)) io.AddKeyEvent(mapped, down);
    }

    private static readonly IReadOnlyDictionary<Key, ImGuiKey> KeyMap = CreateKeyMap();
    private static Dictionary<Key, ImGuiKey> CreateKeyMap()
    {
        var map = new Dictionary<Key, ImGuiKey>();
        foreach (var key in Enum.GetValues<Key>())
        {
            var name = key.ToString();
            name = name switch
            {
                "ControlLeft" => "LeftCtrl",
                "ControlRight" => "RightCtrl",
                "ShiftLeft" => "LeftShift",
                "ShiftRight" => "RightShift",
                "AltLeft" => "LeftAlt",
                "AltRight" => "RightAlt",
                "SuperLeft" => "LeftSuper",
                "SuperRight" => "RightSuper",
                "Left" => "LeftArrow",
                "Right" => "RightArrow",
                "Up" => "UpArrow",
                "Down" => "DownArrow",
                _ => name.StartsWith("Number", StringComparison.Ordinal) ? "_" + name[6..] : name
            };
            if (Enum.TryParse<ImGuiKey>(name, out var result) && result != ImGuiKey.None) map[key] = result;
        }
        return map;
    }

    private void ReleaseGraphics()
    {
        images?.Close(); images = null;
        renderer?.Dispose(); renderer = null;
        input?.Dispose(); input = null;
        if (context != IntPtr.Zero) { ImGui.DestroyContext(context); context = IntPtr.Zero; }
    }

    private static Func<Ui, OverlayImages, IMenuSurface> Adapt(Func<Ui, IMenuSurface> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return (ui, _) => factory(ui);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (context != IntPtr.Zero) { window.MakeCurrent(); ReleaseGraphics(); }
        window.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }
    private static class Native
    {
        [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    }
}
