# ESSENTIAL — Slay the Spire 2 modding context

Compiled from a completed mod project (`Rule the Spire`, a map-drawing QoL mod) on game **v0.111.0**.
Everything below is verified against this installation. Read this first, then use the reference
implementation.

---

## 1. Environment

| Item | Value |
|---|---|
| Game | `C:\Games\Steam\steamapps\common\Slay the Spire 2` |
| Game build | **v0.111.0** (`release_info.json`), engine **Godot/MegaDot 4.5.1**, pack format **3** |
| Managed assembly | `data_sts2_windows_x86_64\sts2.dll` (~9.7 MB) |
| API docs shipped with the game | `data_sts2_windows_x86_64\sts2.xml` (~5.6 MB) — full XML doc comments, grep it |
| Local mods | `<game>\mods\<ModId>\` |
| Workshop mods | `C:\Games\Steam\steamapps\workshop\content\2868840\` (BaseLib, RitsuLib, etc. are installed) |
| .NET SDK | **9.0.319** at `C:\Users\Reznolf\.dotnet` (user-local, **not on PATH by default**) |
| Godot installed | Steam Godot **4.7.2 standard** (not .NET) — not suitable for publishing this game's PCKs |
| MegaDot | **not installed**; only needed for `.pck` export / Godot editor work |
| Decompiler | `ilspycmd` 9.1.0.7988 at `C:\Users\Reznolf\.dotnet\tools` |

Per shell session:

```powershell
$env:PATH = "C:\Users\Reznolf\.dotnet;C:\Users\Reznolf\.dotnet\tools;$env:PATH"
```

### Temporary tools (may be cleared; re-download if missing)

| Tool | Path |
|---|---|
| GDRE Tools 2.7.4 | `C:\Users\Reznolf\AppData\Local\Temp\gdre\gdre_tools.exe` |
| Decompiled game source | `C:\Users\Reznolf\AppData\Local\Temp\sts2src\sts2.decompiled.cs` |
| Extracted scenes/assets | `C:\Users\Reznolf\AppData\Local\Temp\sts2pck`, `sts2recover`, `sts2cursor`, `sts2settings` |
| ffmpeg 9.0.2 (libvorbis) | `C:\Users\Reznolf\AppData\Local\Temp\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\` |
| FModBankParser Demo | `C:\Users\Reznolf\AppData\Local\Temp\FModBankParser.Demo.exe` (needs .NET 10 runtime) |

---

## 2. Game architecture / mod format

- **Engine**: MegaDot, Mega Crit's fork of **Godot 4.5.1**, C# gameplay on **.NET 9**. Not Unity;
  BepInEx advice does not apply. `0Harmony.dll` ships with the game.
- **Mod = a folder** in `mods/` containing:
  - `<Name>.json` manifest
  - `<Name>.dll` (optional)
  - `<Name>.pck` (optional; required by the loader if `has_pck` is true, named exactly `<modId>.pck`)
- **Manifest fields**: `id`, `name`, `author`, `description`, `version`, `min_game_version`,
  `has_pck`, `has_dll`, `dependencies`, `affects_gameplay`.
- **Entry point**: a static method marked `[ModInitializer("MethodName")]` in
  `MegaCrit.Sts2.Core.Modding`. If no initializer exists, the loader creates a Harmony instance and
  calls `Harmony.PatchAll` for the assembly.
- **PCK**: loaded with `ProjectSettings.LoadResourcePack(<modFolder>/<modId>.pck)`. Resources then
  resolve under `res://`. A broken PCK throws and the mod fails to load. The game reads pack format
  **3**; GDRE only writes formats 0–2, so **do not generate PCKs with GDRE for this game**.
  For code-only mods (like the reference), keep `has_pck: false`.
- **Mod preview image**: the modding screen looks for `res://<modId>/mod_image.png`, which only
  exists inside a PCK. For loose-file mods, the reference implementation loads
  `<mod.path>/mod_image.png` from disk in a `NModInfoContainer.Fill(Mod)` postfix. `mod.path` is the
  mod folder.
