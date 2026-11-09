# Recipes for generating menus with Atelier

Use this guide alongside the compiling examples. Read public signatures in `Ui`, `MenuOptions` and `MenuPage` before inventing an API.

## Start with the right integration

For compact game/mod menus, use `samples/GameMenu/NightshiftMenu.cs` as the layout reference. It implements `IMenuSurface` and uses scoped Atelier styling with native ImGui controls. The desktop `Menu` shell is optional. Set `Ui.StackFieldsBelow = 0` when dense columns should remain inline. Keep layout dimensions scaled through `Ui.S()` and use the host-owned font handles.

- Existing context/renderer/window: reference **Atelier.ImGui** only. Create `Ui` and `Menu` once and call `Menu.Draw(fillViewport: true)` inside the existing frame.
- Existing layout: use `using (ui.Style())` and call components inside the current ImGui window.
- New standalone Windows tool: use **Atelier.ImGui.Windows** and the `ServerManager` entry point.

## Build a page

Group related settings into one section, use short labels, and put explanations in the description parameter. The toolkit handles label/control placement, small widths, padding and clipping.

```csharp
new MenuPage("recovery", "Recovery", "Keep your server available.", page =>
{
    page.Section("restarts", "Process recovery", panel =>
    {
        panel.Toggle("automatic", "Automatic restart", ref state.AutoRestart,
            "Recover when the process exits.");
        using (panel.Disabled(!state.AutoRestart))
            panel.Slider("delay", "Restart delay", ref state.Delay, 1, 60, "%.0f sec");
    });
});
```

`state` belongs to the application. The example assumes those are mutable fields; C# properties cannot be passed by `ref`. For a property, copy it to a local, pass the local by `ref`, then assign back when the component reports a change.

## Stable identity

Use stable IDs such as `"automatic"`, with a separate human-readable label. Do not use array positions, current values, random GUIDs per frame or translated labels as identity. For repeated items:

```csharp
foreach (var server in servers)
{
    using (ui.Id(server.Id))
        ui.Toggle("enabled", server.Name, ref server.Enabled);
}
```

Page IDs, section IDs and explicit ID scopes compose naturally. Reusing an ID in the same scope is a caller bug.

## Actions and state

```csharp
if (ui.Button("apply", "Apply changes", ButtonKind.Primary))
    settingsService.Apply();

using (ui.Disabled(!canRestart))
    if (ui.Button("restart", "Restart server"))
        serverService.RequestRestart();
```

Prefer one primary action per decision area. Keep destructive actions visually separate. Do slow work outside the render callback and report its state back on the UI thread.

## Visual consistency

Use `UiTheme` colors, `Section`, `Heading`, `MutedText` and the shared controls. Customize branding through `MenuOptions`, and the palette through a record copy of `UiTheme`. Avoid hand-positioning routine settings, hardcoded raw pixel colors, gradients on every surface, decorative status data, or reimplementing widgets with mouse rectangles.

The core theme does not install fonts or alter the host globally. Use host-owned fonts and a scale that matches them. Set `ReducedMotion` when the application requests it.

## Verification

For animation, background or image work, follow `docs/effects.md`. Use `Ui.Animate` and `Ui.Transition` with stable IDs instead of managing time dictionaries in each menu. Load textures once through the host and keep their borrowed `UiImage` values. Honor `Ui.ReducedMotion` and draw background effects before foreground content. Do not add a second renderer or image decoder to the core.

Build the affected project. Use native input tests for changed behavior. Render the actual ImGui output at the target size and DPI; check popups, scrolling and long labels. Build success alone does not prove interaction or appearance.

`Ui.ItemDrawn` reports native IDs and control bounds if your own test harness needs to target them. Its callback runs synchronously during drawing and should be a lightweight observer.
