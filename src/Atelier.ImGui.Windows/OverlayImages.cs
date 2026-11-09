using StbImageSharp;

namespace Atelier.Windows;

/// <summary>Host-owned image uploads, released when the overlay closes. Use only on the overlay's render thread.</summary>
public sealed class OverlayImages
{
    private readonly GlRenderer renderer;
    private readonly Dictionary<string, UiImage> files = new(StringComparer.OrdinalIgnoreCase);
    private bool closed;
    internal OverlayImages(GlRenderer renderer) => this.renderer = renderer;

    /// <summary>Loads and caches a local PNG/JPEG/etc. Call during menu creation, not once per frame.</summary>
    public UiImage Load(string path)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        var absolute = Path.GetFullPath(path);
        if (files.TryGetValue(absolute, out var cached)) return cached;
        using var stream = File.OpenRead(absolute);
        var decoded = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var image = CreateRgba(decoded.Width, decoded.Height, decoded.Data);
        files.Add(absolute, image);
        return image;
    }

    /// <summary>Uploads tightly packed RGBA8 pixels with the first row at the top.</summary>
    public UiImage CreateRgba(int width, int height, ReadOnlySpan<byte> rgba)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        return renderer.CreateImage(width, height, rgba);
    }

    internal void Close() { closed = true; files.Clear(); }
}