- **Saves**: modded and unmodded progress are separate profiles. `affects_gameplay` flags co-op mod
  matching; set `false` for UI/QoL mods.

---

## 3. Creating a new mod

```powershell
$env:PATH = "C:\Users\Reznolf\.dotnet;$env:PATH"
dotnet new install Alchyr.Sts2.Templates     # once
cd "C:\Users\Reznolf\Documents\Local Projects\Mods\Slay The Spire 2\Slay The Tism II"
dotnet new alchyrsts2mod -n SlayTheTismII -o . -M Reznolf
```

Template short names: `alchyrsts2mod` (empty), `alchyrsts2contentmod`, `alchyrsts2charmod`.
The template already references `0Harmony` and `sts2` by `HintPath`, with `Private=false`.

Key csproj facts:

- `Sdk="Godot.NET.Sdk/4.5.1"`, `TargetFramework=net9.0`.
- `<Sts2Path>` / `<Sts2DataDir>` auto-detected by `Sts2PathDiscovery.props` from the Steam registry;
  set `<Sts2Path>` manually in `Directory.Build.props` if needed.
- `<GodotPath>` is only required for `dotnet publish` (PCK export). `dotnet build` works without
  Godot for code-only mods.
- On build the template copies `<Name>.dll`, `.pdb`, `.json` to `$(ModsPath)<ProjectName>\`.
  Add `<Copy>` lines for any loose assets (images/audio) you want deployed.

Manifest example:

```json
{
  "id": "SlayTheTismII",
  "name": "Slay The Tism II",
  "author": "Reznolf",
  "description": "...",
  "version": "v0.1.0",
  "min_game_version": "0.107.0",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [],
  "affects_gameplay": false
}
```

---

## 4. Decompiling and exploring

```powershell
$env:PATH = "C:\Users\Reznolf\.dotnet;$env:PATH"
dotnet tool install -g ilspycmd --version 9.1.0.7988   # once
ilspycmd "C:\Games\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll" -o "$env:TEMP\sts2src"
```

`ilspycmd` produces one large `sts2.decompiled.cs` (~19 MB). Grep it for class names, `_Ready`,
scene paths, etc. Use `sts2.xml` for doc comments (types/members with docs only).

GDRE commands (PCK is ~2 GB; always use `--include`):

```powershell
$pck = "C:\Games\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
& "$env:TEMP\gdre\gdre_tools.exe" --headless --list-files=$pck | Select-String 'main_menu|logo'
& "$env:TEMP\gdre\gdre_tools.exe" --headless --extract=$pck --output="$env:TEMP\out" --include="res://scenes/..."
& "$env:TEMP\gdre\gdre_tools.exe" --headless --recover=$pck --output="$env:TEMP\out" --include="res://.godot/imported/name*.ctex"
```

- Source PNGs are **not** in the PCK; imported textures are `.ctex` (or `.image` for cursors).
  `--extract` copies the compressed resource; `--recover` decompresses to a real PNG.
- The reference mod's reference assets are in `Rule the Spire\reference\` (icons, cursors, audio).

---

## 5. Harmony / entry-point patterns

```csharp
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SlayTheTismII";
    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        new Harmony(ModId).PatchAll(assembly);
    }
}
```

- Harmony patches private methods fine; prefer public/`_Ready` targets when possible.
- Godot C# nodes created in code callbacks work (the source generator emits bindings); add the node
  under `NGame.Instance` (persists) or the target screen.

---

## 6. UI modding patterns and pitfalls (learned the hard way)

### Looking up nodes

- Use `GetNodeOrNull<T>("Path")` and guard everything; a mod failure must not break the game's UI.
- `%UniqueName` paths work only for nodes registered with `unique_name_in_owner`.

### Duplicating scripted nodes — NEVER clear their script

`node.Duplicate()` of a node whose script is a **C# class** returns that script's managed instance.
Calling `SetScript(nil)` **disposes that instance**, and any later access throws
`ObjectDisposedException` ("Cannot access a disposed object"). Build new rows from **scriptless
scenes** instead.

Useful scriptless scenes:

- `res://scenes/screens/settings_tickbox.tscn` — a plain Control containing `TickboxVisuals/Ticked`
  and `TickboxVisuals/NotTicked` TextureRects and a `SelectionReticle` (hide it; normally driven by
  the settings script).
