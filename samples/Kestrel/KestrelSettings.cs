using System.Globalization;
using System.Numerics;
using System.Reflection;
using Atelier;
using ImGuiNET;

namespace Kestrel;

/// <summary>
/// Application-owned values bound by <see cref="KestrelMenu"/>. Fields are mutable so controls can bind by ref;
/// the menu never replaces this instance, so references held by the host stay valid after a profile load.
/// </summary>
public sealed class KestrelSettings
{
    // Visuals / players
    public bool Esp = true, Box = true, Names = true, Health = true, Distance, Skeleton, Teammates, VisibleOnly = true;
    public int BoxStyle;
    public float EspDistance = 250;
    public ImGuiKey EspKey = ImGuiKey.V;
    public Vector4 BoxColor = UiTheme.Rgb(0xE7E6EF);
    public bool Chams = true, ChamsHidden = true, Glow, Arrows = true, Footsteps;
    public int Material = 1;
    public float ChamsOpacity = 70, ArrowSize = 14, FootstepRange = 20;
    public Vector4 VisibleColor = UiTheme.Rgb(0x8E7CF8), HiddenColor = UiTheme.Rgb(0x4F5B93), ArrowColor = UiTheme.Rgb(0xF0806E);

    // Visuals / world
    public bool Items = true, RarityColors = true, GrenadePath = true, Impacts, Night, Fog = true, NoSmoke, NoFlash = true, NoScope;
    public bool[] ItemFilter = [true, true, true, false, false];
    public float ItemDistance = 120, FogDensity = 35, Brightness = 100, GrenadeTrail = 2;
    public ImGuiKey ItemKey = ImGuiKey.None;
    public Vector4 GrenadeColor = UiTheme.Rgb(0xE5B24E), SkyTint = UiTheme.Rgb(0x2A2F4A);

    // Camera
    public float Fov = 90, ViewmodelFov = 68, ZoomLevel = 3, BlurStrength = 40, ScreenShake = 60, Smoothing = 20, Sensitivity = 1.2f, CameraDistance = 2.5f;
    public float ViewmodelX = 1, ViewmodelY, ViewmodelZ = -1.5f;
    public int Aspect, Shoulder = 1;
    public bool SmoothZoom = true, ThirdPerson, Collision = true, ShowHands = true, MotionBlur, HeadBob = true, RawInput = true, InvertY;
    public bool HitMarker = true, DamageNumbers, HitSound = true;
    public Vector4 HitColor = UiTheme.Rgb(0xE7E6EF);
    public ImGuiKey ZoomKey = ImGuiKey.Z, ThirdPersonKey = ImGuiKey.MouseMiddle;

    // Radar
    public bool Radar = true, RotateRadar = true, FovCone = true, RadarEnemies = true, RadarTeam = true, Objectives = true;
    public bool MarkerNames, MarkerDistance = true;
    public bool[] RadarItems = [true, false, true, false, false];
    public int RadarShape, RadarCorner = 1;
    public float RadarZoom = 1, RadarOpacity = 90, MarkerSize = 4, RadarSize = 220, RadarMargin = 24;
    public ImGuiKey RadarKey = ImGuiKey.None;
    public Vector4 EnemyColor = UiTheme.Rgb(0xF0806E), TeamColor = UiTheme.Rgb(0x67B2F0);

    // Interface
    public Vector4 Accent = UiTheme.Rgb(0x8E7CF8);
    public float WindowOpacity = 100;
    public bool ReducedMotion, RailTooltips = true, Watermark = true, KeybindList = true, SpectatorList, Notifications = true;
    /// <summary>When set, Escape closes the menu unless a popup, text field or keybind capture is consuming it.</summary>
    public bool CloseOnEscape = true;
    /// <summary>The key a host can use to show or hide the menu. The menu itself does not install a hotkey.</summary>
    public ImGuiKey MenuKey = ImGuiKey.Insert;

    /// <summary>Labels for the item categories indexed by <see cref="ItemFilter"/> and <see cref="RadarItems"/>.</summary>
    public static readonly string[] ItemKinds = ["Weapons", "Ammo", "Medical", "Armor", "Loot"];
    public static readonly string[] BoxStyles = ["Corners", "Full"];
    public static readonly string[] Materials = ["Flat", "Shaded", "Glow", "Wireframe"];
    public static readonly string[] Aspects = ["Native", "16:10", "4:3", "5:4"];
    public static readonly string[] Shoulders = ["Left", "Right"];
    public static readonly string[] RadarShapes = ["Circle", "Square"];
    public static readonly string[] RadarCorners = ["Top left", "Top right", "Bottom left", "Bottom right"];

    /// <summary>Keys a keybind chip can capture: ImGui's named keys, excluding mouse wheels and reserved slots.</summary>
    public static IReadOnlyList<ImGuiKey> BindableKeys { get; } = Enum.GetValues<ImGuiKey>()
        .Where(k => k >= ImGuiKey.NamedKey_BEGIN && k < ImGuiKey.NamedKey_END && k is not (ImGuiKey.MouseWheelX or ImGuiKey.MouseWheelY) &&
            !k.ToString().StartsWith("Reserved", StringComparison.Ordinal))
        .Distinct().ToArray();

    /// <summary>True for <see cref="ImGuiKey.None"/> (unbound) and for every key in <see cref="BindableKeys"/>.</summary>
    public static bool IsBindable(ImGuiKey key) => key == ImGuiKey.None || BindableSet.Contains(key);

    /// <summary>The option labels for an index field such as <c>Material</c>, or null for other fields.</summary>
    public static string[]? OptionsFor(string field) => Choices.GetValueOrDefault(field);

