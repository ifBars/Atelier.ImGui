using ImGuiNET;
using System.Numerics;

namespace Atelier;

/// <summary>Optional host-owned fonts. Default handles use ImGui's current font.</summary>
public readonly record struct UiFonts(ImFontPtr Body, ImFontPtr Heading, ImFontPtr Small);

public enum ButtonKind { Primary, Secondary, Danger }

/// <summary>A submitted control's scoped identity and bounds, for optional tooling.</summary>
public readonly record struct UiItem(uint Id, string Name, Vector2 Minimum, Vector2 Maximum);