- `res://scenes/screens/settings_slider.tscn` — a plain Control containing `Slider` (an `NSlider`,
  which is a `Range`) and a `SliderValue` label. `NSlider.SetValueWithoutAnimation` touches its
  handle, so call it **after** the row is added to the tree.
- `res://scenes/screens/settings_tab.tscn`, `res://scenes/ui/volume_slider.tscn`, etc.

### Settings screen injection

- Patch `[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]` (namespace
  `MegaCrit.Sts2.Core.Nodes.Screens.Settings`).
- Panel content: `ScrollContainer/Mask/Clipper/GeneralSettings/VBoxContainer` (the **General** tab;
  there is no "Gameplay" tab in this build).
- Rows are `MarginContainer` with `custom_minimum_size = (0, 64)`, theme margins 12/0/12/0, a
  `RichTextLabel` label, and a control with `size_flags_horizontal = 8` (shrink-end). `Container`
  respects shrink flags, so right alignment works.
- After inserting rows, try `panel.CallDeferred("RefreshSize")` if `panel.HasMethod("RefreshSize")`.
- Labels: clone the template `RichTextLabel` (keeps its `MegaRichTextLabel` script) and set `.Text`
  to a literal; no localization key needed.

### Buttons

- Subclass `NButton`; override `_Ready` to call `ConnectSignals()` (base `_Ready` throws unless the
  exact type is `NButton`). Use `Released` event, `OnFocus/OnUnfocus` for hover tweens.
- The map drawing toolbar buttons live at `%DrawingTools/HBoxContainer` and are 60×60.
- `NMapDrawButton.SetIsDrawing(bool)` swaps normal/glow textures; colors: quill `#57C4FF`, eraser
  `#FF5757`, clear `#FFE57D`.

### Layout / z-order

- `CanvasItem.ZIndex` sorts **globally within a canvas layer**, not per parent. Raising map nodes
  puts them above the legend/Back button; lowering them puts the dimming `Backstop` over the map.
  Prefer `Node.MoveChild` to reorder siblings within one container.
- Reparenting a Control with centered anchors (0.5) recomputes offsets against the **new parent's
  size**. If the new parent is a size-(0,0) `Control`, the child shifts by half the old parent size.
  Fix: set `child.Position = original.Position` after `AddChild`, or give the container the same size.

### Sliders

- `NSlider : Range`; no `Editable` property. To disable, set `MouseFilter = Ignore` (they are
  mouse-driven; `focus_mode = 0`).

### Cursors

- `NGame.Instance.CursorManager.OverrideCursor(Image tilted, Image normal, Vector2 hotspot)` and
  `StopOverridingCursor()`.
- Tilted is shown while **any** mouse button is down (manager tracks `_isDown`).
- `NMapDrawings.UpdateLocalCursor()` calls `StopOverridingCursor()` whenever the drawing mode is
  `None`, so re-apply after any drawing API call.
- Hotspot reference: quill `(2, 56)` on 64×64; cursors are fully colored art (not white line art).

### Screen/menu awareness

- `ActiveScreenContext.Instance.Updated` fires on screen changes; `IsCurrent(IScreenContext)` tells
  you whether a screen is current.
- `NRun.Instance.GlobalUi.Overlays` is an `NOverlayStack` with `Changed` and `ScreenCount`; use it to
  detect fullscreen overlays/menus.

---

## 7. Main menu / title logo (the new mod)

Start by locating the exact nodes — do not assume.

