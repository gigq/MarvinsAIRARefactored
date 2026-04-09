# LMU Integration Handoff

This note is the handoff point for continuing the multi-sim work on a Windows box.

## Current branch

- Branch: `t3code/multi-sim-support-framework`
- Commit: `2ab2df8`
- Fork remote: `origin = git@github.com:gigq/MarvinsAIRARefactored.git`
- Upstream remote: `upstream = https://github.com/mherbold/MarvinsAIRARefactored.git`
- PR URL:
  - `https://github.com/gigq/MarvinsAIRARefactored/pull/new/t3code/multi-sim-support-framework`

## Goal of this pass

The goal was not to fully integrate LMU yet.

The goal was to create a structure that:

1. keeps the current iRacing implementation working with minimal disturbance,
2. introduces a clean place to register additional sims,
3. avoids turning the repo into a hard fork that will be painful to merge from upstream later,
4. makes settings, file storage, and menu/page availability sim-aware.

## What was added

### 1. Sim registry and capability model

New files:

- `SimSupport/SimId.cs`
- `SimSupport/SimFeature.cs`
- `SimSupport/SimSupportLevel.cs`
- `SimSupport/SimDefinition.cs`
- `SimSupport/SimRegistry.cs`

This is the new central place for:

- the list of supported/scaffolded sims,
- display names,
- per-sim folder names,
- capability flags,
- sim window title metadata,
- support status text shown in the UI.

Current sims in the registry:

- `IRacing`
- `LeMansUltimate`

Current support levels:

- `iRacing` = fully wired existing backend
- `LeMansUltimate` = scaffolded only, backend not implemented yet

### 2. Additive simulator seam

New file:

- `Components/Simulator.SimSupport.cs`

This keeps the existing `Simulator` class mostly intact and adds:

- `SelectedSimId`
- `CurrentSimDefinition`
- `SupportsSelectedSimulatorBackend`
- `ApplySelectedSimulator()`

Right now:

- if selected sim supports the telemetry backend, the existing IRSDK path starts,
- if it does not, IRSDK is stopped and the app stays in scaffold mode.

This is intentional. It prevents fake/partial LMU behavior while still letting the shell become sim-aware.

### 3. Sim-aware document/content directories

Updated:

- `App.xaml.cs`

Added:

- `App.GetSimulatorDocumentsFolder(SimId)`
- `App.GetSimulatorContentDirectory(SimId, folderName)`

Behavior:

- iRacing still uses the legacy MAIRA folder layout for upstream compatibility.
- Non-iRacing sims go under:
  - `Documents/MarvinsALMUA Refactored/Sims/<SimFolder>/...`

This was done to avoid cross-sim collisions while preserving the current iRacing expectations.

### 4. Settings and context are now sim-aware

Updated:

- `DataContext/Settings.cs`
- `DataContext/Context.cs`

Added setting:

- `Settings.AppSelectedSimulator`

When sim selection changes, the app currently:

- updates the default Trading Paints folder if it was still using the previous default,
- refreshes calibration directory,
- reloads recordings for the selected sim,
- reapplies simulator backend selection,
- refreshes the window/menu state.

`Context` now includes `SimulatorId`, so context-driven settings no longer collide across sims if car/track names overlap.

That matters for future LMU support.

### 5. Menu/page capability gating

Updated:

- `Controls/MairaAppMenuPopup.xaml.cs`
- `Windows/MainWindow.xaml.cs`
- `Pages/AppSettingsPage.xaml.cs`

Behavior now:

- unsupported pages are hidden from the app menu for the selected sim,
- unsupported pages are excluded from the "Default Page" dropdown,
- the selected page is coerced to a valid supported page if needed.

Currently gated by capabilities:

- `TradingPaints`
- `Simulator`

For LMU right now those pages are hidden because the LMU entry has `SimFeature.None`.

### 6. App Settings simulator selector

Updated:

- `Pages/AppSettingsPage.xaml`
- `Pages/AppSettingsPage.xaml.cs`

There is now a `Simulation` group at the top of App Settings with:

- simulator dropdown,
- support/scaffold note text.

This is where additional sims should continue to surface.

### 7. Sim-specific recordings/calibration paths

Updated:

- `Components/RecordingManager.cs`
- `Components/SteeringEffects.cs`
- `Components/Simulator.cs`

Changes:

- recordings are loaded from the selected sim’s content folder,
- steering calibration uses the selected sim’s calibration folder,
- debug diagnostic YAML export now goes into the selected sim’s diagnostics folder.

This isolates future LMU data from iRacing data.

## What was intentionally not done yet

These items are still outstanding:

