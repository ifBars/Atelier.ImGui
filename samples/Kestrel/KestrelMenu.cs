using System.Numerics;
using Atelier;
using ImGuiNET;

namespace Kestrel;

/// <summary>
/// A compact, art-directed game menu. Depends only on Atelier.ImGui and ImGui.NET: create it once, then call
/// <see cref="Draw"/> inside the host's existing frame. Values live in the caller-owned <see cref="KestrelSettings"/>.
/// Every control is a native ImGui item (button, slider, combo, input or color edit) with custom drawing on top.
/// </summary>
public sealed class KestrelMenu : IMenuSurface
{
    public static Vector2 CanvasSize => new(760, 500);

    private const float Rail = 56, HeaderBottom = 52, FooterTop = 470, ContentTop = 66, Gap = 12, Column = 330;
    private const float RowHeight = 28, ControlWidth = 150, CardPadding = 14;
    private static readonly float LeftColumn = Rail + 16, RightColumn = Rail + 16 + Column + Gap;

    private static readonly string[] BoxStyles = ["Corners", "Full"];
    private static readonly string[] Materials = ["Flat", "Shaded", "Glow", "Wireframe"];
    private static readonly string[] ItemKinds = ["Weapons", "Ammo", "Medical", "Armor", "Loot"];
    private static readonly string[] Aspects = ["Native", "16:10", "4:3", "5:4"];
    private static readonly string[] Shoulders = ["Left", "Right"];
    private static readonly string[] Shapes = ["Circle", "Square"];
    private static readonly string[] Corners = ["Top left", "Top right", "Bottom left", "Bottom right"];
    private static readonly Vector4[] AccentPresets =
        [UiTheme.Rgb(0x8E7CF8), UiTheme.Rgb(0xF0806E), UiTheme.Rgb(0x5CD1A0), UiTheme.Rgb(0x67B2F0), UiTheme.Rgb(0xE5B24E)];
    private static readonly ImGuiKey[] BindableKeys = Enum.GetValues<ImGuiKey>()
        .Where(k => k >= ImGuiKey.NamedKey_BEGIN && k < ImGuiKey.NamedKey_END && k is not (ImGuiKey.MouseWheelX or ImGuiKey.MouseWheelY) &&
            !k.ToString().StartsWith("Reserved", StringComparison.Ordinal))
        .Distinct().ToArray();

    private sealed record Page(string Id, string Label, Action Draw);
    private sealed record Category(string Id, string Label, Page[] Pages);
    private sealed class Profile(string name, KestrelSettings snapshot)
    {
        public string Name = name;
        public KestrelSettings Snapshot = snapshot;
    }

    private readonly Ui ui;
    private readonly UiTheme baseTheme;
    private readonly Category[] categories;
    private readonly Page[] pages;
    private readonly int[] lastPage;
    private readonly List<Profile> profiles = [];
    private readonly Dictionary<string, UiItem> items = [];
    private int page, selectedProfile, activeProfile;
    private string newProfile = "";
    private string? listening;
    private string feedback = "";
    private double feedbackTime = double.NegativeInfinity;
    private Vector2 origin;
    private ImDrawListPtr dl;
    private float cardX, cardW, rowY;
    private bool escapeConsumed;

    public KestrelMenu(Ui ui, KestrelSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        this.ui = ui;
        Settings = settings ?? new KestrelSettings();
        baseTheme = UiTheme.Graphite with
        {
            Background = UiTheme.Rgb(0x0F0F14),
            Sidebar = UiTheme.Rgb(0x0B0B0F),
            Surface = UiTheme.Rgb(0x15151C),
            SurfaceHover = UiTheme.Rgb(0x23222D),
            Field = UiTheme.Rgb(0x0D0D12),
            Border = UiTheme.Rgb(0x262530),
            Text = UiTheme.Rgb(0xE7E6EF),
            Muted = UiTheme.Rgb(0x8B89A0),
            Danger = UiTheme.Rgb(0xF07A86),
            Warning = UiTheme.Rgb(0xE5B24E)
        };
        categories =
        [
            new("visuals", "Visuals", [new("visuals.players", "Players", DrawPlayers), new("visuals.world", "World", DrawWorld)]),
            new("camera", "Camera", [new("camera.view", "View", DrawView), new("camera.motion", "Motion", DrawMotion)]),
            new("radar", "Radar", [new("radar.display", "Display", DrawRadarDisplay), new("radar.markers", "Markers", DrawRadarMarkers)]),
            new("config", "Config", [new("config.profiles", "Profiles", DrawProfiles), new("config.interface", "Interface", DrawInterface)])
        ];
        pages = categories.SelectMany(c => c.Pages).ToArray();
        lastPage = categories.Select(c => Array.IndexOf(pages, c.Pages[0])).ToArray();
        var ranked = Settings.Clone();
        (ranked.Skeleton, ranked.Chams, ranked.Footsteps, ranked.RadarZoom, ranked.Fov) = (true, false, true, 1.5f, 103);
        var recording = Settings.Clone();
        (recording.Esp, recording.Radar, recording.HeadBob, recording.MotionBlur, recording.ReducedMotion) = (false, false, false, true, true);
        profiles.Add(new("Default", Settings.Clone()));
        profiles.Add(new("Ranked", ranked));
        profiles.Add(new("Recording", recording));
        ApplyTheme();
    }

    public KestrelSettings Settings { get; }
    public bool IsOpen { get; set; } = true;
    public event Action? DragStarted;
    /// <summary>True while a keybind chip is waiting for input; hosts should suspend their own hotkeys.</summary>
    public bool IsCapturingKey => listening is not null;
    public IReadOnlyList<string> PageIds => pages.Select(p => p.Id).ToArray();
    public IReadOnlyList<string> Profiles => profiles.Select(p => p.Name).ToArray();
    public string ActiveProfile => profiles[activeProfile].Name;
    public bool HasUnsavedChanges => !Settings.SameAs(profiles[activeProfile].Snapshot);
    public string Feedback => feedback;
    /// <summary>Screen bounds of controls submitted in the last frame, keyed by stable control ID. Intended for tests and tools.</summary>
    public IReadOnlyDictionary<string, UiItem> Items => items;
    /// <summary>The control that had keyboard focus in the last frame, if any.</summary>
    public string? FocusedControl { get; private set; }

    public string SelectedPage
    {
        get => pages[page].Id;
        set
        {
            var index = Array.FindIndex(pages, p => p.Id == value);
            if (index < 0) throw new ArgumentException("Unknown page ID.", nameof(value));
            Select(index);
        }
    }

