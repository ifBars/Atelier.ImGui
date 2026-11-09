# Validation of the initial implementation

Cinder navigation cleanup: removed the duplicate icon rail and its mismatched targeting/sun imagery. The only navigation is Players, World & view, and Config. The canvas is now 690×568 with the same settings and preview widths, and its declared size is shared by the sample host and validation harness. The existing input and 100%/125%/150% captures were rerun, along with the native Cinder host capture.

Cinder refinement: checked the annotated tab strip, group captions and footer dropdown. Captions are inset inside their frames; tabs use measured text centering; all footer controls align to one height. The Cinder checks now include 125% scaling, footer geometry and native keyboard selection through the styled dropdown, alongside the prior 100%/150% checks. The fractional-DPI font rounding mismatch found at 125% was corrected by calculating frame padding from the active font size.

Cinder addition: eighteen behavior groups pass. The three classic-menu pages have 100% and 150% captures. Checks cover master/disabled checkbox behavior, native slider changes, independent profile snapshots, tab input, reset, drag notification and close. The Windows/OpenGL host was run with Tahoma atlas setup and its own framebuffer captured and inspected. Vanta and the previous examples retain their existing layouts.

Reference-led examples: seventeen behavior groups pass with Vanta and Redline coverage. All seven new pages render at 100% and 150%. Checks include native input state changes, profile save/load, reset/apply, drag notification and close. Both examples were also run in the Windows/OpenGL host with their downloaded artwork and bundled display fonts, captured from their own framebuffer, and visually inspected. The optional host now provides a pre-upload font configuration callback; the original factories remain supported.

Effects addition: fifteen behavior groups pass, including numeric timing at 30/120 FPS, continuous retargeting, reduced-motion snapping/frozen backgrounds, transition input gating/exception cleanup, image fit/UV bounds and animated checkbox input. The headless renderer now samples full RGBA textures. All five Nightshift pages are captured at 100%/150%; the native OpenGL Effects capture includes a PNG decoded, cached and uploaded through the host. Loader invalidation after close is checked. Image loading is confined to the optional host.

The game-menu addition was subsequently checked with ten behavior groups, including tab input, drag notification and close at 100% and 150%. All four game-menu tabs have rasterized captures. The real Windows/OpenGL game host also rendered and captured successfully. Its controls and preview are local-only demonstration state.

Validated on Windows on 2026-11-08, targeting .NET 8 and ImGui.NET 1.91.6.1.

- Release solution build: succeeded with zero warnings and errors, including the core, Windows adapter, standalone demo, existing-host example and test executable.
- Native ImGui behavior harness: nine groups passed. Covers argument validation, mouse/keyboard toggle behavior, disabled input, text/slider mutation, independent scoped IDs, dropdown selection, color-picker mutation, shell navigation/close, drag notification, style/exception cleanup, closed-menu geometry and page rendering.
- Gallery: all four showcase pages at 100% and 150%, narrow viewports and the separate 560×408 existing-host example. The images were inspected for layout; popup and native-host output were also inspected. Files are generated under `artifacts/screenshots/`.
- Actual Windows/OpenGL host: rendered 45 frames, captured its GPU framebuffer and closed cleanly.
- Self-contained Windows x64 distribution: published to `artifacts/ServerManager/`, then its `ServerManager.exe --smoke` completed with exit code 0.
- Local package: `artifacts/packages/Atelier.ImGui.0.1.0.nupkg` inspected; contains the core assembly and README, with ImGui.NET as its sole package dependency.
- NuGet vulnerability query, including transitive dependencies: no known vulnerable packages reported by the configured sources at validation time.

This is not a runtime integration test of the external reference application. Its files were not modified. OS window dragging has code-path/native-host coverage and the core drag notification has an input test; an actual user drag across monitors was not automated. Per-monitor font rebuilding and native host resizing are not implemented. The standalone demonstration uses local in-memory application state.

Run the commands in the README to regenerate current evidence after behavioral changes. Build, headless input, software-rendered galleries and native GPU rendering are different checks; keep their conclusions separate.
