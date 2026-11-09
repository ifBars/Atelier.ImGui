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
dotnet publish samples/Vanta -c Release -r win-x64 --self-contained true -o artifacts/Vanta
dotnet publish samples/Redline -c Release -r win-x64 --self-contained true -o artifacts/Redline
```

Keep each output folder intact, including assets and font licenses.

## Checks

The main behavior executable now covers both menus at 100% and 150%, including navigation, switches, slider changes, profile save/load or reset/apply, drag notification and close. Every page has a software-rendered capture. Actual OpenGL host captures use:

```powershell
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --vanta
dotnet run --project tests/Atelier.ImGui.Tests -c Release -- --host --redline
```

Native screenshots are read from the application's own framebuffer. Desktop control is not needed.

