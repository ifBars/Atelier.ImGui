using System.Numerics;
using ImGuiNET;

namespace Atelier;

public sealed record ParticleOptions
{
    public int Count { get; init; } = 36;
    public float Speed { get; init; } = 9;
    public float Radius { get; init; } = 1.2f;
    public float ConnectionDistance { get; init; } = 65;
    public Vector4? Color { get; init; }
    public int Seed { get; init; } = 17;
}

public sealed partial class Ui
{
    /// <summary>Paints a four-corner gradient in screen pixels, within the current window's clip. Does not consume layout space.</summary>
    public void Gradient(Vector2 position, Vector2 size, Vector4 topLeft, Vector4 topRight, Vector4 bottomRight, Vector4 bottomLeft)
    {
        ValidateArea(position, size);
        ImGui.GetWindowDrawList().AddRectFilledMultiColor(position, position + size,
            Color(topLeft), Color(topRight), Color(bottomRight), Color(bottomLeft));
    }

    /// <summary>Paints a horizontal or vertical two-color gradient in screen pixels.</summary>
    public void Gradient(Vector2 position, Vector2 size, Vector4 from, Vector4 to, bool vertical = false)
        => Gradient(position, size, from, vertical ? from : to, to, vertical ? to : from);

    /// <summary>Deterministic drifting points and nearby connections, clipped to a screen-pixel rectangle. No mouse capture.</summary>
    public void Particles(Vector2 position, Vector2 size, ParticleOptions? options = null)
    {
        ValidateArea(position, size);
        var o = options ?? DefaultParticles;
        if (o.Count < 0 || o.Count > 128 || !float.IsFinite(o.Speed) || !float.IsFinite(o.Radius) || o.Radius <= 0 ||
            !float.IsFinite(o.ConnectionDistance) || o.ConnectionDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        var draw = ImGui.GetWindowDrawList();
        var color = o.Color ?? Theme.Accent with { W = .18f };
        var time = ReducedMotion ? 0 : ImGui.GetTime();
        Span<Vector2> points = stackalloc Vector2[o.Count];
        for (var i = 0; i < points.Length; i++)
        {
            var seed = unchecked((uint)(i + o.Seed * 131));
            var x = Fraction(seed * 1664525u + 1013904223u);
            var y = Fraction(seed * 22695477u + 1u);
            points[i] = position + new Vector2(Wrap(x * size.X + time * o.Speed * Scale * (.3f + y), size.X),
                Wrap(y * size.Y - time * o.Speed * Scale * (.2f + x), size.Y));
        }
        draw.PushClipRect(position, position + size, true);
        try
        {
            var reach = S(o.ConnectionDistance);
            for (var i = 0; i < points.Length; i++)
            {
                draw.AddCircleFilled(points[i], S(o.Radius), Color(color), 8);
                if (reach <= 0) continue;
                for (var j = 0; j < i; j++)
                {
                    var distance = Vector2.Distance(points[i], points[j]);
                    if (distance < reach)
                        draw.AddLine(points[i], points[j], Color(color with { W = color.W * (1 - distance / reach) * .5f }));
                }
            }
        }
        finally { draw.PopClipRect(); }
    }

    private static readonly ParticleOptions DefaultParticles = new();
    private static float Fraction(uint seed) => ((seed ^ seed >> 13) % 65521) / 65521f;
    private static float Wrap(double value, float maximum) => (float)((value % maximum + maximum) % maximum);
    private static void ValidateArea(Vector2 position, Vector2 size)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
    }
}
