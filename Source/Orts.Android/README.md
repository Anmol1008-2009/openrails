# Open Rails Android host

This project is the initial Android application and MonoGame OpenGL host. It references the portable simulator libraries and produces an Android package. The app can use Android's Storage Access Framework to copy a user-selected MSTS installation into app-private storage, preserving its routes, consists, trains, and other files. It can list `.act` files and ask the simulator core to initialize a selected route and activity, including starting train initialization. This initialization path has not yet been verified against a valid MSTS installation on Android.

## Build

Install the .NET Android workload, Android SDK platform/build tools, and JDK 21. Then set `JAVA_HOME`, `ANDROID_HOME`, and `ANDROID_SDK_ROOT` for the local installations and run:

```sh
dotnet publish Source/Orts.Android/Orts.Android.csproj -c Release -r android-arm64 -p:PublishTrimmed=false -p:RunAOTCompilation=false
```

The host currently targets 64-bit ARM devices and x64 emulators. Folder access is retained by Android. Import copies the entire selected tree and therefore requires enough free device storage for a second copy; failed or cancelled imports do not become the active installation. After simulation startup, the host displays a 2D route-track overview and train marker. Touch sliders control throttle and train brake; direction changes require throttle near zero and the train nearly stopped. Live speed, power, brake, and direction are shown while the simulator runs.

This is not yet a complete playable port: activity startup has not been validated against a real MSTS installation, and the Windows-only desktop runner currently owns the full 3D world/cab renderer. Sound, renderer portability, and broader controls remain future work.
