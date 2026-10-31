# Open Rails Android port handoff

This note is for whoever continues the Android port. The working tree contains the Android prototype and portable-core changes; they are currently **uncommitted and not pushed**. Review `git status` before changing or cleaning anything. The configured `origin` is `https://github.com/openrails/openrails` on branch `master`; do not push experimental port work to the upstream repository. To collaborate through GitHub, create a personal fork and publish a branch there.

## Current state

The Android host is in `Source/Orts.Android` and uses MonoGame's Android package with the portable Open Rails simulation libraries. It currently:

- Imports a user-selected MSTS installation through Android's Storage Access Framework into app storage.
- Discovers route folders and `.act` activities, then asks the simulator core to initialize a selected activity.
- Runs simulation updates on the MonoGame update thread and queues touch-control changes onto that thread.
- Displays a 2D route-track overview and a player-train marker.
- Provides power and train-brake sliders, a direction toggle guarded by low throttle and near-zero speed, and live speed/control status.

These are prototype capabilities, not proof of end-to-end gameplay. There is no real MSTS installation available in this checkout, so simulator startup against actual route/train content, import of a full installation, and driving have not been verified. The full 3D world/cab renderer, sound, and broad control coverage are not ported. The existing viewer is tied to the Windows desktop runner, which uses Windows Forms and the WindowsDX MonoGame backend; porting it requires isolating platform/UI dependencies and adapting/testing rendering on Android.

The latest change also raises controller state from the game loop to the Android UI so slider values and direction can follow simulator state. Its x64 Release build succeeded, and the portable tests passed (3/3); an emulator smoke test of that latest feedback change was not completed. The prior slider UI build was installed/launched in an x64 emulator, where both sliders and the direction button appeared.

## Android build and test

Prerequisites: .NET 10 SDK with Android workload, JDK 21, Android SDK/platform tools. Set `JAVA_HOME`, `ANDROID_HOME`, and `ANDROID_SDK_ROOT`, then build:

```sh
dotnet publish Source/Orts.Android/Orts.Android.csproj \
  -c Release -r android-arm64 \
  -p:PublishTrimmed=false -p:RunAOTCompilation=false
```

For an x64 emulator, replace `android-arm64` with `android-x64`. The project sets `AndroidEnableMarshalMethods=false`; this fixed the Android JNI callback crash and was verified with an x64 Release launch. A clean Release build may be needed after changing Android workload/build properties to avoid stale generated Java intermediates.

Portable path/content regression tests:

```sh
DOTNET_ROLL_FORWARD=Major dotnet test \
  Source/Orts.Content.Tests/Orts.Content.Tests.csproj \
  -c Release -f net6.0 --no-build
```

The latest confirmed test run passed all 3 tests. `git diff --check` was clean.

## Main implementation areas

- `Source/Orts.Android/MainActivity.cs`: Android UI, folder picker/import orchestration, activity selection/start, controls, lifecycle.
- `Source/Orts.Android/AndroidSimulatorGame.cs`: MonoGame loop, simulation stepping, queued touch commands, 2D route map, status and controller-state reporting.
- `Source/Orts.Android/AndroidContentRootImporter.cs` and `AndroidContentRootScanner.cs`: SAF tree scanning and staged content copying.
- `Source/Orts.Android/AndroidRouteCatalog.cs`: route/activity discovery.
- `Source/Orts.Android/Orts.Android.csproj`: Android target, MonoGame dependency, JNI registration workaround.
- `Source/Orts.Content/MSTSPath.cs`: portable/case-insensitive MSTS path resolution.
- `Source/Orts.Simulation/Simulation/Simulator.cs` and the portable project files: adaptations enabling the simulator core to compile for Android.
- `Source/Orts.Content.Tests/`: portable path regression tests.
- `Source/Orts.Android/README.md`: user-facing capabilities and build instructions.

The overall working tree also contains portable compatibility changes under `Source/Orts.Common`, `Source/Orts.Settings`, and the MSTS format/parser projects. Inspect the diff before committing; the port work has accumulated over multiple steps.

## Recommended next steps

1. Rebuild and emulator-launch the latest x64 package that includes live controller feedback; verify slider movement and direction display remain responsive.
2. Obtain a legally usable, small MSTS test installation/route (or a user-provided one) and verify import, route/activity discovery, simulator start, map, controls, and shutdown. Surface and fix any platform-specific failures.
3. Make import more practical for large installations. Current behavior copies the full selected tree and needs space for a second copy; preserve the SAF permission and make cancellation/error cleanup robust.
4. Map all Windows-only viewer dependencies, then split portable simulation/rendering logic from Windows-only forms, input, audio, and platform setup. Do not promise full PC-addon compatibility before content tests.
5. Add renderer-focused tests and iterate from basic 3D terrain/track to scenery and rolling stock/cab, profiling GPU/memory on a real ARM64 Android device.
6. Publish work on a personal fork/branch; do not commit generated APKs or emulator files.