    /// <summary>Loads the bundled Geist faces before the host uploads its font atlas.</summary>
    public static void ConfigureFonts(Ui ui, string assetDirectory)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var atlas = ImGui.GetIO().Fonts;
        ImFontPtr Add(string file, float size) => atlas.AddFontFromFileTTF(Path.Combine(assetDirectory, file), MathF.Round(size * ui.Scale));
        ui.Fonts = new(Add("Geist-Medium.ttf", 13), Add("Geist-SemiBold.ttf", 15), Add("GeistMono-Medium.ttf", 11));
    }

    public void Draw(bool fillViewport = false)
    {
        if (!IsOpen) return;
        items.Clear();
        FocusedControl = null;
        ApplyTheme();
        var t = ui.Theme;
        using var style = ui.Style();
        var viewport = ImGui.GetMainViewport();
        var size = CanvasSize * ui.Scale;
        var condition = fillViewport ? ImGuiCond.Always : ImGuiCond.FirstUseEver;
        ImGui.SetNextWindowSize(fillViewport ? viewport.Size : size, condition);
        ImGui.SetNextWindowPos(fillViewport ? viewport.Pos : viewport.WorkPos + (viewport.WorkSize - size) / 2, condition);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, ui.S(10));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ui.S(5));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, ui.S(6));
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, ui.S(10));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, t.Background with { W = Settings.WindowOpacity / 100 });
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Lerp(t.Surface, t.SurfaceHover, .5f));
        ImGui.PushStyleColor(ImGuiCol.Border, t.Border);
        var visible = ImGui.Begin("##kestrel", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
        try
        {
            if (!visible) return;
            origin = ImGui.GetWindowPos();
            dl = ImGui.GetWindowDrawList();
            PaintRail();
            PaintHeader(fillViewport);
            ui.Transition("kestrel-page", page, () => pages[page].Draw(), .14f, Vector2.Zero);
            PaintFooter();
            // Popups, text fields and key capture handle Escape themselves; ImGui closes popups before this code runs,
            // so the decision uses the previous frame's state.
            if (Settings.CloseOnEscape && !escapeConsumed && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
                ImGui.IsKeyPressed(ImGuiKey.Escape, false)) IsOpen = false;
            escapeConsumed = listening is not null || ImGui.GetIO().WantTextInput ||
                ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar(5);
        }
    }

    // ---- Shell -------------------------------------------------------------------------------------------

    private void PaintRail()
    {
        var t = ui.Theme;
        var category = CategoryOf(page);
        Fill(0, 0, Rail, CanvasSize.Y, t.Sidebar with { W = Settings.WindowOpacity / 100 }, 10, ImDrawFlags.RoundCornersLeft);
        Fill(Rail - 1, 0, 1, CanvasSize.Y, t.Border);
        // Brand mark: two angled planes that read as a folded wing.
        dl.AddTriangleFilled(P(17, 34), P(28, 16), P(28, 29), C(t.Accent));
        dl.AddTriangleFilled(P(28, 16), P(39, 34), P(28, 29), C(t.Accent with { W = .45f }));
        Fill(14, HeaderBottom, Rail - 28, 1, t.Border);
        for (var i = 0; i < categories.Length; i++)
        {
            var y = 68 + i * 44;
            if (Hit("rail." + categories[i].Id, 10, y, 36, 36)) Select(lastPage[i]);
            var hover = Hover("rail." + i);
            if (Settings.RailTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip(categories[i].Label);
            var active = ui.Animate("rail-active:" + i, category == i ? 1 : 0);
            Fill(10, y, 36, 36, Vector4.Lerp(t.Text with { W = .035f * hover }, t.Accent with { W = .12f }, active), 8);
            Icon(i, new Vector2(28, y + 18), Vector4.Lerp(Vector4.Lerp(t.Muted, t.Text, hover * .6f), t.Accent, active));
        }
        var indicator = ui.Animate("rail-indicator", 68 + category * 44, .2f);
        Fill(0, indicator + 10, 2.5f, 16, t.Accent, 1.5f, ImDrawFlags.RoundCornersRight);
    }

    private void PaintHeader(bool fillViewport)
    {
        var t = ui.Theme;
        var width = CanvasSize.X;
        var category = categories[CategoryOf(page)];
        ui.Gradient(P(Rail, 0), new Vector2(260, HeaderBottom) * ui.Scale,
            t.Accent with { W = .05f }, Vector4.Zero, Vector4.Zero, Vector4.Zero);
        Fill(Rail, HeaderBottom, width - Rail, 1, t.Border);
        Label(LeftColumn + 2, HeaderBottom / 2, category.Label, Face.Heading, t.Text);
        var x = LeftColumn + 2 + Measure(category.Label, Face.Heading) + 14;
        Fill(x, 20, 1, 13, t.Border);
        x += 14;
        float targetX = x, targetW = 0;
        foreach (var item in category.Pages)
        {
            var labelWidth = Measure(item.Label, Face.Body);
            if (Hit("tab." + item.Id, x - 8, 8, labelWidth + 16, HeaderBottom - 8)) Select(Array.IndexOf(pages, item));
            var hover = Hover("tab." + item.Id);
            var selected = ui.Animate("tab-active:" + item.Id, pages[page] == item ? 1 : 0);
            Label(x, HeaderBottom / 2, item.Label, Face.Body, Vector4.Lerp(Vector4.Lerp(t.Muted, t.Text, hover * .5f), t.Text, selected));
            if (pages[page] == item) (targetX, targetW) = (x, labelWidth);
            x += labelWidth + 22;
        }
        var underlineX = ui.Animate("tab-x", targetX, .2f);
        var underlineW = ui.Animate("tab-w", targetW, .2f);
        Fill(underlineX, HeaderBottom - 2, underlineW, 2, t.Accent, 1);

        // The empty header is the drag handle; controls submitted earlier keep input priority.
        var dragX = x - 8;
        Hit("window.drag", dragX, 0, width - 48 - dragX, HeaderBottom);
        if (ImGui.IsItemActivated()) DragStarted?.Invoke();
        if (!fillViewport && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);

        if (Hit("window.close", width - 40, 14, 24, 24)) IsOpen = false;
        var closeHover = Hover("window.close");
        Fill(width - 40, 14, 24, 24, t.Text with { W = .06f * closeHover }, 6);
        var close = Vector4.Lerp(t.Muted, t.Text, closeHover);
        Segment(width - 32, 22, width - 24, 30, close, 1.4f);
        Segment(width - 24, 22, width - 32, 30, close, 1.4f);
    }

    private void PaintFooter()
    {
        var t = ui.Theme;
        Fill(Rail, FooterTop, CanvasSize.X - Rail, 1, t.Border);
        var middle = FooterTop + (CanvasSize.Y - FooterTop) / 2;
        var x = LeftColumn + 2;
        Label(x, middle, "KESTREL", Face.Mono, Dim);
        x += Measure("KESTREL", Face.Mono) + 10;
        Label(x, middle, "/", Face.Mono, Dim);
        Label(x + 14, middle, ActiveProfile.ToUpperInvariant(), Face.Mono, t.Muted);

        var age = ImGui.GetTime() - feedbackTime;
        string status;
        Vector4 dot;
        if (age < 2.4) (status, dot) = (feedback.ToUpperInvariant(), t.Accent);
        else if (HasUnsavedChanges) (status, dot) = ("UNSAVED CHANGES", t.Warning);
        else (status, dot) = ("IN SYNC", Dim);
        var fade = age < 2.4 ? Math.Clamp((float)(2.4 - age) / .3f, 0, 1) : 1;
        var right = CanvasSize.X - 18;
        var statusWidth = Measure(status, Face.Mono);
        Label(right - statusWidth, middle, status, Face.Mono, t.Muted with { W = fade });
        dl.AddCircleFilled(P(right - statusWidth - 9, middle), ui.S(2.5f), C(dot with { W = fade }), 12);
    }

    private void Select(int index)
    {
        if (index == page) return;
        page = index;
        lastPage[CategoryOf(index)] = index;
        listening = null;
    }

    private int CategoryOf(int pageIndex)
    {
        var count = 0;
        for (var i = 0; i < categories.Length; i++)
        {
            count += categories[i].Pages.Length;
            if (pageIndex < count) return i;
        }
        return 0;
    }

    private void ApplyTheme()
    {
        var accent = Settings.Accent with { W = 1 };
        if (ui.Theme.Accent != accent || ui.Theme.Background != baseTheme.Background)
            ui.Theme = baseTheme with
            {
                Accent = accent,
                AccentHover = Vector4.Lerp(accent, Vector4.One, .18f),
                OnAccent = Vector4.Lerp(accent, new Vector4(0, 0, 0, 1), .82f)
            };
        ui.ReducedMotion = Settings.ReducedMotion;
    }

    // ---- Pages -------------------------------------------------------------------------------------------

    private void DrawPlayers()
    {
        var s = Settings;
        var y = ContentTop;
        Card("ESP", LeftColumn, ref y, () =>
        {
            Bind("esp.key", ref s.EspKey);
            Check("esp.enabled", "Enabled", ref s.Esp);
            using (ui.Disabled(!s.Esp))
            {
                Swatch("esp.box.color", ref s.BoxColor);
                Check("esp.box", "Bounding box", ref s.Box);
                using (ui.Disabled(!s.Box)) SegmentedRow("esp.style", "Box style", ref s.BoxStyle, BoxStyles);
                Check("esp.names", "Name", ref s.Names);
                Check("esp.health", "Health bar", ref s.Health);
                Check("esp.distance", "Distance", ref s.Distance);
                Check("esp.skeleton", "Skeleton", ref s.Skeleton);
            }
        });
        Card("Filters", LeftColumn, ref y, () =>
        {
            using var disabled = ui.Disabled(!s.Esp);
            Check("filter.team", "Teammates", ref s.Teammates);
            Check("filter.visible", "Visible only", ref s.VisibleOnly);
            Slider("esp.range", "Max distance", ref s.EspDistance, 25, 500, " m");
        }, fill: true);
        y = ContentTop;
        Card("Chams", RightColumn, ref y, () =>
        {
            Swatch("chams.visible.color", ref s.VisibleColor);
            Check("chams.visible", "Visible", ref s.Chams);
            Swatch("chams.hidden.color", ref s.HiddenColor);
            Check("chams.hidden", "Through walls", ref s.ChamsHidden);
            using (ui.Disabled(!s.Chams && !s.ChamsHidden))
            {
                Combo("chams.material", "Material", ref s.Material, Materials);
                Slider("chams.opacity", "Opacity", ref s.ChamsOpacity, 0, 100, "%");
                Check("chams.glow", "Glow outline", ref s.Glow);
            }
        });
        Card("Indicators", RightColumn, ref y, () =>
        {
            Swatch("arrows.color", ref s.ArrowColor);
            Check("arrows", "Off-screen arrows", ref s.Arrows);
            using (ui.Disabled(!s.Arrows)) Slider("arrows.size", "Arrow size", ref s.ArrowSize, 8, 28, " px");
            Check("footsteps", "Footstep rings", ref s.Footsteps);
            using (ui.Disabled(!s.Footsteps)) Slider("footsteps.range", "Hearing range", ref s.FootstepRange, 5, 40, " m");
        }, fill: true);
    }

    private void DrawWorld()
    {
        var s = Settings;
        var y = ContentTop;
        Card("Items", LeftColumn, ref y, () =>
        {
            Bind("items.key", ref s.ItemKey);
            Check("items.enabled", "Enabled", ref s.Items);
            using (ui.Disabled(!s.Items))
            {
                Multi("items.filter", "Categories", s.ItemFilter, ItemKinds);
                Slider("items.range", "Max distance", ref s.ItemDistance, 10, 300, " m");
                Check("items.rarity", "Rarity colors", ref s.RarityColors);
            }
        });
        Card("Projectiles", LeftColumn, ref y, () =>
        {
            Swatch("grenade.color", ref s.GrenadeColor);
            Check("grenade.path", "Grenade path", ref s.GrenadePath);
            using (ui.Disabled(!s.GrenadePath)) Slider("grenade.trail", "Trail duration", ref s.GrenadeTrail, .5f, 5, " s", decimals: 1);
            Check("impacts", "Impact markers", ref s.Impacts);
        }, fill: true);
        y = ContentTop;
        Card("Environment", RightColumn, ref y, () =>
        {
            Check("night", "Night mode", ref s.Night);
            Check("fog", "Fog", ref s.Fog);
            using (ui.Disabled(!s.Fog)) Slider("fog.density", "Fog density", ref s.FogDensity, 0, 100, "%");
            Slider("brightness", "Brightness", ref s.Brightness, 50, 150, "%");
            ColorRow("sky", "Sky tint", ref s.SkyTint);
        });
        Card("Removals", RightColumn, ref y, () =>
        {
            Check("remove.smoke", "Smoke", ref s.NoSmoke);
            Check("remove.flash", "Flash", ref s.NoFlash);
            Check("remove.scope", "Scope overlay", ref s.NoScope);
        }, fill: true);
    }

    private void DrawView()
    {
        var s = Settings;
        var y = ContentTop;
        Card("Field of view", LeftColumn, ref y, () =>
        {
            Slider("fov", "World", ref s.Fov, 60, 120, "°");
            Slider("fov.viewmodel", "Viewmodel", ref s.ViewmodelFov, 54, 90, "°");
            Combo("aspect", "Aspect ratio", ref s.Aspect, Aspects);
        });
        Card("Zoom", LeftColumn, ref y, () =>
        {
            BindRow("zoom.key", "Zoom key", ref s.ZoomKey);
            Slider("zoom.level", "Magnification", ref s.ZoomLevel, 1.5f, 8, "x", decimals: 1);
            Check("zoom.smooth", "Smooth transition", ref s.SmoothZoom);
        }, fill: true);
        y = ContentTop;
        Card("Third person", RightColumn, ref y, () =>
        {
            Bind("third.key", ref s.ThirdPersonKey);
            Check("third.enabled", "Enabled", ref s.ThirdPerson);
            using (ui.Disabled(!s.ThirdPerson))
            {
                Slider("third.distance", "Distance", ref s.CameraDistance, 1, 6, " m", decimals: 1);
                SegmentedRow("third.shoulder", "Shoulder", ref s.Shoulder, Shoulders);
                Check("third.collision", "Camera collision", ref s.Collision);
            }
        });
        Card("Viewmodel", RightColumn, ref y, () =>
        {
            Check("hands", "Show hands", ref s.ShowHands);
            using var disabled = ui.Disabled(!s.ShowHands);
            Slider("viewmodel.x", "Offset X", ref s.ViewmodelX, -5, 5, "", decimals: 1);
            Slider("viewmodel.y", "Offset Y", ref s.ViewmodelY, -5, 5, "", decimals: 1);
            Slider("viewmodel.z", "Offset Z", ref s.ViewmodelZ, -5, 5, "", decimals: 1);
        }, fill: true);
    }

    private void DrawMotion()
    {
        var s = Settings;
        var y = ContentTop;
        Card("Effects", LeftColumn, ref y, () =>
        {
            Check("blur", "Motion blur", ref s.MotionBlur);
            using (ui.Disabled(!s.MotionBlur)) Slider("blur.strength", "Blur strength", ref s.BlurStrength, 0, 100, "%");
            Check("bob", "Head bob", ref s.HeadBob);
            Slider("shake", "Screen shake", ref s.ScreenShake, 0, 100, "%");
        });
        Card("Feedback", LeftColumn, ref y, () =>
        {
            Swatch("hit.color", ref s.HitColor);
            Check("hit.marker", "Hit marker", ref s.HitMarker);
            Check("hit.numbers", "Damage numbers", ref s.DamageNumbers);
            Check("hit.sound", "Hit sound", ref s.HitSound);
        }, fill: true);
        y = ContentTop;
        Card("Input", RightColumn, ref y, () =>
        {
            Slider("sensitivity", "Sensitivity", ref s.Sensitivity, .1f, 5, "", decimals: 2);
            Slider("smoothing", "Smoothing", ref s.Smoothing, 0, 100, "%");
            Check("raw", "Raw input", ref s.RawInput);
            Check("invert", "Invert vertical", ref s.InvertY);
        }, fill: true);
    }

    private void DrawRadarDisplay()
    {
        var s = Settings;
        var y = ContentTop;
        Card("Radar", LeftColumn, ref y, () =>
        {
            Bind("radar.key", ref s.RadarKey);
            Check("radar.enabled", "Enabled", ref s.Radar);
            using (ui.Disabled(!s.Radar))
            {
                SegmentedRow("radar.shape", "Shape", ref s.RadarShape, Shapes);
                Slider("radar.zoom", "Zoom", ref s.RadarZoom, .5f, 3, "x", decimals: 1);
                Slider("radar.opacity", "Opacity", ref s.RadarOpacity, 20, 100, "%");
                Check("radar.rotate", "Rotate with view", ref s.RotateRadar);
                Check("radar.cone", "Field of view cone", ref s.FovCone);
            }
        });
        Card("Placement", LeftColumn, ref y, () =>
        {
            using var disabled = ui.Disabled(!s.Radar);
            Combo("radar.corner", "Anchor", ref s.RadarCorner, Corners);
            Slider("radar.size", "Size", ref s.RadarSize, 120, 320, " px");
            Slider("radar.margin", "Margin", ref s.RadarMargin, 0, 64, " px");
        }, fill: true);
        RadarCard();
    }

    private void DrawRadarMarkers()
    {
        var s = Settings;
        var y = ContentTop;
        Card("Markers", LeftColumn, ref y, () =>
        {
            using var disabled = ui.Disabled(!s.Radar);
            Swatch("radar.enemy.color", ref s.EnemyColor);
            Check("radar.enemies", "Enemies", ref s.RadarEnemies);
            Swatch("radar.team.color", ref s.TeamColor);
            Check("radar.team", "Teammates", ref s.RadarTeam);
            Check("radar.objectives", "Objectives", ref s.Objectives);
            Multi("radar.items", "Items", s.RadarItems, ItemKinds);
            Slider("radar.marker", "Marker size", ref s.MarkerSize, 2, 8, " px");
        });
        Card("Labels", LeftColumn, ref y, () =>
        {
            using var disabled = ui.Disabled(!s.Radar);
            Check("radar.names", "Names", ref s.MarkerNames);
            Check("radar.distance", "Distance", ref s.MarkerDistance);
        }, fill: true);
        RadarCard();
    }

    private void DrawProfiles()
    {
        var t = ui.Theme;
        var y = ContentTop;
        var bottom = FooterTop - Gap - 8;
        Card("Profiles", LeftColumn, ref y, () =>
        {
            for (var i = 0; i < profiles.Count; i++)
            {
                var key = "profile.row." + i;
                if (Hit(key, cardX - 6, rowY, cardW + 12, 30)) selectedProfile = i;
                var hover = Hover(key);
                var selected = ui.Animate(key + ":selected", selectedProfile == i ? 1 : 0);
                Fill(cardX - 6, rowY, cardW + 12, 30, Vector4.Lerp(t.Text with { W = .03f * hover }, t.Accent with { W = .10f }, selected), 6);
                if (selected > 0) Stroke(cardX - 6, rowY, cardW + 12, 30, t.Accent with { W = .28f * selected }, 6);
                Label(cardX + 4, rowY + 15, profiles[i].Name, Face.Body, Vector4.Lerp(t.Muted, t.Text, Math.Max(selected, hover * .6f)));
                if (i == activeProfile) RightLabel(cardX + cardW - 2, rowY + 15, HasUnsavedChanges ? "ACTIVE *" : "ACTIVE", Face.Mono, t.Accent);
                rowY += 32;
            }
            // The create row stays at the bottom of the list card.
            rowY = bottom - 41;
            Divider();
            if (TextInput("profile.name", ref newProfile, cardX, rowY, cardW - 84, "New profile")) Create();
            using (ui.Disabled(!CanCreate(out _)))
                if (Button("profile.create", "Create", cardX + cardW - 76, rowY, 76, ButtonKind.Secondary)) Create();
            rowY = bottom;
        }, $"{profiles.Count}/6", fill: true);
        y = ContentTop;
        var profile = profiles[selectedProfile];
        Card("Selected", RightColumn, ref y, () =>
        {
            Label(cardX, rowY + 13, profile.Name, Face.Heading, t.Text);
            var state = selectedProfile != activeProfile ? "STORED" : HasUnsavedChanges ? "ACTIVE / MODIFIED" : "ACTIVE / IN SYNC";
            RightLabel(cardX + cardW, rowY + 13, state, Face.Mono, selectedProfile == activeProfile ? t.Accent : Dim);
            rowY += RowHeight + 6;
            var half = (cardW - 8) / 2;
            if (Button("profile.load", "Load", cardX, rowY, half, ButtonKind.Primary)) Load(selectedProfile);
            if (Button("profile.save", "Save", cardX + half + 8, rowY, half, ButtonKind.Secondary)) Save(selectedProfile);
            rowY += RowHeight + 4;
            Divider();

            var changes = Settings.Differences(profile.Snapshot).ToArray();
            Label(cardX, rowY + 8, "CHANGES ON LOAD", Face.Mono, t.Muted);
            RightLabel(cardX + cardW, rowY + 8, changes.Length.ToString(), Face.Mono, Dim);
            rowY += 22;
            var listBottom = bottom - 2 * (RowHeight + 6) - 13;
            var capacity = (int)((listBottom - rowY) / 22);
            if (changes.Length == 0) Label(cardX, rowY + 11, "Matches your current values", Face.Body, Dim);
            for (var i = 0; i < Math.Min(changes.Length, capacity); i++)
            {
                if (i == capacity - 1 && changes.Length > capacity)
                {
                    Label(cardX, rowY + 11, $"+{changes.Length - i} more", Face.Body, Dim);
                    break;
                }
                var (field, current, next) = changes[i];
                Label(cardX, rowY + 11, Humanize(field), Face.Body, OffLabel);
                var to = Display(field, next);
                var from = Display(field, current);
                var right = cardX + cardW;
                RightLabel(right, rowY + 11, to, Face.Mono, t.Text);
                var arrow = right - Measure(to, Face.Mono) - 14;
                Segment(arrow, rowY + 11, arrow + 7, rowY + 11, Dim, 1);
                Segment(arrow + 4.5f, rowY + 8.5f, arrow + 7, rowY + 11, Dim, 1);
                Segment(arrow + 4.5f, rowY + 13.5f, arrow + 7, rowY + 11, Dim, 1);
                RightLabel(arrow - 6, rowY + 11, from, Face.Mono, Dim);
                rowY += 22;
            }

            rowY = listBottom;
            Divider();
            if (Button("profile.copy", "Copy to clipboard", cardX, rowY, half, ButtonKind.Secondary))
            {
                ImGui.SetClipboardText(profile.Snapshot.Export());
                Notify("Copied " + profile.Name);
            }
            using (ui.Disabled(profiles.Count >= 6))
                if (Button("profile.import", "Import", cardX + half + 8, rowY, half, ButtonKind.Secondary)) Import();
            rowY += RowHeight + 6;
            if (Button("profile.reset", "Reset values", cardX, rowY, half, ButtonKind.Secondary))
            {
                Settings.CopyFrom(new KestrelSettings());
                Notify("Defaults restored");
            }
            using (ui.Disabled(profiles.Count == 1))
                if (Button("profile.delete", "Delete", cardX + half + 8, rowY, half, ButtonKind.Danger)) Delete(selectedProfile);
            rowY = bottom;
        }, fill: true);
    }

    private static string Humanize(string field)
    {
        if (field == "Fov") return "Field of view";
        var text = string.Concat(field.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
        return text.Replace("fov", "FOV").Replace("Esp", "ESP");
    }

    private static string Display(string field, object value) => value switch
    {
        bool b => b ? "ON" : "OFF",
        float f => f.ToString(MathF.Abs(f) < 10 && f != MathF.Round(f) ? "0.0#" : "0"),
        int i => (field switch
        {
            "BoxStyle" => BoxStyles, "Material" => Materials, "Aspect" => Aspects, "Shoulder" => Shoulders,
            "RadarShape" => Shapes, "RadarCorner" => Corners, _ => null
        }) is { } names && i >= 0 && i < names.Length ? names[i].ToUpperInvariant() : i.ToString(),
        ImGuiKey key => key == ImGuiKey.None ? "NONE" : KeyLabel(key),
        Vector4 c => $"#{(int)(c.X * 255):X2}{(int)(c.Y * 255):X2}{(int)(c.Z * 255):X2}",
        bool[] flags => $"{flags.Count(x => x)}/{flags.Length}",
        _ => value.ToString() ?? ""
    };

    private void DrawInterface()
    {
        var s = Settings;
        var t = ui.Theme;
        var y = ContentTop;
        Card("Theme", LeftColumn, ref y, () =>
        {
            Swatch("accent.custom", ref s.Accent);
            var x = cardX + cardW - 26 - AccentPresets.Length * 22;
            for (var i = 0; i < AccentPresets.Length; i++)
            {
                var key = "accent." + i;
                var center = new Vector2(x + i * 22 + 8, rowY + RowHeight / 2);
                if (Hit(key, center.X - 9, center.Y - 9, 18, 18)) s.Accent = AccentPresets[i];
                var hover = Hover(key);
                var selected = ui.Animate(key + ":selected", Vector4.Distance(s.Accent with { W = 1 }, AccentPresets[i]) < .01f ? 1 : 0);
                dl.AddCircleFilled(P(center.X, center.Y), ui.S(5.5f + hover * .8f), C(AccentPresets[i]), 24);
                if (selected > 0) dl.AddCircle(P(center.X, center.Y), ui.S(8.5f), C(t.Text with { W = selected * .85f }), 24, ui.S(1.2f));
            }
            Label(cardX, rowY + RowHeight / 2, "Accent", Face.Body, t.Text);
            rowY += RowHeight;
            Slider("opacity", "Window opacity", ref s.WindowOpacity, 60, 100, "%");
        });
        Card("Overlays", LeftColumn, ref y, () =>
        {
            Check("overlay.watermark", "Watermark", ref s.Watermark);
            Check("overlay.keybinds", "Keybind list", ref s.KeybindList);
            Check("overlay.spectators", "Spectator list", ref s.SpectatorList);
            Check("overlay.notifications", "Notifications", ref s.Notifications);
        }, fill: true);
        y = ContentTop;
        Card("Behavior", RightColumn, ref y, () =>
        {
            Check("escape", "Close with Escape", ref s.CloseOnEscape);
            Check("tooltips", "Navigation tooltips", ref s.RailTooltips);
            Check("motion", "Reduce motion", ref s.ReducedMotion);
        });
        // Every bind in one place; the chips edit the same fields as the feature pages.
        Card("Keybinds", RightColumn, ref y, () =>
        {
            BindRow("menu.key", "Menu", ref s.MenuKey);
            BindRow("binds.esp", "Player ESP", ref s.EspKey);
            BindRow("binds.items", "Item ESP", ref s.ItemKey);
            BindRow("binds.zoom", "Zoom", ref s.ZoomKey);
            BindRow("binds.third", "Third person", ref s.ThirdPersonKey);
            BindRow("binds.radar", "Radar", ref s.RadarKey);
        }, fill: true);
    }

    // ---- Profiles ----------------------------------------------------------------------------------------

    private bool CanCreate(out string name)
    {
        name = newProfile.Trim();
        var candidate = name;
        return name.Length > 0 && profiles.Count < 6 && profiles.All(p => !string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private void Create()
    {
        if (!CanCreate(out var name)) return;
        profiles.Add(new(name, Settings.Clone()));
        selectedProfile = activeProfile = profiles.Count - 1;
        newProfile = "";
        Notify("Created " + name);
    }

    private void Import()
    {
        if (!KestrelSettings.TryImport(ImGui.GetClipboardText(), out var imported)) { Notify("Clipboard has no profile"); return; }
        var name = "Imported";
        for (var n = 2; profiles.Any(p => p.Name == name); n++) name = "Imported " + n;
        profiles.Add(new(name, imported));
        selectedProfile = profiles.Count - 1;
        Notify("Imported " + name);
    }

    private void Load(int index)
    {
        Settings.CopyFrom(profiles[index].Snapshot);
        activeProfile = index;
        Notify("Loaded " + profiles[index].Name);
    }

    private void Save(int index)
    {
        profiles[index].Snapshot = Settings.Clone();
        activeProfile = index;
        Notify("Saved to " + profiles[index].Name);
    }

    private void Delete(int index)
    {
        if (profiles.Count == 1) return;
        var name = profiles[index].Name;
        profiles.RemoveAt(index);
        if (activeProfile > index || activeProfile == profiles.Count) activeProfile = Math.Max(0, activeProfile - 1);
        selectedProfile = Math.Min(index, profiles.Count - 1);
        Notify("Deleted " + name);
    }

    private void Notify(string message) => (feedback, feedbackTime) = (message, ImGui.GetTime());

    // ---- Layout primitives -------------------------------------------------------------------------------

    /// <summary>A card that sizes to its rows. Content draws on channel 1; the background is added after measuring.</summary>
    private void Card(string title, float x, ref float y, Action body, string? meta = null, bool fill = false)
    {
        var t = ui.Theme;
        var top = y;
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        (cardX, cardW, rowY) = (x + CardPadding, Column - CardPadding * 2, top + 30);
        try
        {
            Label(cardX, top + 17, title.ToUpperInvariant(), Face.Mono, t.Muted);
            if (meta is not null) RightLabel(cardX + cardW, top + 17, meta, Face.Mono, Dim);
            body();
        }
        finally
        {
            var height = fill ? Math.Max(rowY - top + 8, FooterTop - Gap - top) : rowY - top + 8;
            dl.ChannelsSetCurrent(0);
            Fill(x, top, Column, height, t.Surface, 8);
            Stroke(x, top, Column, height, t.Border, 8);
            Fill(x + 10, top, Column - 20, 1, t.Text with { W = .035f });
            dl.ChannelsMerge();
            y = top + height + Gap;
        }
    }

    private void Divider()
    {
        Fill(cardX, rowY + 6, cardW, 1, ui.Theme.Border with { W = .7f });
        rowY += 13;
    }

    private bool Check(string key, string label, ref bool value)
    {
        var t = ui.Theme;
        var pressed = Hit(key, cardX - 6, rowY, cardW + 12, RowHeight);
        if (pressed) value = !value;
        var hover = Hover(key);
        var on = ui.Animate(key + ":on", value ? 1 : 0, .16f);
        if (hover > 0) Fill(cardX - 6, rowY + 2, cardW + 12, RowHeight - 4, t.Text with { W = .028f * hover }, 5);
        float bx = cardX, by = rowY + 7;
        Fill(bx, by, 14, 14, Vector4.Lerp(t.Field, t.Accent, on), 4);
        Stroke(bx, by, 14, 14, Vector4.Lerp(Vector4.Lerp(t.Border, t.Muted, .3f + hover * .35f), t.Accent, on), 4);
        if (on > .02f)
        {
            Vector2 a = new(bx + 3.6f, by + 7.3f), b = new(bx + 6, by + 9.7f), c = new(bx + 10.5f, by + 4.6f);
            var stroke = t.OnAccent with { W = on };
            Segment(a, Vector2.Lerp(a, b, Math.Min(1, on * 2)), stroke, 1.6f);
            if (on > .5f) Segment(b, Vector2.Lerp(b, c, on * 2 - 1), stroke, 1.6f);
        }
        Label(cardX + 24, rowY + RowHeight / 2, label, Face.Body, Vector4.Lerp(OffLabel, t.Text, on));
        rowY += RowHeight;
        return pressed;
    }

    private bool Slider(string key, string label, ref float value, float minimum, float maximum, string unit, int decimals = 0)
    {
        var t = ui.Theme;
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, t.Text);
        var x = cardX + cardW - ControlWidth;
        const float valueWidth = 40;
        ImGui.SetCursorScreenPos(P(x - 2, rowY + 4));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, Math.Max(0, (ui.S(20) - ImGui.GetFontSize()) / 2)));
        ImGui.SetNextItemWidth(ui.S(ControlWidth - valueWidth + 4));
        bool changed;
        try { changed = ImGui.SliderFloat("##" + key, ref value, minimum, maximum, "", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.NoInput); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(7); }
        Remember(key);
        var hover = Hover(key);
        var held = ui.Animate(key + ":held", ImGui.IsItemActive() ? 1 : 0, .12f);
        // ImGui maps input across the frame minus 2 px padding and half the grab on each side; draw the same span.
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var left = min.X + 2 + ui.S(5);
        var right = max.X - 2 - ui.S(5);
        var middle = MathF.Round((min.Y + max.Y) / 2);
        var fraction = Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
        var knob = left + (right - left) * fraction;
        dl.AddRectFilled(new(left, middle - ui.S(1.5f)), new(right, middle + ui.S(1.5f)), C(t.Border), ui.S(2));
        dl.AddRectFilled(new(left, middle - ui.S(1.5f)), new(knob, middle + ui.S(1.5f)), C(t.Accent), ui.S(2));
        if (held > 0) dl.AddCircleFilled(new(knob, middle), ui.S(9), C(t.Accent with { W = .18f * held }), 24);
        dl.AddCircleFilled(new(knob, middle), ui.S(5 + .8f * Math.Max(hover, held)), C(t.Text), 24);
        dl.AddCircle(new(knob, middle), ui.S(5 + .8f * Math.Max(hover, held)), C(t.Accent with { W = .9f }), 24, ui.S(1.2f));
        var text = value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals)) + unit;
        RightLabel(cardX + cardW, rowY + RowHeight / 2, text, Face.Mono, Vector4.Lerp(t.Muted, t.Text, Math.Max(hover, held)));
        if (changed) listening = null;
        rowY += RowHeight;
        return changed;
    }

    private bool Combo(string key, string label, ref int value, string[] options)
    {
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, ui.Theme.Text);
        var selected = value;
        var changed = Dropdown(key, cardX + cardW - ControlWidth, rowY + 3, ControlWidth, options[value], () =>
        {
            var picked = false;
            for (var i = 0; i < options.Length; i++)
                if (Option($"{key}.{i}", options[i], selected == i, multiple: false)) { selected = i; picked = true; }
            return picked;
        });
        value = selected;
        rowY += RowHeight;
        return changed;
    }

    private bool Multi(string key, string label, bool[] flags, string[] options)
    {
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, ui.Theme.Text);
        var chosen = options.Where((_, i) => flags[i]).ToArray();
        var summary = chosen.Length == 0 ? "None" : chosen.Length == options.Length ? "All" :
            chosen.Length == 1 ? chosen[0] : $"{chosen[0]} +{chosen.Length - 1}";
        var changed = Dropdown(key, cardX + cardW - ControlWidth, rowY + 3, ControlWidth, summary, () =>
        {
            var toggled = false;
            for (var i = 0; i < options.Length; i++)
                if (Option($"{key}.{i}", options[i], flags[i], multiple: true)) { flags[i] = !flags[i]; toggled = true; }
            return toggled;
        });
        rowY += RowHeight;
        return changed;
    }

    /// <summary>A native combo frame with a custom chevron. The popup keeps ImGui's keyboard and mouse behavior.</summary>
    private bool Dropdown(string key, float x, float y, float width, string preview, Func<bool> body)
    {
        var t = ui.Theme;
        ImGui.SetCursorScreenPos(P(x, y));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ui.S(9), Math.Max(0, (ui.S(22) - ImGui.GetFontSize()) / 2)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ui.S(4)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ui.S(2)));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, t.Field);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Lerp(t.Field, t.SurfaceHover, .6f));
        ImGui.PushStyleColor(ImGuiCol.Header, t.Accent with { W = .14f });
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, t.Text with { W = .06f });
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, t.Accent with { W = .2f });
        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Round(ui.S(width)), 0);
        ImGui.SetNextItemWidth(size.X);
        var changed = false;
        try
        {
            size.Y = ImGui.GetFrameHeight();
            var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(position, position + size);
            var open = ImGui.BeginCombo("##" + key, preview, ImGuiComboFlags.NoArrowButton | ImGuiComboFlags.HeightLarge);
            items[key] = new UiItem(ImGui.GetID("##" + key), key, position, position + size);
            if (open)
            {
                try { changed = body(); }
                finally { ImGui.EndCombo(); }
            }
            var border = open ? t.Accent with { W = .7f } : Vector4.Lerp(t.Border, t.Muted, hovered ? .4f : 0);
            dl.AddRect(position + new Vector2(.5f), position + size - new Vector2(.5f), C(border), ui.S(5));
            var center = new Vector2(position.X + size.X - ui.S(13), position.Y + size.Y / 2);
            var direction = open ? -1 : 1;
            var chevron = open ? t.Accent : t.Muted;
            dl.AddLine(center + new Vector2(ui.S(-3.5f), ui.S(-1.8f * direction)), center + new Vector2(0, ui.S(1.8f * direction)), C(chevron), ui.S(1.3f));
            dl.AddLine(center + new Vector2(0, ui.S(1.8f * direction)), center + new Vector2(ui.S(3.5f), ui.S(-1.8f * direction)), C(chevron), ui.S(1.3f));
        }
        finally
        {
            ImGui.PopStyleColor(5);
            ImGui.PopStyleVar(3);
        }
        if (changed) listening = null;
        return changed;
    }

    private bool Option(string key, string text, bool selected, bool multiple)
    {
        var t = ui.Theme;
        var popup = ImGui.GetWindowDrawList();
        var pressed = ImGui.Selectable("##" + key, selected && !multiple,
            multiple ? ImGuiSelectableFlags.NoAutoClosePopups : ImGuiSelectableFlags.None, new Vector2(0, ui.S(24)));
        if (selected && !multiple && ImGui.IsWindowAppearing()) ImGui.SetItemDefaultFocus();
        Remember(key);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var middle = (min.Y + max.Y) / 2;
        var font = Font(Face.Body);
        var size = Size(Face.Body);
        var textX = min.X + ui.S(8);
        if (multiple)
        {
            var box = new Vector2(min.X + ui.S(8), middle - ui.S(6));
            popup.AddRectFilled(box, box + new Vector2(ui.S(12)), C(selected ? t.Accent : t.Field), ui.S(3));
            popup.AddRect(box, box + new Vector2(ui.S(12)), C(selected ? t.Accent : t.Border), ui.S(3));
            if (selected)
            {
                popup.AddLine(box + new Vector2(ui.S(3), ui.S(6.2f)), box + new Vector2(ui.S(5.2f), ui.S(8.4f)), C(t.OnAccent), ui.S(1.5f));
                popup.AddLine(box + new Vector2(ui.S(5.2f), ui.S(8.4f)), box + new Vector2(ui.S(9.2f), ui.S(3.8f)), C(t.OnAccent), ui.S(1.5f));
            }
            textX += ui.S(20);
        }
        else if (selected) popup.AddCircleFilled(new(max.X - ui.S(12), middle), ui.S(2.5f), C(t.Accent), 12);
        popup.AddText(font, size, new(textX, middle - size / 2), C(selected ? t.Text : t.Muted), text);
        return pressed;
    }

    private bool SegmentedRow(string key, string label, ref int value, string[] options)
    {
        var t = ui.Theme;
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, t.Text);
        float x = cardX + cardW - ControlWidth, y = rowY + 3, w = ControlWidth, h = 22;
        Fill(x, y, w, h, t.Field, 5);
        Stroke(x, y, w, h, t.Border, 5);
        var segment = w / options.Length;
        var position = ui.Animate(key + ":position", value, .18f);
        Fill(x + 2 + position * segment, y + 2, segment - 4, h - 4, t.SurfaceHover, 4);
        Stroke(x + 2 + position * segment, y + 2, segment - 4, h - 4, t.Text with { W = .07f }, 4);
        var changed = false;
        for (var i = 0; i < options.Length; i++)
        {
            if (Hit($"{key}.{i}", x + i * segment, y, segment, h) && value != i) { value = i; changed = true; }
            var hover = Hover($"{key}.{i}");
            var closeness = Math.Clamp(1 - Math.Abs(position - i), 0, 1);
            var color = Vector4.Lerp(Vector4.Lerp(t.Muted, t.Text, hover * .5f), t.Text, closeness);
            Label(x + i * segment + (segment - Measure(options[i], Face.Body)) / 2, y + h / 2, options[i], Face.Body, color);
        }
        rowY += RowHeight;
        return changed;
    }

    /// <summary>A trailing color chip on the current row. Submit before the row so it keeps input priority.</summary>
    private bool Swatch(string key, ref Vector4 color)
    {
        var t = ui.Theme;
        ImGui.SetCursorScreenPos(P(cardX + cardW - 16, rowY + 6));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, Math.Max(0, (ui.S(16) - ImGui.GetFontSize()) / 2)));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ui.S(4));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ui.S(10)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ui.S(8), ui.S(6)));
        bool changed;
        try
        {
            changed = ImGui.ColorEdit4("##" + key, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel |
                ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop);
        }
        finally { ImGui.PopStyleVar(5); }
        Remember(key);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var hover = Hover(key);
        dl.AddRect(min + new Vector2(.5f), max - new Vector2(.5f), C(Vector4.Lerp(t.Text with { W = .16f }, t.Text with { W = .5f }, hover)), ui.S(4));
        return changed;
    }

    private void ColorRow(string key, string label, ref Vector4 color)
    {
        Swatch(key, ref color);
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, ui.Theme.Text);
        rowY += RowHeight;
    }

    /// <summary>A trailing keybind chip. Click, then press a key or mouse button; Escape cancels and Backspace clears.</summary>
    private bool Bind(string key, ref ImGuiKey bound)
    {
        var t = ui.Theme;
        var changed = false;
        if (listening == key)
        {
            foreach (var candidate in BindableKeys)
            {
                if (!ImGui.IsKeyPressed(candidate, false)) continue;
                if (candidate is not (ImGuiKey.Escape or ImGuiKey.MouseLeft))
                {
                    bound = candidate is ImGuiKey.Backspace or ImGuiKey.Delete ? ImGuiKey.None : candidate;
                    changed = true;
                }
                listening = null;
                break;
            }
        }
        var capturing = listening == key;
        var text = capturing ? "PRESS" : KeyLabel(bound);
        var width = MathF.Max(30, Measure(text, Face.Mono) + 14);
        float x = cardX + cardW - width, y = rowY + 5, h = 18;
        if (Hit(key, x, y, width, h)) listening = capturing ? null : key;
        var hover = Hover(key);
        Fill(x, y, width, h, capturing ? t.Accent with { W = .14f } : t.Field, 4);
        Stroke(x, y, width, h, capturing ? t.Accent : Vector4.Lerp(t.Border, t.Muted, hover * .5f), 4);
        var color = capturing ? t.Accent : Vector4.Lerp(t.Muted, t.Text, hover);
        if (text.Length > 0) Label(x + (width - Measure(text, Face.Mono)) / 2, y + h / 2, text, Face.Mono, color);
        else Fill(x + width / 2 - 4, y + h / 2, 8, 1, Vector4.Lerp(Dim, t.Muted, hover));
        return changed;
    }

    private void BindRow(string key, string label, ref ImGuiKey bound)
    {
        Bind(key, ref bound);
        Label(cardX, rowY + RowHeight / 2, label, Face.Body, ui.Theme.Text);
        rowY += RowHeight;
    }

    private bool Button(string key, string label, float x, float y, float width, ButtonKind kind)
    {
        var t = ui.Theme;
        const float height = 28;
        var pressed = Hit(key, x, y, width, height);
        var hover = Hover(key);
        var held = ImGui.IsItemActive() ? 1f : 0;
        var (fill, border, text) = kind switch
        {
            ButtonKind.Primary => (Vector4.Lerp(t.Accent, t.AccentHover, hover * .6f - held * .3f), t.Accent, t.OnAccent),
            ButtonKind.Danger => (t.Danger with { W = .06f + hover * .06f }, t.Danger with { W = .35f + hover * .2f }, t.Danger),
            _ => (Vector4.Lerp(t.SurfaceHover, t.Border, hover * .6f), t.Text with { W = .08f + hover * .06f }, t.Text)
        };
        Fill(x, y, width, height, fill, 6);
        Stroke(x, y, width, height, border, 6);
        Label(x + (width - Measure(label, Face.Body)) / 2, y + height / 2, label, Face.Body, text);
        return pressed;
    }

    /// <summary>A native text field. Returns true when Enter is pressed.</summary>
    private bool TextInput(string key, ref string value, float x, float y, float width, string placeholder)
    {
        var t = ui.Theme;
        ImGui.SetCursorScreenPos(P(x, y));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ui.S(10), Math.Max(0, (ui.S(28) - ImGui.GetFontSize()) / 2)));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ui.S(6));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, t.Field);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, t.Field);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, t.Field);
        ImGui.SetNextItemWidth(ui.S(width));
        bool entered;
        try { entered = ImGui.InputTextWithHint("##" + key, placeholder, ref value, 24, ImGuiInputTextFlags.EnterReturnsTrue); }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(2); }
        Remember(key);
        var active = ImGui.IsItemActive();
        var hover = Hover(key);
        dl.AddRect(ImGui.GetItemRectMin() + new Vector2(.5f), ImGui.GetItemRectMax() - new Vector2(.5f),
            C(active ? t.Accent with { W = .7f } : Vector4.Lerp(t.Border, t.Muted, hover * .4f)), ui.S(6));
        return entered;
    }

    // ---- Radar preview -----------------------------------------------------------------------------------

    private static readonly Vector2[] Enemies = [new(32, -64), new(-50, -24), new(72, 38), new(-94, -92), new(140, 30)];
    private static readonly Vector2[] Team = [new(-22, 34), new(14, 60)];
    private static readonly (Vector2 Position, int Kind)[] Loot = [(new(42, -18), 0), (new(-62, 52), 2), (new(86, -72), 1), (new(-36, -88), 3), (new(18, 98), 4), (new(-8, -30), 2)];
    private static readonly Vector2 Objective = new(-70, -46);

    private void RadarCard()
    {
        var y = ContentTop;
        Card("Preview", RightColumn, ref y, () =>
        {
            var legendTop = FooterTop - Gap - 8 - 20;
            DrawRadar(cardX, rowY + 4, cardW, legendTop - rowY - 12);
            rowY = legendTop;
            Legend();
        }, Settings.Radar ? $"{120 / Settings.RadarZoom:0} M RANGE" : "OFF", fill: true);
    }

    private void DrawRadar(float x, float y, float width, float height)
    {
        var s = Settings;
        var t = ui.Theme;
        var middle = new Vector2(x + width / 2, y + height / 2);
        var radius = MathF.Min(width, height) / 2 - 2;
        var center = P(middle.X, middle.Y);
        var r = ui.S(radius);
        var square = s.RadarShape == 1;
        var lowerLeft = P(middle.X - radius, middle.Y - radius);
        var upperRight = P(middle.X + radius, middle.Y + radius);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (s.Radar ? s.RadarOpacity / 100 : .3f));
        dl.PushClipRect(lowerLeft - new Vector2(1), upperRight + new Vector2(1), true);
        try
        {
            if (square) dl.AddRectFilled(lowerLeft, upperRight, C(t.Field), ui.S(12));
            else dl.AddCircleFilled(center, r, C(t.Field), 96);
            for (var ring = 1; ring <= 2; ring++)
            {
                var ringRadius = r * ring / 3;
                if (square) dl.AddRect(center - new Vector2(ringRadius), center + new Vector2(ringRadius), C(t.Border with { W = .7f }), ui.S(6));
                else dl.AddCircle(center, ringRadius, C(t.Border with { W = .7f }), 64);
            }
            dl.AddLine(center - new Vector2(r, 0), center + new Vector2(r, 0), C(t.Border with { W = .45f }));
            dl.AddLine(center - new Vector2(0, r), center + new Vector2(0, r), C(t.Border with { W = .45f }));

            const float heading = 28;
            var view = s.RotateRadar ? 0 : heading;
            var rotation = s.RotateRadar ? -heading * MathF.PI / 180 : 0;
            var direction = (view - 90) * MathF.PI / 180;
            if (s.FovCone)
            {
                var half = s.Fov / 2 * MathF.PI / 180;
                dl.PathLineTo(center);
                dl.PathArcTo(center, r, direction - half, direction + half, 32);
                dl.PathFillConvex(C(t.Accent with { W = .08f }));
                foreach (var edge in new[] { direction - half, direction + half })
                    dl.AddLine(center, center + new Vector2(MathF.Cos(edge), MathF.Sin(edge)) * r, C(t.Accent with { W = .28f }), ui.S(1));
            }
            if (!ui.ReducedMotion && s.Radar)
            {
                var sweep = (float)(ImGui.GetTime() % 6 / 6 * MathF.Tau);
                const int slices = 12;
                for (var i = 0; i < slices; i++)
                {
                    dl.PathLineTo(center);
                    dl.PathArcTo(center, r * (square ? 1.42f : 1), sweep - (i + 1) * .05f, sweep - i * .05f, 3);
                    dl.PathFillConvex(C(t.Accent with { W = .07f * (1 - i / (float)slices) }));
                }
            }

            var pixelsPerMeter = r / (120 / s.RadarZoom);
            var marker = ui.S(s.MarkerSize / 2 + 1.5f);
            Vector2 Map(Vector2 world)
            {
                var (sin, cos) = MathF.SinCos(rotation);
                return new Vector2(world.X * cos - world.Y * sin, world.X * sin + world.Y * cos) * pixelsPerMeter;
            }
            bool Inside(Vector2 offset, float margin) => square
                ? MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y)) <= r - margin
                : offset.Length() <= r - margin;

            if (s.Objectives)
            {
                var o = Map(Objective);
                if (Inside(o, marker))
                {
                    var p = center + o;
                    var d = marker + ui.S(1.5f);
                    dl.AddQuadFilled(p - new Vector2(0, d), p + new Vector2(d, 0), p + new Vector2(0, d), p - new Vector2(d, 0), C(t.Warning));
                }
            }
            foreach (var (position, kind) in Loot)
            {
                if (!s.RadarItems[kind]) continue;
                var o = Map(position);
                if (!Inside(o, marker)) continue;
                var half = marker * .75f;
                dl.AddRectFilled(center + o - new Vector2(half), center + o + new Vector2(half), C(t.Muted), ui.S(1));
            }
            if (s.RadarTeam)
                foreach (var mate in Team)
                {
                    var o = Map(mate);
                    if (Inside(o, marker)) dl.AddCircleFilled(center + o, marker, C(s.TeamColor), 16);
                }
            if (s.RadarEnemies)
                for (var i = 0; i < Enemies.Length; i++)
                {
                    var enemy = Enemies[i];
                    var o = Map(enemy);
                    if (Inside(o, marker))
                    {
                        dl.AddCircleFilled(center + o, marker, C(s.EnemyColor), 16);
                        var tag = (s.MarkerNames ? "P" + (i + 1) : "") + (s.MarkerNames && s.MarkerDistance ? " " : "") +
                            (s.MarkerDistance ? $"{enemy.Length():0}m" : "");
                        if (tag.Length > 0)
                            dl.AddText(Font(Face.Mono), Size(Face.Mono), center + o + new Vector2(marker + ui.S(4), -Size(Face.Mono) / 2), C(t.Muted), tag);
                    }
                    else
                    {
                        // Out of range: pin to the edge as a hollow marker.
                        var edge = square ? o / MathF.Max(MathF.Abs(o.X), MathF.Abs(o.Y)) * (r - marker - ui.S(3))
                            : Vector2.Normalize(o) * (r - marker - ui.S(3));
                        dl.AddCircle(center + edge, marker * .85f, C(s.EnemyColor with { W = .7f }), 16, ui.S(1.2f));
                    }
                }

            var (fx, fy) = (MathF.Cos(direction), MathF.Sin(direction));
            var forward = new Vector2(fx, fy);
            var side = new Vector2(-fy, fx);
            dl.AddTriangleFilled(center + forward * ui.S(8), center - forward * ui.S(5) + side * ui.S(5.5f),
                center - forward * ui.S(5) - side * ui.S(5.5f), C(t.Text));
            if (square) dl.AddRect(lowerLeft, upperRight, C(t.Border), ui.S(12));
            else dl.AddCircle(center, r, C(t.Border), 96, ui.S(1));
        }
        finally
        {
            dl.PopClipRect();
            ImGui.PopStyleVar();
        }
    }

    private void Legend()
    {
        var s = Settings;
        var t = ui.Theme;
        var entries = new (string Text, Vector4 Color, bool On, int Shape)[]
        {
            ("ENEMY", s.EnemyColor, s.RadarEnemies, 0), ("TEAM", s.TeamColor, s.RadarTeam, 0),
            ("ITEM", t.Muted, s.RadarItems.Any(x => x), 1), ("OBJECTIVE", t.Warning, s.Objectives, 2)
        };
        var x = cardX;
        var middle = rowY + 10;
        foreach (var (text, color, on, shape) in entries)
        {
            var tint = on ? color : Dim with { W = .6f };
            var p = P(x + 4, middle);
            if (shape == 0) dl.AddCircleFilled(p, ui.S(3), C(tint), 12);
            else if (shape == 1) dl.AddRectFilled(p - new Vector2(ui.S(2.5f)), p + new Vector2(ui.S(2.5f)), C(tint));
            else dl.AddQuadFilled(p - new Vector2(0, ui.S(3.5f)), p + new Vector2(ui.S(3.5f), 0), p + new Vector2(0, ui.S(3.5f)), p - new Vector2(ui.S(3.5f), 0), C(tint));
            Label(x + 13, middle, text, Face.Mono, on ? t.Muted : Dim);
            x += 13 + Measure(text, Face.Mono) + 18;
        }
        rowY += 20;
    }

    // ---- Drawing helpers ---------------------------------------------------------------------------------

    private enum Face { Body, Heading, Mono }
    private Vector4 Dim => Vector4.Lerp(ui.Theme.Muted, ui.Theme.Background, .4f);
    private Vector4 OffLabel => Vector4.Lerp(ui.Theme.Muted, ui.Theme.Text, .45f);
    private Vector2 P(float x, float y) => origin + new Vector2(x, y) * ui.Scale;
    private static uint C(Vector4 color) => ImGui.GetColorU32(color);
    private static Vector2 Round(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));

    private void Fill(float x, float y, float w, float h, Vector4 color, float rounding = 0, ImDrawFlags flags = ImDrawFlags.None)
        => dl.AddRectFilled(Round(P(x, y)), Round(P(x + w, y + h)), C(color), ui.S(rounding), flags);

    private void Stroke(float x, float y, float w, float h, Vector4 color, float rounding = 0)
        => dl.AddRect(Round(P(x, y)) + new Vector2(.5f), Round(P(x + w, y + h)) - new Vector2(.5f), C(color), ui.S(rounding));

    private void Segment(float x, float y, float x2, float y2, Vector4 color, float thickness) => Segment(new(x, y), new(x2, y2), color, thickness);
    private void Segment(Vector2 a, Vector2 b, Vector4 color, float thickness) => dl.AddLine(P(a.X, a.Y), P(b.X, b.Y), C(color), ui.S(thickness));

    private ImFontPtr Font(Face face)
    {
        var font = face switch { Face.Heading => ui.Fonts.Heading, Face.Mono => ui.Fonts.Small, _ => ui.Fonts.Body };
        return font.Equals(default(ImFontPtr)) ? ImGui.GetFont() : font;
    }

    /// <summary>Uses each atlas font at its native size so text stays crisp; host fonts fall back to proportional sizes.</summary>
    private float Size(Face face)
    {
        var font = face switch { Face.Heading => ui.Fonts.Heading, Face.Mono => ui.Fonts.Small, _ => ui.Fonts.Body };
        if (!font.Equals(default(ImFontPtr))) return font.FontSize;
        return ImGui.GetFontSize() * (face == Face.Heading ? 1.15f : face == Face.Mono ? .85f : 1);
    }

    private float Measure(string text, Face face)
    {
        var font = Font(face);
        ImGui.PushFont(font);
        try { return ImGui.CalcTextSize(text).X * (Size(face) / font.FontSize) / ui.Scale; }
        finally { ImGui.PopFont(); }
    }

    /// <summary>Draws text vertically centered on <paramref name="middle"/> (design units).</summary>
    private void Label(float x, float middle, string text, Face face, Vector4 color)
    {
        var size = Size(face);
        dl.AddText(Font(face), size, new Vector2(P(x, 0).X, MathF.Round(P(0, middle).Y - size / 2)), C(color), text);
    }

    private void RightLabel(float right, float middle, string text, Face face, Vector4 color)
        => Label(right - Measure(text, face), middle, text, face, color);

    private void Icon(int kind, Vector2 c, Vector4 color)
    {
        var col = C(color);
        var w = ui.S(1.5f);
        var center = P(c.X, c.Y);
        switch (kind)
        {
            case 0: // Visuals: an eye.
                dl.AddEllipse(center, new Vector2(ui.S(8.5f), ui.S(5.5f)), col, 0, 28, w);
                dl.AddCircleFilled(center, ui.S(2.4f), col, 12);
                break;
            case 1: // Camera: a body and lens.
                dl.AddRect(P(c.X - 8.5f, c.Y - 5), P(c.X + 8.5f, c.Y + 6.5f), col, ui.S(2.5f), ImDrawFlags.None, w);
                dl.AddCircle(P(c.X, c.Y + .8f), ui.S(3.2f), col, 16, w);
                dl.AddLine(P(c.X - 3, c.Y - 7.5f), P(c.X + 3, c.Y - 7.5f), col, w);
                break;
            case 2: // Radar: rings and a sweep.
                dl.AddCircle(center, ui.S(8.5f), col, 28, w);
                dl.AddCircle(center, ui.S(4), col, 20, w);
                dl.AddLine(center, P(c.X + 6, c.Y - 6), col, w);
                dl.AddCircleFilled(P(c.X - 3.5f, c.Y + 4.5f), ui.S(1.6f), col, 8);
                break;
            default: // Config: three vertical sliders.
                foreach (var (dx, knob) in new[] { (-6f, -2.5f), (0f, 3.5f), (6f, -4.5f) })
                {
                    dl.AddLine(P(c.X + dx, c.Y - 8), P(c.X + dx, c.Y + 8), col, w);
                    dl.AddRectFilled(P(c.X + dx - 2.5f, c.Y + knob - 1.5f), P(c.X + dx + 2.5f, c.Y + knob + 1.5f), col, ui.S(1));
                }
                break;
        }
    }

    private static string KeyLabel(ImGuiKey key) => key switch
    {
        ImGuiKey.None => "",
        ImGuiKey.Insert => "INS",
        ImGuiKey.Delete => "DEL",
        ImGuiKey.PageUp => "PGUP",
        ImGuiKey.PageDown => "PGDN",
        ImGuiKey.LeftShift => "LSHIFT",
        ImGuiKey.RightShift => "RSHIFT",
        ImGuiKey.LeftCtrl => "LCTRL",
        ImGuiKey.RightCtrl => "RCTRL",
        ImGuiKey.LeftAlt => "LALT",
        ImGuiKey.RightAlt => "RALT",
        ImGuiKey.CapsLock => "CAPS",
        ImGuiKey.MouseRight => "M2",
        ImGuiKey.MouseMiddle => "M3",
        ImGuiKey.MouseX1 => "M4",
        ImGuiKey.MouseX2 => "M5",
        _ => ImGui.GetKeyName(key).ToUpperInvariant() is { Length: > 6 } name ? name[..6] : ImGui.GetKeyName(key).ToUpperInvariant()
    };

    // ---- Native hit targets ------------------------------------------------------------------------------

    /// <summary>
    /// A transparent native button: mouse, keyboard navigation, focus and disabled behavior come from ImGui.
    /// ImGui draws the keyboard focus highlight itself, in the theme's accent.
    /// </summary>
    private bool Hit(string key, float x, float y, float w, float h)
    {
        ImGui.SetCursorScreenPos(P(x, y));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        bool pressed;
        try { pressed = ImGui.Button("##" + key, Vector2.Max(new Vector2(1), new Vector2(w, h) * ui.Scale)); }
        finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(3); }
        Remember(key);
        return pressed;
    }

    private float Hover(string key) => ui.Animate(key + ":hover", ImGui.IsItemHovered() ? 1 : 0, .12f);
    private void Remember(string key)
    {
        items[key] = new UiItem(ImGui.GetItemID(), key, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        if (ImGui.IsItemFocused()) FocusedControl = key;
    }
}
