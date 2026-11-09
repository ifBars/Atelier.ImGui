# Reference-led game menus

The examples are native ImGui.NET applications and can also be used as `IMenuSurface` implementations inside an existing host. Their settings affect local demo state only.

## Cinder / compact classic menu

```powershell
dotnet run --project samples/Cinder -c Release
```

A 690×568 classic layout with square edges, fine bevels, a quiet scan texture, Tahoma at compact UI sizes and a brass accent. A single text tab bar groups player overlays/materials under Players, environment/camera controls under World & view, and profiles/menu styling under Config. Text is limited to control labels and short action feedback. Profile Save/Load uses independent in-memory snapshots; disabled ESP controls reject input when the master option is off.

The refined layout uses inset group headers, a centered three-part tab strip, and consistent dropdown/button heights. Dropdowns keep native popup and keyboard selection behavior while using a small custom chevron. Frame padding is calculated from the actual font metrics so 125% DPI font rounding does not misalign the footer. Current captures cover 100%, 125% and 150%.

The downloaded [classic Skeet reference](https://commons.wikimedia.org/wiki/File:Skeet_menu.png) is stored as `artifacts/design-references/skeet-classic.png` and was visually inspected for proportions, caption treatment and control density. It is research material and is not included in the demo. Cinder requires no downloaded image or proprietary game asset. Its player drawing and menu implementation are original sample code.

- [Menu source](../samples/Cinder/CinderMenu.cs)
- [Native capture](images/cinder.png)
- `CinderMenu.ConfigureFonts` loads installed Windows Tahoma fonts before host atlas upload; the font files are not redistributed.

To use it with an existing host, include the menu class plus the shared sample helpers, construct `new CinderMenu(ui)` once and call `Draw(fillViewport: true)` in the existing frame. Keep your renderer, native dragging and input handling. Use `CinderMenu.CanvasSize` (690×568 design units) multiplied by the UI scale for the client area.

```powershell
dotnet publish samples/Cinder -c Release -r win-x64 --self-contained true -o artifacts/Cinder
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --cinder
```

## Kestrel / compact paid-menu layout

```powershell
dotnet run --project samples/Kestrel -c Release
```

A 760×500 violet-on-ink layout built for density: 28-unit rows, labels on the left, every control right-aligned to one 150-unit column, and uppercase Geist Mono captions on cards. Navigation has exactly two levels: an icon rail picks the category (Visuals, Camera, Radar, Config), and the header shows that category's tabs with a sliding underline. The rail remembers each category's last tab. Each column's last card extends to the footer, so every page sits on the same grid.

Controls are native ImGui items with custom drawing on top: transparent buttons for checkboxes, segmented controls, profile rows and keybind chips; `SliderFloat` for sliders (the drawn track uses ImGui's own input span, so clicks land where the knob is drawn); `BeginCombo` for single- and multi-select dropdowns; `ColorEdit4` for color chips; and `InputTextWithHint` for profile names. Keyboard navigation, focus, popups and disabled scopes therefore behave like ImGui. Disabled options dim under their master option.

- **Keybind chips**: click, then press a key or the right, middle or side (M4/M5) mouse button. Escape cancels, Backspace or Delete clears, and clicking elsewhere cancels. `IsCapturingKey` tells the host to suspend its own hotkeys while a chip is listening. Config → Interface lists every bind in one card.
- **Profiles**: in-memory snapshots with load, save, create (Enter or Create), delete and reset. The footer shows the active profile and whether values have changed since it was saved. Before loading another profile, the right card lists the values that would change. Copy and Import use the clipboard and a single-line `kestrel1;` text format.
- **Radar preview**: shape, zoom, opacity, rotation, the FOV cone (driven by Camera → World FOV), marker colors and size, item filters and distance or name tags all redraw the preview.
- **Motion**: rail/tab indicators, check fills, segmented pills and hovers animate in 0.12–0.2 s, and pages fade in over 0.14 s. The radar sweep is the only continuous motion. Reduce motion stops it and settles transitions.
- **Escape**: closes the menu (Config → Interface → *Close with Escape*), except when a popup, text field or key capture used the key.

Geist and Geist Mono (SIL OFL 1.1) are bundled; see the [asset credits](../samples/Kestrel/assets/ASSET-CREDITS.md). Fonts are loaded at the exact sizes they are drawn at (13/15/11 units, rounded to the DPI), so text stays crisp at 100%, 125% and 150%. Without `ConfigureFonts`, the menu uses the host's current font at proportional sizes.

- [Menu source](../samples/Kestrel/KestrelMenu.cs) and [settings](../samples/Kestrel/KestrelSettings.cs)
- Captures: [players](images/kestrel.png), [radar](images/kestrel-radar.png)

### Using Kestrel in an existing host

Kestrel depends only on the core library and ImGui.NET; it doesn't need `StyledMenus` or unsafe code. Copy `KestrelMenu.cs` and `KestrelSettings.cs`, plus the `assets` folder if you want the bundled fonts.

```csharp
// Once, after the host has chosen its UI scale. Give Kestrel its own Ui: it sets the theme and ReducedMotion.
var ui = new Ui(scale: hostScale);
KestrelMenu.ConfigureFonts(ui, assetDirectory); // Optional; call before the host uploads its font atlas.
var settings = new KestrelSettings();            // Your state: the menu edits this instance in place.
var menu = new KestrelMenu(ui, settings);

// Each frame, between your NewFrame() and Render():
menu.HandleMenuKey(); // Optional: toggles IsOpen with settings.MenuKey; skips an unbound key and active key capture.
menu.Draw(fillViewport: true);
```

If the host already owns a show/hide hotkey, skip `HandleMenuKey` and suspend that hotkey while `menu.IsCapturingKey` is true. Never pass `ImGuiKey.None` to `ImGui.IsKeyPressed`; clearing a keybind sets it to `None`.

Size the host client area to `KestrelMenu.CanvasSize * hostScale`. Subscribe to `DragStarted` if your native window should move when the header is dragged. Profile loads copy values into the same `KestrelSettings` instance, including array contents, so references your application holds remain current. Before each frame the menu calls `Settings.Normalize()`: category arrays with the wrong length are resized, option indices are clamped, and keys that cannot be bound are cleared. Imported clipboard text with such values is rejected outright. Deleting the active profile leaves the current values unassociated (the footer shows *No profile*) until you save or load one.

### References for Kestrel

| Local reference | Source | Design observations used |
| --- | --- | --- |
| `unity-uitk-showcase.png` | [sinanata/unity-ui-toolkit-design-system](https://github.com/sinanata/unity-ui-toolkit-design-system) `docs/screenshots/showcase.png` (commit `a2089d5`) | Small uppercase card captions, label-left/control-right setting rows, segmented pills with a raised active segment, restrained single-accent palette |
| Cinder, Vanta and Nightshift in this repository | Existing samples | Dense two-column cards and color-chip rows (Cinder), icon rail with an animated selection (Vanta), sliding tab underline and page transition (Nightshift) |

The screenshot is stored in the ignored `artifacts/design-references/` directory and was opened and inspected before implementation. It is not bundled. Game-UI galleries such as gameuidatabase.com, interfaceingame.com and Dribbble could not be reached from the network-restricted build environment, so they were not used. Kestrel's composition, labels, icons, brand mark and radar are original draw commands.

## Vanta / orbital toolkit

```powershell
dotnet run --project samples/Vanta -c Release
```

A 960×620 dark/cyan composition with narrow icon navigation, image-backed orbital artwork, animated flight markers and a flat settings inspector. Rajdhani gives the headings a technical character; body text remains Segoe UI. Visuals, Navigation, World and Profiles each draw real controls. The preview reacts to overlay, label, trail, opacity and color settings. Save/Load stores settings in memory, and reduced motion stops orbit movement and transitions.

- [Menu source](../samples/Vanta/VantaMenu.cs)
- [Native rendered capture](images/vanta.png)
- [Asset credits](../samples/Vanta/assets/ASSET-CREDITS.md)

## Redline / driver's toolkit

```powershell
dotnet run --project samples/Redline -c Release
```

A 900×600 red/ivory composition with condensed display typography, a cinematic automotive image, angled shapes and flat switch rows. There is no sidebar or nested panel layout. Driving, Camera and HUD expose native input controls; Apply reports a local applied state, and Reset restores defaults. Switching pages animates the selected-tab indicator and content fade.

- [Menu source](../samples/Redline/RedlineMenu.cs)
- [Native rendered capture](images/redline.png)
- [Asset credits](../samples/Redline/assets/ASSET-CREDITS.md)

## References downloaded and inspected

Downloaded screenshots live in the ignored `artifacts/design-references/` directory. They are research material, separate from the distributable artwork. The reference images were opened and visually inspected before implementation.

| Local reference | Source | Design observations used |
| --- | --- | --- |
| `neverlose-showcase.jpg` | [Neverlose v3 showcase](https://forum.neverlose.cc/t/neverlose-v3-showcase/301490); [downloaded thumbnail](https://img.youtube.com/vi/7rkLvt-xJrU/maxresdefault.jpg) | Control density, quiet dark surface hierarchy, compact navigation, visual subject beside settings |
| `persona-settings.jpg` | [Persona 5 Royal discussion with screenshot](https://steamcommunity.com/app/1687950/discussions/0/828204733286101972/); [downloaded screenshot](https://i.postimg.cc/Gt4V889L/1687950-1.jpg) | Strong red/black selection, angular shapes and typography-led hierarchy |
| `forza-settings.jpg` | [Forza Horizon 5 menu deep dive](https://caniplaythat.com/2021/11/04/forza-horizon-5-accessibility-menu-deep-dive/); [downloaded screenshot](https://i0.wp.com/caniplaythat.com/wp-content/uploads/2021/10/Forza-Horizon-5-HUD-and-gameplay-menu.jpg) | Flat setting rows, condensed labels, readable information over game imagery |

The initial Neverlose forum-image download was challenged by its CDN. The publicly accessible showcase thumbnail was downloaded instead. No browser automation or challenge bypass was used.

Both implementations use their own compositions, artwork selection, labels and draw commands. Reference screenshots are not bundled with either demo. Font licenses and photo credits accompany the sample assets.

## Integrating into the original host shape

The layout classes depend on Atelier plus the small `samples/StyledMenus` project. That project contains shared sample drawing helpers and no native window, graphics backend or input hooks.

1. Reference the core and `StyledMenus.csproj` (or include its helper source).
2. Include the selected example's menu class in your project.
3. Upload its artwork using your existing renderer and construct a borrowed `UiImage`.
4. Create the menu once, then call `Draw(fillViewport: true)` from the existing UI callback.
5. Keep your existing drag/close handling, subscribing to `DragStarted` if needed.

```csharp
var menu = new VantaMenu(ui, borrowedArtwork); // Or new RedlineMenu(ui, borrowedArtwork).
// Each frame, between your own NewFrame() and Render():
menu.Draw(fillViewport: true);
```

For the intended typography, load the bundled display font in the host's atlas **before GPU upload**, then assign the handle to `Ui.Fonts.Heading`. The optional Windows adapter exposes `OverlayOptions.ConfigureFonts` for this; the example entry points show it. Existing font choices also work.

The samples use fixed logical canvases and support 100%/150% scaling. Match the host client area to the design size multiplied by `Ui.Scale`; arbitrary responsive resizing is not implemented here. Slider rows use native ImGui slider interaction and keyboard navigation, with custom drawing; direct numeric text entry is disabled for those sample sliders.

## Local distributions

```powershell
dotnet publish samples/Kestrel -c Release -r win-x64 --self-contained true -o artifacts/Kestrel
dotnet publish samples/Vanta -c Release -r win-x64 --self-contained true -o artifacts/Vanta
dotnet publish samples/Redline -c Release -r win-x64 --self-contained true -o artifacts/Redline
```

Keep each output folder intact, including assets and font licenses.

## Checks

The main behavior executable covers Kestrel at 100%, 125% and 150% (every page, both popups, and the input checks listed in [validation](validation.md)), and covers Vanta and Redline at 100% and 150%, including navigation, switches, slider changes, profile save/load or reset/apply, drag notification and close. Every page has a software-rendered capture. Actual OpenGL host captures use:

```powershell
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --kestrel
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --vanta
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --redline
```

Native screenshots are read from the application's own framebuffer. Desktop control is not needed.

