using System.Numerics;
using ImGuiNET;

namespace Atelier;

public sealed record MenuOptions
{
    public string Title { get; init; } = "Application";
    public string Subtitle { get; init; } = "WORKSPACE";
    public string Footer { get; init; } = "Built with Atelier";
    public string Brand { get; init; } = "ATELIER";
    public string Monogram { get; init; } = "A";
    public string SidebarFooter { get; init; } = "DESKTOP TOOLKIT\nPreview 0.1";
    public Vector2 InitialSize { get; init; } = new(960, 720);
    public Vector2 MinimumSize { get; init; } = new(740, 540);
    public float SidebarWidth { get; init; } = 200;
    public bool Resizable { get; init; } = true;
}

/// <summary>A page's ID stays stable when its label changes. Render callbacks run each visible frame.</summary>
public sealed record MenuPage(string Id, string Label, string Description, Action<Ui> Draw);

/// <summary>An instance-owned, borderless ImGui shell. The host owns native windows, input and rendering.</summary>
public sealed class Menu : IMenuSurface
{
    private readonly Ui ui;
    private readonly string id;
    private readonly MenuOptions options;
    private readonly MenuPage[] pages;
    private int selected;
    private bool fillsViewport;

    public Menu(Ui ui, string id, MenuOptions options, params MenuPage[] pages)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Length == 0 || pages.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id) || p.Draw is null) ||
            pages.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != pages.Length)
            throw new ArgumentException("A menu requires pages with unique, nonempty IDs and render callbacks.", nameof(pages));
        if (!Valid(options.MinimumSize) || !Valid(options.InitialSize) ||
            options.InitialSize.X < options.MinimumSize.X || options.InitialSize.Y < options.MinimumSize.Y ||
            !float.IsFinite(options.SidebarWidth) || options.SidebarWidth < 120 ||
            options.SidebarWidth > options.MinimumSize.X - 300)
            throw new ArgumentException("Menu sizes must be positive and leave at least 300 units for content.", nameof(options));
        this.ui = ui;
        this.id = id;
        this.options = options;
        this.pages = [.. pages];
    }

    public bool IsOpen { get; set; } = true;
    /// <summary>Optional host notification when the title drag handle is pressed.</summary>
    public event Action? DragStarted;
    public string SelectedPage
    {
        get => pages[selected].Id;
        set
        {
            var index = Array.FindIndex(pages, p => p.Id == value);
            if (index < 0) throw new ArgumentException("Unknown menu page ID.", nameof(value));
            selected = index;
        }
    }

    public void Draw(bool fillViewport = false)
    {
        if (!IsOpen) return;
        using var style = ui.Style();
        fillsViewport = fillViewport;
        var viewport = ImGui.GetMainViewport();
        if (fillViewport)
        {
            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
        }
        else
        {
            ImGui.SetNextWindowSize(options.InitialSize * ui.Scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(options.MinimumSize * ui.Scale, new Vector2(float.MaxValue));
            ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2, ImGuiCond.FirstUseEver, new Vector2(.5f));
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove;
        // NoDecoration includes NoResize; opt back in for this shell's native ImGui resize grip.
        if (options.Resizable && !fillViewport) flags &= ~ImGuiWindowFlags.NoResize;
        var visible = ImGui.Begin("###atelier-" + id, flags);
        ImGui.PopStyleVar();
        try
        {
            if (!visible) return;
            DrawChrome();
            var size = ImGui.GetWindowSize();
            var rail = ui.S(options.SidebarWidth);
            ImGui.SetCursorPos(new Vector2(rail + ui.S(28), ui.S(94)));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            var body = ImGui.BeginChild("##body", new Vector2(size.X - rail - ui.S(48), size.Y - ui.S(140)));
            try
            {
                if (body)
                {
                    var page = pages[selected];
                    ui.Heading(page.Label, page.Description);
                    ImGui.PushID(page.Id);
                    try { page.Draw(ui); }
                    finally { ImGui.PopID(); }
                }
            }
            finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
        }
        finally { ImGui.End(); }
    }

    private void DrawChrome()
    {
        var t = ui.Theme;
        var position = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var rail = ui.S(options.SidebarWidth);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(position, position + new Vector2(rail, size.Y), Ui.Color(t.Sidebar), ui.S(12), ImDrawFlags.RoundCornersLeft);
        draw.AddLine(position + new Vector2(rail, 0), position + new Vector2(rail, size.Y), Ui.Color(t.Border));
        draw.AddLine(position + new Vector2(rail, ui.S(72)), position + new Vector2(size.X, ui.S(72)), Ui.Color(t.Border));
        draw.AddLine(position + new Vector2(rail, size.Y - ui.S(36)), position + new Vector2(size.X, size.Y - ui.S(36)), Ui.Color(t.Border));

        ImGui.SetCursorPos(new Vector2(ui.S(20), ui.S(22)));
        ui.Badge(options.Monogram);
        ImGui.SameLine();
        ImGui.SetCursorPosY(ui.S(27));
        ImGui.TextUnformatted(options.Brand);

        // Only the header is a drag handle. Controls elsewhere never move the menu.
        ImGui.SetCursorPos(new Vector2(rail + ui.S(24), ui.S(16)));
        ImGui.InvisibleButton("##drag", new Vector2(Math.Max(ui.S(30), size.X - rail - ui.S(86)), ui.S(40)));
        if (ImGui.IsItemActivated()) DragStarted?.Invoke();
        if (!fillsViewport && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        draw.AddText(position + new Vector2(rail + ui.S(28), ui.S(27)), Ui.Color(t.Text), options.Title);

        ImGui.SetCursorPos(new Vector2(size.X - ui.S(48), ui.S(19)));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        var closePos = ImGui.GetCursorScreenPos();
        if (ImGui.Button("##close", new Vector2(ui.S(28)))) IsOpen = false;
        ImGui.PopStyleColor();
        draw.AddLine(closePos + new Vector2(ui.S(10)), closePos + new Vector2(ui.S(18)), Ui.Color(t.Muted), ui.S(1.4f));
        draw.AddLine(closePos + new Vector2(ui.S(18), ui.S(10)), closePos + new Vector2(ui.S(10), ui.S(18)), Ui.Color(t.Muted), ui.S(1.4f));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close");

        ImGui.SetCursorPos(new Vector2(0, ui.S(86)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ui.S(12), ui.S(8)));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var navigation = ImGui.BeginChild("##navigation", new Vector2(rail, Math.Max(ui.S(40), size.Y - ui.S(160))),
            ImGuiChildFlags.AlwaysUseWindowPadding);
        try
        {
            if (navigation)
            {
                ImGui.SetCursorPosX(ui.S(22));
                using (ui.Font(ui.Fonts.Small)) ui.MutedText(options.Subtitle);
                ImGui.Dummy(new Vector2(0, ui.S(8)));
                for (var i = 0; i < pages.Length; i++)
                {
                    ImGui.SetCursorPosX(ui.S(12));
                    ImGui.PushID(pages[i].Id);
                    var active = selected == i;
                    var row = ImGui.GetCursorScreenPos();
                    ImGui.PushStyleColor(ImGuiCol.Button, active ? t.Accent with { W = .1f } : Vector4.Zero);
                    ImGui.PushStyleColor(ImGuiCol.Text, active ? t.Accent : t.Muted);
                    ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, .5f));
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
                    if (ImGui.Button(pages[i].Label + "###page", new Vector2(rail - ui.S(24), ui.S(40)))) selected = i;
                    ImGui.PopStyleVar(2);
                    ImGui.PopStyleColor(2);
                    if (active) draw.AddRectFilled(row + new Vector2(0, ui.S(11)), row + new Vector2(ui.S(3), ui.S(29)), Ui.Color(t.Accent), ui.S(2));
                    ImGui.PopID();
                }
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); ImGui.PopStyleVar(); }
        ImGui.SetCursorPos(new Vector2(ui.S(22), size.Y - ui.S(56)));
        using (ui.Font(ui.Fonts.Small)) ui.MutedText(options.SidebarFooter);
        ImGui.SetCursorPos(new Vector2(rail + ui.S(28), size.Y - ui.S(26)));
        using (ui.Font(ui.Fonts.Small)) ui.MutedText(options.Footer);
    }

    private static bool Valid(Vector2 size) => float.IsFinite(size.X) && float.IsFinite(size.Y) && size.X > 0 && size.Y > 0;
}