    /// <summary>A deep copy; the arrays are copied too.</summary>
    public KestrelSettings Clone()
    {
        var copy = (KestrelSettings)MemberwiseClone();
        copy.ItemFilter = [.. ItemFilter];
        copy.RadarItems = [.. RadarItems];
        return copy;
    }

    /// <summary>
    /// Repairs values the controls cannot display: wrong-length category arrays are resized (keeping their leading values),
    /// option indices are clamped to their lists, and unknown keys become unbound. The menu calls this before drawing.
    /// </summary>
    public void Normalize()
    {
        ItemFilter = Resized(ItemFilter);
        RadarItems = Resized(RadarItems);
        foreach (var field in Fields)
        {
            if (field.FieldType == typeof(int) && OptionsFor(field.Name) is { } options)
                field.SetValue(this, Math.Clamp((int)field.GetValue(this)!, 0, options.Length - 1));
            else if (field.FieldType == typeof(ImGuiKey) && !IsBindable((ImGuiKey)field.GetValue(this)!))
                field.SetValue(this, ImGuiKey.None);
        }

        static bool[] Resized(bool[]? flags)
        {
            if (flags?.Length == ItemKinds.Length) return flags;
            var resized = new bool[ItemKinds.Length];
            flags?.AsSpan(0, Math.Min(flags.Length, resized.Length)).CopyTo(resized);
            return resized;
        }
    }

    /// <summary>Copies values in place, including array contents, so existing references remain current.</summary>
    public void CopyFrom(KestrelSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var field in Fields)
        {
            if (field.GetValue(other) is bool[] source && field.GetValue(this) is bool[] target && source.Length == target.Length)
                source.CopyTo(target, 0);
            else
                field.SetValue(this, field.GetValue(other) is bool[] array ? array.ToArray() : field.GetValue(other));
        }
    }

    /// <summary>True when every field matches exactly. Arrays compare by contents.</summary>
    public bool SameAs(KestrelSettings other) => !Differences(other).Any();

    /// <summary>Fields whose values differ exactly, with this instance's value first. Arrays compare by contents.</summary>
    public IEnumerable<(string Field, object? Current, object? Other)> Differences(KestrelSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var field in Fields)
        {
            var a = field.GetValue(this);
            var b = field.GetValue(other);
            if (!FieldEquals(a, b)) yield return (field.Name, a, b);
        }
    }

    /// <summary>A single-line text form for sharing through the clipboard. Unknown names are ignored on import.</summary>
    public string Export() => Prefix + string.Join(";", Fields.Select(f => f.Name + "=" + Format(f.GetValue(this)!)));

    /// <summary>
    /// Parses <see cref="Export"/> text. Fields that are absent keep their defaults; any present field with an invalid value
    /// (including an option index outside its list or a key that cannot be bound) rejects the whole text.
    /// </summary>
    public static bool TryImport(string? text, out KestrelSettings settings)
    {
        settings = new KestrelSettings();
        if (text is null || !text.Trim().StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var fields = Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        foreach (var pair in text.Trim()[Prefix.Length..].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !fields.TryGetValue(parts[0], out var field)) continue;
            if (!TryParse(field, parts[1], field.GetValue(settings), out var value)) return false;
            field.SetValue(settings, value);
        }
        return true;
    }

    private const string Prefix = "kestrel1;";
    private static readonly HashSet<ImGuiKey> BindableSet = [.. BindableKeys];
    private static readonly Dictionary<string, string[]> Choices = new(StringComparer.Ordinal)
    {
        [nameof(BoxStyle)] = BoxStyles, [nameof(Material)] = Materials, [nameof(Aspect)] = Aspects,
        [nameof(Shoulder)] = Shoulders, [nameof(RadarShape)] = RadarShapes, [nameof(RadarCorner)] = RadarCorners
    };

    private static bool FieldEquals(object? a, object? b) =>
        a is bool[] left && b is bool[] right ? left.AsSpan().SequenceEqual(right) : Equals(a, b);

    private static string Format(object value) => value switch
    {
        bool b => b ? "1" : "0",
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        Vector4 c => string.Concat(new[] { c.X, c.Y, c.Z, c.W }.Select(x => ((int)MathF.Round(Math.Clamp(x, 0, 1) * 255)).ToString("X2"))),
        bool[] flags => string.Concat(flags.Select(x => x ? '1' : '0')),
        _ => value.ToString()!
    };

    private static bool TryParse(FieldInfo field, string text, object? current, out object? value)
    {
        var type = field.FieldType;
        value = null;
        if (type == typeof(bool) && text is "0" or "1") value = text == "1";
        else if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f)) value = f;
        else if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) &&
            (OptionsFor(field.Name) is not { } options || i >= 0 && i < options.Length)) value = i;
        else if (type == typeof(ImGuiKey) && Enum.TryParse<ImGuiKey>(text, out var key) && Enum.IsDefined(key) && IsBindable(key)) value = key;
        else if (type == typeof(Vector4) && text.Length == 8 && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
            value = new Vector4((rgba >> 24) / 255f, (rgba >> 16 & 255) / 255f, (rgba >> 8 & 255) / 255f, (rgba & 255) / 255f);
        else if (type == typeof(bool[]) && current is bool[] flags && text.Length == flags.Length && text.All(c => c is '0' or '1'))
            value = text.Select(c => c == '1').ToArray();
        return value is not null;
    }

    private static readonly FieldInfo[] Fields = typeof(KestrelSettings).GetFields(BindingFlags.Public | BindingFlags.Instance);
}
