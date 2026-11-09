# Effects and images

Effects use the same `Ui` instance as your controls. They submit ordinary ImGui draw commands, so an existing Direct3D host needs no extra frame loop or shader integration.

## Moving backgrounds and gradients

Call these **inside an ImGui window, before its foreground content**:

```csharp
var position = ImGui.GetWindowPos() + new Vector2(1);
var size = ImGui.GetWindowSize() - new Vector2(2);

ui.Gradient(position, size,
    UiTheme.Rgb(0x121315), UiTheme.Rgb(0x26311D), vertical: true);

ui.Particles(position, size, new ParticleOptions
{
    Count = 40,
    Speed = 12,
    Radius = 1.2f,
    ConnectionDistance = 75,
    Color = ui.Theme.Accent with { W = .25f },
    Seed = 17
});
```

Positions and sizes are **screen pixels** here, matching ImGui's draw lists. Particle speed, radius and connection distance are design units, scaled through `Ui.Scale`. Set `ConnectionDistance = 0` for unconnected drifting points. Count is capped at 128; connections compare nearby pairs. The seed makes the pattern reproducible. `ui.ReducedMotion = true` draws a fixed particle arrangement.

For a four-corner gradient, pass colors in **top-left, top-right, bottom-right, bottom-left** order. Gradients and particles consume no layout space, capture no input, and stay inside the current window's clipping region. Particles additionally clip to their supplied rectangle. These are square draw regions; inset them within rounded window edges when needed.

## Animate any numeric property

```csharp
float underlineX = ui.Animate("tab-position", selectedTab * 125f,
    duration: .2f, easing: Easing.CubicOut);

float opacity = ui.Pulse(period: 3, minimum: .25f, maximum: .8f);
```

Animation IDs are scoped by the current ImGui ID stack. Keep them stable and unique for independent properties. The first sample starts at the supplied target. Later target changes animate from the current value, including rapid reversals. Durations are seconds; timing uses ImGui's clock, and sampling twice in a frame does not advance the animation twice. `Linear`, `SmoothStep` and `CubicOut` are available. Values stop exactly at the target.

Use the value for positions, sizes, colors via `Vector4.Lerp`, or other draw parameters. `Animate` uses whatever units the target uses; it does not scale the result for you.

Reduced motion snaps numeric animations and makes `Pulse` return its maximum. Unused numeric/transition state is periodically pruned; a value reintroduced after pruning starts at its new target.

## Tabs, toggles and checkboxes

```csharp
if (ui.Tab("visuals", "VISUALS", selectedTab == 0, new Vector2(115, 32)))
    selectedTab = 0;

ui.Checkbox("boxes", "Bounding box", ref settings.Boxes);
ui.Toggle("enabled", "Enable overlay", ref settings.Enabled);
```

These keep native ImGui mouse, keyboard, focus and disabled behavior. `Tab` animates selected/hover colors; `Checkbox` animates its fill and check stroke; `Toggle` animates the knob and track. Values change immediately on activation, while presentation animates. Visible strings are separate from stable IDs.

`Tab` size is in design units. Pass `drawIndicator: false` if you draw a shared moving underline using `ui.Animate`, as the Nightshift sample does. Put these controls inside `ui.Style()` when used outside `Menu.Draw()`.

## Fade and slide a page

Inside the page's child window, before drawing its contents:

```csharp
ui.Transition("settings-page", selectedTab, () =>
{
    DrawCurrentPage();
}, duration: .18f, offset: new Vector2(0, 7));
```

The key changes when the selected page changes. The first page appears immediately; subsequent pages fade and slide in. Only the incoming page's callback runs, and its controls stay disabled until it settles. Alpha and disabled state restore even if the callback throws. The offset is in design units and affects flow layout; absolute `SetCursorPos` calls in the callback override the slide offset.

Use separate transition IDs for independent content regions. Reduced motion removes the fade, slide and input delay. This is an incoming-content transition, not a crossfade that executes both pages' callbacks.

## Show an image in any host

The core takes a **borrowed renderer texture handle**, not a filename:

```csharp
// Upload through your existing renderer once. The ID must be understood by that renderer.
var image = new UiImage(nativeTextureId, pixelWidth, pixelHeight);

// In your frame:
ui.Image(image, new Vector2(220, 80), ImageFit.Cover, rounding: 4);
```

`Image` reserves the requested layout space; its size and rounding are design units. `DrawImage` draws at explicit screen-pixel coordinates without affecting layout. Both accept an optional RGBA tint.

- `Contain`: preserves aspect ratio and centers the whole image with empty space around it.
- `Cover`: fills the rectangle and crops UVs around the center.
- `Stretch`: fills the rectangle without preserving aspect ratio.

Your host owns texture upload, lifetime and recreation after a device reset. A Direct3D host may use its existing texture-view mapping; an OpenGL host may use its texture name. Do not pass a file handle or a texture ID from another renderer/context. The core never frees the borrowed texture.

## Load images with the optional Windows host

The adapter offers an image-aware factory without changing existing factories:

```csharp
using var app = new OverlayApp("My overlay", (ui, images) =>
{
    var banner = images.Load(Path.Combine(AppContext.BaseDirectory, "assets", "banner.png"));
    return new MyMenu(ui, banner); // Your IMenuSurface implementation.
});
app.Run();
```

`OverlayImages.Load` decodes a local image to RGBA and caches it by full path. `CreateRgba(width, height, pixels)` uploads your own tightly packed RGBA8 buffer, first row at the top. Load once during setup, keep the `UiImage`, and draw it each frame. These methods must run on the overlay's render thread. The adapter owns those uploads and frees them on close. Using the loader after close throws. Animated file playback is not implemented.

The decoder dependency is confined to the Windows adapter. Existing hosts can keep their current image-loading code. The core still depends only on ImGui.NET.

See `samples/GameMenu/NightshiftMenu.cs` for a complete Effects tab, and `samples/GameMenu/Program.cs` for loading its bundled image. The demo's signal image is locally authored sample artwork with no external asset dependency.
