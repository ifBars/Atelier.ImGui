using System.Numerics;
using ImGuiNET;

namespace Atelier;

public enum ImageFit { Contain, Cover, Stretch }

/// <summary>A borrowed renderer texture ID and pixel dimensions. The host owns upload, lifetime and device resets.</summary>
public readonly record struct UiImage
{
    public UiImage(IntPtr textureId, int width, int height)
    {
        if (textureId == IntPtr.Zero || width <= 0 || height <= 0) throw new ArgumentException("An image requires a texture and positive dimensions.");
        TextureId = textureId;
        Width = width;
        Height = height;
    }
    public IntPtr TextureId { get; }
    public int Width { get; }
    public int Height { get; }
}

public sealed partial class Ui
{
    /// <summary>Displays a borrowed image. Size and rounding are design units; reserves the requested layout rectangle.</summary>
    public void Image(UiImage image, Vector2 size, ImageFit fit = ImageFit.Contain, float rounding = 0, Vector4? tint = null)
    {
        DrawImage(image, ImGui.GetCursorScreenPos(), size * Scale, fit, S(rounding), tint);
        ImGui.Dummy(size * Scale);
    }

    /// <summary>Draws without consuming layout space. Position, size and rounding are screen pixels.</summary>
    public void DrawImage(UiImage image, Vector2 position, Vector2 size, ImageFit fit = ImageFit.Cover, float rounding = 0, Vector4? tint = null)
    {
        ValidateArea(position, size);
        if (image.TextureId == IntPtr.Zero || image.Width <= 0 || image.Height <= 0) throw new ArgumentException("Image is uninitialized.", nameof(image));
        if (!float.IsFinite(rounding) || rounding < 0) throw new ArgumentOutOfRangeException(nameof(rounding));
        var original = new Vector2(image.Width, image.Height);
        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        if (fit == ImageFit.Contain)
        {
            var actual = original * Math.Min(size.X / original.X, size.Y / original.Y);
            position += (size - actual) / 2;
            size = actual;
        }
        else if (fit == ImageFit.Cover)
        {
            var drawn = original * Math.Max(size.X / original.X, size.Y / original.Y);
            var visible = size / drawn;
            uv0 = (Vector2.One - visible) / 2;
            uv1 = Vector2.One - uv0;
        }
        ImGui.GetWindowDrawList().AddImageRounded(image.TextureId, position, position + size, uv0, uv1,
            Color(tint ?? Vector4.One), rounding);
    }
}