- No LMU telemetry adapter/backend exists yet.
- `Simulator` is still overwhelmingly iRacing-specific.
- No sim abstraction interface has been introduced for all telemetry fields.
- Trading Paints remains iRacing-only.
- The simulator diagnostics page still assumes IRSDK-shaped data.
- No installer changes were made.
- No localization strings were added for the new Simulation selector; the label is plain text for now.
- No compile/test run was completed in this environment.

## Important implementation note

This pass deliberately used an additive structure instead of refactoring the whole telemetry stack at once.

That means the codebase is still mostly iRacing-shaped, but there is now a controlled boundary for expanding it.

Do not try to retrofit LMU by sprinkling `if (selectedSim == LMU)` everywhere. That will make future merges ugly fast.

## Recommended next steps on Windows

### Phase 1: verify and stabilize this branch

1. Pull branch `t3code/multi-sim-support-framework`.
2. Open the solution in Visual Studio on Windows.
3. Build and fix any compile/runtime issues from this structural pass.
4. Launch with `AppSelectedSimulator = IRacing` and confirm existing iRacing behavior still works.
5. Switch to `Le Mans Ultimate` and verify:
   - app launches,
   - menu hides unsupported pages,
   - app status shows scaffold mode,
   - sim-specific folders are created as expected.

### Phase 2: decide the LMU backend boundary

Before implementing LMU, answer this explicitly:

- Is LMU exposing telemetry through shared memory, plugin API, file output, or some other mechanism?
- What data fields needed by MALMUA are actually available?
- Which existing MALMUA features are realistic for LMU on day one?

Do not assume LMU can provide every iRacing datum currently used by:

- `RacingWheel`
- `Pedals`
- `SteeringEffects`
- `TimingMarkers`
- `GapMonitorWindow`
- `TradingPaints`
- `SpeechToText`
- `AdminBoxx`

### Phase 3: create an LMU adapter instead of bloating `Simulator`

Recommended direction:

1. Keep the current `Simulator` as the app-facing telemetry aggregator for now.
2. Extract the existing IRSDK-specific behavior behind an internal backend class, for example:
   - `ISimTelemetryBackend`
   - `IRacingTelemetryBackend`
   - `LmuTelemetryBackend`
3. Move backend-specific connect/disconnect/session parsing out of `Simulator`.
4. Let `Simulator` own:
   - selected backend,
   - normalized app-facing properties,
   - app notifications on connect/disconnect,
   - common status/update flow.

This keeps existing consumers stable while allowing a second backend to be added incrementally.

### Phase 4: normalize only the fields LMU actually needs first

Do not try to implement every `Simulator` field up front.

Start by identifying the minimum feature set you want LMU to support on day one. Likely candidates:

- speed
- RPM
- gear
- throttle
- brake
- clutch
- steering wheel angle
- yaw rate
- velocity X/Y
- lap distance percent
- on-track status
- car/track names

Get one end-to-end loop working first:

- connect to LMU,
- populate those normalized fields,
- run one feature such as Wind or basic wheel effects.

### Phase 5: capability flags should become real

As LMU support grows, update `SimRegistry` feature flags.

Examples:

- when LMU telemetry works, enable `TelemetryBackend`
- when the simulator diagnostics page can display meaningful LMU data, enable `SimulatorDiagnostics`
- if some LMU equivalent to Trading Paints ever exists, add that later instead of pretending it is universal

## Files most relevant for continuing

Start here:

- `SimSupport/SimRegistry.cs`
- `Components/Simulator.SimSupport.cs`
- `Components/Simulator.cs`
- `DataContext/Settings.cs`
- `Controls/MairaAppMenuPopup.xaml.cs`
- `Pages/AppSettingsPage.xaml.cs`

Likely next files to touch for LMU backend work:

- `Components/Simulator.cs`
- possibly a new backend folder such as `Components/SimBackends/`
- any feature component that directly assumes IRSDK-only enums or structures

## Known caveats

- This Linux environment did not have a .NET SDK installed, so `dotnet build` could not run here.
- `git diff --check` was clean before handoff.
- The new settings/UI flow was reasoned through, but still needs a proper Windows compile and runtime verification.

## Suggested first prompt for the next Codex session

Use something close to this:

> Continue the LMU integration from `Notes/lmu_handoff.md` on branch `t3code/multi-sim-support-framework`. First build the solution on Windows, fix any issues from the multi-sim framework pass, then inspect how LMU telemetry can be read and propose the smallest clean backend abstraction that lets us add LMU without forking the whole app.

## Summary

The repo is now prepared for multi-sim expansion, but LMU is only scaffolded.

The next real milestone is:

- compile on Windows,
- implement a clean LMU telemetry backend,
- normalize only the minimum fields needed for the first useful LMU-supported features.
