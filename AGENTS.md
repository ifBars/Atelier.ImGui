# Working on Atelier.ImGui

- Preserve the boundary: `src/Atelier.ImGui` submits UI only. Put native window, renderer, input and platform APIs in adapters such as `Atelier.ImGui.Windows`.
- Stay on ImGui.NET 1.91.6.1 unless deliberately migrating managed and native bindings together. ktsu is design inspiration, not a Hexa.NET dependency to add implicitly.
- The supplied application references are external context, never source to copy into this library.
- Add controls through native ImGui input behavior, with scoped style/ID cleanup and semantic theme colors. Keep state caller-owned.
- Create menus once. Keep page IDs independent of visible labels, and support the existing-host render callback without an additional context or frame loop.
- Use `docs/agent-guide.md` and `samples/ExistingHost` for integration patterns. Use bun for any future JavaScript package management.
- Validate with `dotnet build Atelier.ImGui.sln -c Release` and `dotnet run --project tests/Atelier.ImGui.Tests -c Release`. The test project is an executable; `dotnet test` does not execute it.
- For host/renderer changes, also run the bounded `--host` test. Inspect generated screenshots; keep runtime captures and publish outputs under ignored `artifacts/`.
- Existing successful evidence only applies to unchanged source/configurations. Distinguish headless input tests, rasterized galleries and native Windows/OpenGL runtime checks.
