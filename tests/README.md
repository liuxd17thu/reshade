# GUI / headless runtime regression

`RESHADE_GUI` controls the overlay. Preset loading, shortcut handling, automatic
saving and AuroraShade preset formats use the same runtime code in both builds.

## Building

From a Visual Studio Developer PowerShell at the repository root:

```powershell
msbuild ReShade.sln /p:Configuration=Release /p:Platform=64-bit /p:ReShadeGUI=0
Copy-Item bin/x64/Release/ReShade64.dll bin/x64/Release/ReShade-headless.dll
msbuild ReShade.sln /p:Configuration=Release /p:Platform=64-bit /p:ReShadeGUI=1
Copy-Item bin/x64/Release/ReShade64.dll bin/x64/Release/ReShade-gui.dll
```

The default remains GUI enabled, except for the existing `Release App`
configuration, which defaults to disabled. CMake accepts `-DRESHADE_GUI=OFF`
or `-DRESHADE_GUI=ON` (the default). Keep separate CMake build directories for
the two configurations.

`OVERLAY/AutoSavePreset` remains the configuration key for automatic preset
saving in both builds. `SCREENSHOT/PostSaveCommandEnabled` enables or disables
the configured screenshot command; it defaults to `1` for compatibility with
presets/configurations made before the explicit switch existed. An empty
command still does nothing.

## Running

The regression executable creates a hidden window and a D3D11 WARP device; it
does not require a game or GPU. It loads the supplied DLL through the public
runtime API. Synthetic key messages target only its own window.

```powershell
New-Item -ItemType Directory -Force build/runtime-tests
cl /nologo /std:c++17 /EHsc /utf-8 /W4 /Iinclude tests/runtime_gui_parity.cpp /Febuild/runtime-tests/runtime_gui_parity.exe /Fobuild/runtime-tests/runtime_gui_parity.obj /link d3d11.lib dxgi.lib user32.lib
build/runtime-tests/runtime_gui_parity.exe bin/x64/Release/ReShade-headless.dll build/runtime-tests/headless
build/runtime-tests/runtime_gui_parity.exe bin/x64/Release/ReShade-gui.dll build/runtime-tests/gui
```

Use a new output directory for each run. The test preserves the generated
configuration, shader, preset and exported preset for inspection, and exits
nonzero on failure. The executable must match the DLL architecture.

Coverage includes initial loading delay, preset and `ui_bind` values, effect /
uniform / technique shortcuts, automatic saving enabled and disabled, explicit
API saves, clone restoration, flair switching, technique group preservation,
single-effect reloads, orphan binding cleanup without opening the overlay,
shared clone macros, and screenshot command configuration persistence.

This exercises the D3D11 runtime and public API; it does not replace game tests
for other graphics APIs or interactive checks of overlay rendering.
