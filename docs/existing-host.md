# Integrating into an existing ImGui.NET application

You can keep the same responsibilities as your reference setup:

| Existing responsibility | Integration |
| --- | --- |
| Native borderless window and process | Keep your implementation |
| Direct3D device, shaders, swapchain and composition | Keep your implementation |
| ImGui context, font atlas upload and input events | Keep your implementation |
| The method that draws the menu each frame | Call `Menu.Draw(fillViewport: true)` |
| Settings and actions | Bind controls to your existing state |
| Generated application metadata | No connection to this library |

The core package only submits ImGui widgets and draw commands. It does not access another process or assume an application domain.

## Complete shell

1. Reference **only** `Atelier.ImGui.csproj` in your existing project.
2. Keep the host's managed ImGui.NET and native `cimgui` at **1.91.6.1**. Native structs depend on the exact Dear ImGui version. Do not overwrite an older host's managed DLL while retaining its older native DLL.
3. Create the UI controller once, after deciding the host's DPI scale. The compiling `samples/ExistingHost/ExistingHostMenu.cs` shows the whole controller and needs no Windows APIs.
4. Call its `Draw()` from your current UI drawing method, after `ImGui.NewFrame()` and before `ImGui.Render()`. Do not add a second frame loop or an enclosing ImGui window.
5. When `controller.Menu.IsOpen` becomes false, apply your existing hide/close behavior. Setting it true lets the same instance draw again.

For a host with the same callback shape as your reference, the change is this small:

```csharp
// Create once; use your host's actual scale, not a second DPI calculation.
private ExistingHostMenu? controller;

protected override void BuildUi()
{
    controller ??= new ExistingHostMenu(scale: U);
    controller.Draw();
    if (!controller.Menu.IsOpen)
        Close(); // Or use your existing hide behavior.
}
```

`U`, `BuildUi` and `Close` above are **host-owned integration points**, not new library requirements. The reference window classes and their source are not included here. This guide is an integration recipe, not a claim that the external application has already been modified or tested.

The small sample uses a sidebar width of 140 design units and fits a 560×408 viewport. Set the host's design/client dimensions as usual. The main content scrolls and settings rows stack when the available content width becomes narrow.

## Native dragging

In fill-viewport mode the core does not move an ImGui window within your OS window. Keep your current native drag regions. If your host currently relies on the menu to initiate movement, subscribe to `Menu.DragStarted` and invoke your host's existing drag operation. The provided host adapter does this with screen-space mouse coordinates. The event is raised on the ImGui thread; do not create a second ImGui context there.

## Keep the current shell, replace only controls

If you want your existing sidebar and header to remain, create `Ui` once and use it inside the existing ImGui window:

```csharp
using (ui.Style())
{
    ui.Toggle("service", "Service enabled", ref settings.Enabled);
    ui.ColorPicker("color", "Highlight color", ref settings.Highlight);
}
```

The variables above are ordinary application fields. Callbacks can invoke your existing service methods when a button returns true. No new settings base class, dependency injection framework or serialization format is required.

You may mix ordinary `ImGui.*` calls inside sections; use standard ImGui stack discipline for your own calls. Library-provided sections and styles unwind their own scopes when a page callback throws.

## Fonts and scaling

With `Ui.Fonts` unset, all components use the font currently selected by your host. For a more refined hierarchy, load regular, semibold heading and smaller secondary fonts into your host's atlas, assign the returned handles to `ui.Fonts`, then rebuild/upload the atlas through the host as usual.

Use logical widget sizes with `Ui.Scale = hostScale` through the constructor. Do not multiply widget widths again; `Ui.Button` width and `MenuOptions` dimensions are design units. Ensure the font pixel size is scaled to match. `DisplayFramebufferScale` remains the renderer's responsibility.

## Compatibility limits

The core targets .NET 8 and the pinned ImGui.NET binding. It does not currently target .NET Framework, Unity's Mono profile or Hexa.NET.ImGui. Check the existing host's target framework and binding version before adding the project reference. The core is independent of Direct3D vs OpenGL, but those runtime/framework boundaries still matter.
