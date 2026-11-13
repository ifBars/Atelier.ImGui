using System.Numerics;
using System.Runtime.InteropServices;
using Atelier;
using Atelier.Windows;
using ImGuiNET;

namespace AtelierTests;

// Consumes the real ImGui draw data; no browser approximation or native desktop control.
internal sealed unsafe class HeadlessHarness : IDisposable
{
    private readonly IntPtr context;
    private readonly byte[] atlas;
    private readonly int atlasWidth;
    private readonly int atlasHeight;
    private readonly Dictionary<IntPtr, (byte[] Pixels, int Width, int Height)> textures = [];
    public Ui Ui { get; }

    public HeadlessHarness(float scale = 1, Action<Ui>? configureFonts = null)
    {
        context = ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(1280 * scale, 960 * scale);
        io.DeltaTime = 1f / 60;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.NativePtr->IniFilename = null;
        // Segoe UI on Windows, an installed sans-serif elsewhere; text metrics therefore differ between platforms.
        var font = HostFonts.Find() ?? throw new InvalidOperationException(
            "No UI font found. Install Inter, Noto Sans, Liberation Sans or DejaVu Sans to run the harness.");
        var body = io.Fonts.AddFontFromFileTTF(font.Regular, 16 * scale);
        var heading = io.Fonts.AddFontFromFileTTF(font.Bold, 25 * scale);
        var small = io.Fonts.AddFontFromFileTTF(font.Regular, 13 * scale);
        io.NativePtr->FontDefault = body.NativePtr;
        Ui = new(null, scale) { Fonts = new(body, heading, small) };
        configureFonts?.Invoke(Ui);
        io.Fonts.GetTexDataAsRGBA32(out IntPtr data, out atlasWidth, out atlasHeight, out _);
        atlas = new byte[atlasWidth * atlasHeight * 4];
        Marshal.Copy(data, atlas, 0, atlas.Length);
        io.Fonts.SetTexID(new IntPtr(1));
        textures.Add(new IntPtr(1), (atlas, atlasWidth, atlasHeight));
    }

    public void Frame(Action draw)
    {
        ImGui.SetCurrentContext(context);
        ImGui.NewFrame();
        draw();
        ImGui.Render();
    }

    public void Click(Vector2 position, Action draw)
    {
        ImGui.GetIO().AddMousePosEvent(position.X, position.Y);
        Frame(draw);
        ImGui.GetIO().AddMouseButtonEvent(0, true);
        Frame(draw);
        ImGui.GetIO().AddMouseButtonEvent(0, false);
        Frame(draw);
    }

    public UiImage RegisterImage(int width, int height, byte[] pixels)
    {
        var id = new IntPtr(textures.Count + 1);
        textures.Add(id, (pixels, width, height));
        return new UiImage(id, width, height);
    }