```powershell
# List candidate scenes
& "$env:TEMP\gdre\gdre_tools.exe" --headless --list-files=$pck |
  Select-String 'main_menu|title|logo|splash' | ForEach-Object { $_.Line.Trim() }

# Extract and inspect the scene
& "$env:TEMP\gdre\gdre_tools.exe" --headless --extract=$pck --output="$env:TEMP\menu" `
  --include="res://scenes/screens/main_menu.tscn"
```

Then grep the decompiled source for the class behind it (`NMainMenu`, `NTitleScreen`, `NLogo`,
whatever the scene's `script =` references). Likely shape:

- A `TextureRect` (or `Sprite2D`/`SpineSprite`) holding the logo.
- Fixing/replacing a `TextureRect.Texture` is low risk:
  - postfix the owning node's `_Ready` (or the menu's `_Ready`) **after** the game assigns the
    original, then set `Texture` to your own.
  - load the PNG from the mod folder:
    ```csharp
    var image = new Image();
    if (image.LoadPngFromBuffer(File.ReadAllBytes(path)) == Error.Ok)
        logo.Texture = ImageTexture.CreateFromImage(image);
    ```
    `path = Path.Combine(Assembly.GetExecutingAssembly().Location's dir, "logo.png")`.
- Preserve layout: keep the existing `ExpandMode`, `StretchMode`, anchors, offsets, pivot and
  `CustomMinimumSize`. If the logo is inside a container, replacing the texture is enough; if it is
  sized from the texture, adjust `CustomMinimumSize` to your image's size.
- Match the original aspect ratio; the main menu is displayed at multiple resolutions/aspect ratios.
- If the logo is animated (scale/tween/Spine), consider patching after the animation setup or
  replacing the texture before tweens read it; check `_Ready` order.
- If the menu already exists before your mod initializes (unlikely; mods load at startup before the
  main menu), use a lazy `GetTree().NodeAdded` hook or patch the menu's `_Ready` and also apply once
  if the node already exists.
- Copy the logo PNG into the deployed mod folder from the csproj (same pattern as `mod_image.png`).
- `has_pck: false` is fine; do **not** ship a GDRE-generated PCK (format 3 mismatch).

To discover the runtime tree, temporarily log paths:

```csharp
foreach (Node n in GetTree().Root.FindChildren("*", "", true, false))
    Logger.Info($"{n.GetPath()} ({n.GetType()})");
```

(Do this behind a flag; it is verbose.)

---

## 8. Assets

- Game textures are `.ctex` inside the PCK; use GDRE `--recover` to get PNGs.
- Toolbar icons: 80×80, 32-bit ARGB, **pure white line art**; tinted at runtime with `self_modulate`
  (inactive `#FFFFFF80`, active tool-specific). The additive material `shared_additive_mat` makes
  them glow.
- Cursors: 64×64 colored art; `_tilted` variant for the pressed state.
- Audio SFX in the game are FMOD events (banks under `res://banks/desktop/`), not loose files.
  A mod cannot add events to those banks; use Godot audio for custom sounds:
  `AudioStreamOggVorbis.LoadFromFile(path)` + `AudioStreamPlayer` (or `AudioStreamWav`).
  Mirror the game's volume from `SaveManager.Instance.SettingsSave.VolumeMaster/VolumeSfx`.
- FMOD sample extraction: `FModBankParser.Demo.exe <bank> -e -o <dir>` exports named `.ogg` samples.

---

## 9. Build / deploy / test workflow

```powershell
$env:PATH = "C:\Users\Reznolf\.dotnet;$env:PATH"
cd "<project folder>"
dotnet build -v m
```

- The build copies the DLL/PDB/JSON (and any extra `<Copy>` items) into `<game>\mods\<ProjectName>\`.
- **If the game is running it locks the DLL** and the copy fails (`MSB3026/MSB3027`). Close the game,
  or run a background retry loop:
  ```powershell
  for ($i=0; $i -lt 180; $i++) {
      try { Copy-Item $src $dst -Force -ErrorAction Stop; break } catch { Start-Sleep 5 }
  }
  ```
- Enable the mod in **Settings → Mod Settings** (a new mod ID may start disabled).
- Log file: `%APPDATA%\SlayTheSpire2\logs\godot.log`. The mod's logger prefix is `[<ModId>]`.
- Verify deployment by hash:
  `(Get-FileHash $src).Hash -eq (Get-FileHash $dst).Hash`.

### Packaging for another player

- Stage a folder `<ModId>/` containing the DLL, JSON, PDB, assets, and an `INSTALL.txt`.
- Exclude the personal config file.
- `Compress-Archive -Path $stage -DestinationPath "<name>.zip"` so the zip root contains the
  `<ModId>` folder; the recipient extracts into `mods/`.

---

## 10. Reference implementation

`C:\Users\Reznolf\Documents\Local Projects\Mods\Slay The Spire 2\Rule the Spire`

Key files (read these for working patterns):

| File | Demonstrates |
|---|---|
| `RuleTheSpire.csproj` | SDK/references, asset copy targets, `Sts2Path`/`GodotPath` |
| `RuleTheSpireCode/MainFile.cs` | `[ModInitializer]`, Harmony, config load |
| `RuleTheSpireCode/RuleTheSpireConfig.cs` | JSON config next to the DLL, clamping, change events |
| `RuleTheSpireCode/Patches.cs` | Harmony prefixes/postfixes, `_Ready`, `_Input`, `SetMap` |
| `RuleTheSpireCode/PathTool.cs` | toolbar button, settings row integration, cursor manager, disk asset loading |
| `RuleTheSpireCode/PathToolButton.cs` | custom `NButton` subclass |
| `RuleTheSpireCode/SettingsIntegration.cs` | injecting tickbox + slider rows into Settings → General |
| `RuleTheSpireCode/PathOverlay.cs` | reflection into private game state, overlay rendering, z-order |
| `RuleTheSpireCode/PathSound.cs` | Godot audio playback with game volume |
| `RuleTheSpireCode/ModImagePatch.cs` | loading `mod_image.png` from disk for the modding screen |
| `reference/…` | extracted icons, cursors, audio samples |

The project is also a working example of: per-act map rebuild handling, stroke matching, custom
cursors, settings persistence, and packaging.

---

## 11. Do-not list

- Do not use `ZIndex` on game UI subtrees (global canvas sorting).
- Do not `SetScript(nil)` on duplicated C#-scripted nodes.
- Do not assume `Control.Position` survives reparenting under centered anchors.
- Do not generate PCKs with GDRE for this game (format 3).
- Do not call `NSlider.SetValueWithoutAnimation` before the node is in the tree.
- Do not write to `res://`.
- Do not let mod exceptions escape into game `_Ready`/`_Input`; wrap in try/catch and log.
- Do not rebuild while the game is running (DLL lock).
- Do not expect `mod_image.png` to show without either a PCK or the disk-loading patch.

---

## 12. Quick reference commands

```powershell
# Environment
$env:PATH = "C:\Users\Reznolf\.dotnet;C:\Users\Reznolf\.dotnet\tools;$env:PATH"

# Game
$game = "C:\Games\Steam\steamapps\common\Slay the Spire 2"
$data = "$game\data_sts2_windows_x86_64"
$mods = "$game\mods"

# Decompile
ilspycmd "$data\sts2.dll" -o "$env:TEMP\sts2src"

# Docs search
Select-String -Path "$data\sts2.xml" -Pattern 'NMainMenu|Logo|Title'

# GDRE
$pck = "$game\SlayTheSpire2.pck"
$gdre = "$env:TEMP\gdre\gdre_tools.exe"
& $gdre --headless --list-files=$pck | Select-String 'logo|main_menu'
& $gdre --headless --extract=$pck --output="$env:TEMP\menu" --include="res://scenes/..."
& $gdre --headless --recover=$pck --output="$env:TEMP\menu" --include="res://.godot/imported/*.ctex"

# Build + deploy
cd "<project>"
dotnet build -v m

# Logs
Get-Content "$env:APPDATA\SlayTheSpire2\logs\godot.log" -Tail 100
```