    public UiImage LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var image = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return RegisterImage(image.Width, image.Height, image.Data);
    }

    public void Capture(string path)
    {
        var data = ImGui.GetDrawData();
        var width = (int)data.DisplaySize.X;
        var height = (int)data.DisplaySize.Y;
        var pixels = new int[width * height];
        Array.Fill(pixels, unchecked((int)0xFF0B0D10));
        for (var l = 0; l < data.CmdListsCount; l++)
        {
            var list = data.CmdLists[l];
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var command = list.CmdBuffer[c];
                if (command.UserCallback != IntPtr.Zero) throw new NotSupportedException("Custom draw callbacks are outside this test renderer.");
                var texture = textures[command.TextureId];
                var clip = command.ClipRect;
                for (uint i = 0; i < command.ElemCount; i += 3)
                {
                    var offset = (int)(command.IdxOffset + i);
                    var a = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[offset]];
                    var b = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[offset + 1]];
                    var d = list.VtxBuffer[(int)command.VtxOffset + list.IdxBuffer[offset + 2]];
                    Rasterize(a, b, d, clip, width, height, pixels, texture);
                }
            }
        }
        Png.Save(path, width, height, pixels);
    }

    private void Rasterize(ImDrawVertPtr a, ImDrawVertPtr b, ImDrawVertPtr c, Vector4 clip, int width, int height, int[] pixels,
        (byte[] Pixels, int Width, int Height) texture)
    {
        var area = Cross(b.pos - a.pos, c.pos - a.pos);
        if (Math.Abs(area) < .001f) return;
        bool IncludeEdge(Vector2 first, Vector2 second)
        {
            var edge = area > 0 ? second - first : first - second;
            return edge.Y > 0 || edge.Y == 0 && edge.X < 0;
        }
        var minX = (int)Math.Max(0, Math.Max(clip.X, MathF.Floor(Math.Min(a.pos.X, Math.Min(b.pos.X, c.pos.X)))));
        var minY = (int)Math.Max(0, Math.Max(clip.Y, MathF.Floor(Math.Min(a.pos.Y, Math.Min(b.pos.Y, c.pos.Y)))));
        var maxX = (int)Math.Min(width, Math.Min(clip.Z, MathF.Ceiling(Math.Max(a.pos.X, Math.Max(b.pos.X, c.pos.X)))));
        var maxY = (int)Math.Min(height, Math.Min(clip.W, MathF.Ceiling(Math.Max(a.pos.Y, Math.Max(b.pos.Y, c.pos.Y)))));
        for (var y = minY; y < maxY; y++)
            for (var x = minX; x < maxX; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                var wa = Cross(b.pos - p, c.pos - p) / area;
                var wb = Cross(c.pos - p, a.pos - p) / area;
                var wc = 1 - wa - wb;
                const float epsilon = .000001f;
                if (wa < -epsilon || wb < -epsilon || wc < -epsilon) continue;
                // A shared triangle edge belongs to one triangle, otherwise translucent shapes get a diagonal seam.
                if (Math.Abs(wa) <= epsilon && !IncludeEdge(b.pos, c.pos) ||
                    Math.Abs(wb) <= epsilon && !IncludeEdge(c.pos, a.pos) ||
                    Math.Abs(wc) <= epsilon && !IncludeEdge(a.pos, b.pos)) continue;
                var uv = a.uv * wa + b.uv * wb + c.uv * wc;
                var sample = SampleTexture(uv, texture);
                var opacity = sample.W * (Channel(a.col, 24) * wa + Channel(b.col, 24) * wb + Channel(c.col, 24) * wc) / 255;
                if (opacity <= 0) continue;
                var old = pixels[y * width + x];
                var red = Blend(0, 16, sample.X); var green = Blend(8, 8, sample.Y); var blue = Blend(16, 0, sample.Z);
                pixels[y * width + x] = unchecked((int)0xFF000000) | red << 16 | green << 8 | blue;

                int Blend(int sourceShift, int targetShift, float textureChannel)
                {
                    var source = (Channel(a.col, sourceShift) * wa + Channel(b.col, sourceShift) * wb + Channel(c.col, sourceShift) * wc) * textureChannel;
                    var destination = (old >> targetShift) & 255;
                    return Math.Clamp((int)MathF.Round(source * opacity + destination * (1 - opacity)), 0, 255);
                }
            }
    }

    private static Vector4 SampleTexture(Vector2 uv, (byte[] Pixels, int Width, int Height) texture)
    {
        var x = Math.Clamp(uv.X * texture.Width - .5f, 0, texture.Width - 1);
        var y = Math.Clamp(uv.Y * texture.Height - .5f, 0, texture.Height - 1);
        var x0 = (int)x; var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, texture.Width - 1); var y1 = Math.Min(y0 + 1, texture.Height - 1);
        var tx = x - x0; var ty = y - y0;
        Vector4 At(int xx, int yy)
        {
            var i = (yy * texture.Width + xx) * 4;
            return new Vector4(texture.Pixels[i], texture.Pixels[i + 1], texture.Pixels[i + 2], texture.Pixels[i + 3]) / 255;
        }
        return (At(x0, y0) * (1 - tx) + At(x1, y0) * tx) * (1 - ty) +
            (At(x0, y1) * (1 - tx) + At(x1, y1) * tx) * ty;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static int Channel(uint color, int shift) => (int)(color >> shift) & 255;
    public void Dispose() => ImGui.DestroyContext(context);
}
