# TarkovNvColor

Windows utility that applies your Escape From Tarkov NVIDIA desktop color preset when the game runs, then restores defaults when you quit.

| Setting | Default | Game (shipped) |
|--------|---------|----------------|
| Contrast | 50% | 60% |
| Gamma | 1.00 | 1.50 |
| Digital Vibrance | 50% | 70% |

Colors apply to the **Windows primary (main) monitor only**.

## Requirements

- Windows
- NVIDIA GPU + driver (`nvapi64.dll` — already installed with the driver)
- No .NET install needed if you use the **Release folder** / GitHub Release zip (self-contained)

## Release folder (easiest)

Use the ready-made `release\` folder (or the GitHub **Releases** zip). Everything is already in one place:

| File | What to do |
|------|------------|
| `1-Start-Session.bat` | **Normal use** — opens BSG launcher (or Steam if BSG missing), applies colors while Tarkov runs |
| `2-Start-GameOnly.bat` | Does **not** open any launcher — you start Tarkov yourself; colors only while the game runs |
| `3-Start-Steam.bat` | Forces Steam Tarkov (`steam://rungameid/3932890`) |
| `4-Apply-Game-Preset.bat` | Instantly apply the game colors (test without launching Tarkov) |
| `5-Reset-Colors.bat` | Restore neutral colors (C50 / G1.00 / DV50) |
| `6-Fix-StartMenu-Shortcut.bat` | After a Tarkov/BSG update: recreate one Start Menu shortcut → this folder’s exe `--session` (may ask Admin) |
| `config.json` | Edit in Notepad to change contrast / gamma / DV / Steam / game-only |
| `TarkovNvColor.exe` | The app (used by the `.bat` files) |

### Typical use

1. Open the `release` folder (or extract the GitHub zip anywhere).
2. Double-click **`1-Start-Session.bat`**.
3. Play Tarkov as usual. Colors flip on game start and restore when you quit.

**Already open Tarkov yourself?** Use **`2-Start-GameOnly.bat`** instead.

**Want different colors?** Edit `config.json` → save → run the bat again. No rebuild.

**Start Menu broke after an update (two launchers / plain BSG again)?** Run **`6-Fix-StartMenu-Shortcut.bat`**.

Keep all files in the **same folder**. Don’t separate the exe from `config.json` or the `.bat` files.

## What `--session` does

Default session (launcher-aware):

1. Starts or attaches to the **Battlestate launcher**, or falls back to **Steam** if BSG is not found (`launchMode: auto`).
2. When `EscapeFromTarkov.exe` starts → **game** preset.
3. When the game closes → **default** preset.
4. Exits when the play session ends (BSG: launcher + game both closed; Steam / game-only: when the game closes).

Not a 24/7 background service.

## Features

### Editable presets (`config.json`)

Change numbers in Notepad next to the exe — **no rebuild**.

### Battlestate + Steam

| Mode | Behavior |
|------|----------|
| `auto` (default) | Prefer BSG launcher; if missing, launch Steam Tarkov (`steam://rungameid/3932890`) |
| `bsg` | Battlestate launcher only |
| `steam` | Steam only |

CLI overrides:

```powershell
.\TarkovNvColor.exe --session --bsg
.\TarkovNvColor.exe --session --steam
.\TarkovNvColor.exe --session --launcher "C:\Battlestate Games\BsgLauncher\BsgLauncher.exe"
```

### Game-only (ignore launcher)

Does **not** start BSG or Steam. Does **not** change colors until the game process appears. Applies the game preset only while Tarkov is running, restores defaults when it exits, then quits.

```powershell
.\TarkovNvColor.exe --session --game-only
```

Or in `config.json`: `"gameOnly": true`  
Or double-click `2-Start-GameOnly.bat` in the Release folder.

### Primary monitor only

Contrast, gamma, and digital vibrance target the **Windows main display** (`Make this my main display`). Other monitors are left alone.

### Manual apply / reset

```powershell
.\TarkovNvColor.exe --apply game
.\TarkovNvColor.exe --apply default
.\TarkovNvColor.exe --reset
```

Or use `4-Apply-Game-Preset.bat` / `5-Reset-Colors.bat`.

### Start Menu after a Tarkov update

BSG updates often recreate a plain launcher shortcut. Run `6-Fix-StartMenu-Shortcut.bat` (or `Fix-TarkovShortcut.bat`).

It finds Battlestate / Tarkov shortcuts, removes duplicates, and recreates one user Start Menu shortcut pointing at this folder’s `TarkovNvColor.exe --session` (BSG icon).

## CLI reference

| Flag | What it does |
|------|----------------|
| `--session` | Launcher-aware session (see above) |
| `--session --steam` | Force Steam Tarkov |
| `--session --bsg` | Force Battlestate launcher |
| `--session --game-only` | Ignore launcher; colors follow `EscapeFromTarkov.exe` only |
| `--session --launcher "path"` | Explicit `BsgLauncher.exe` path |
| `--apply game` / `--apply default` | One-shot presets from `config.json` |
| `--reset` | Neutral baseline (C50 / G1.00 / DV50) on the primary monitor |
| `--watch` | Forever game-only watch (no auto-exit); prefer `--session` |

## `config.json`

Keep this file next to `TarkovNvColor.exe`.

```json
{
  "gameProcess": "EscapeFromTarkov",
  "launcherProcess": "BsgLauncher",
  "launcherPath": null,
  "launchMode": "auto",
  "steamAppId": 3932890,
  "gameOnly": false,
  "pollMs": 3000,
  "brightnessPercent": 50,
  "default": {
    "contrast": 50,
    "gamma": 1.0,
    "digitalVibrance": 50
  },
  "game": {
    "contrast": 60,
    "gamma": 1.5,
    "digitalVibrance": 70
  }
}
```

| Field | Meaning |
|-------|---------|
| `game` / `default` | Presets while Tarkov is running / not running |
| `launchMode` | `auto`, `bsg`, or `steam` |
| `steamAppId` | Steam App ID (`3932890`) |
| `gameOnly` | `true` = same as `--game-only` |
| `launcherPath` | Optional full path to `BsgLauncher.exe` |
| `gameProcess` / `launcherProcess` | Process names without `.exe` |
| `pollMs` | Process poll interval (ms) |
| `brightnessPercent` | Brightness used when applying CG (usually `50`) |

## Build from source

```powershell
cd path\to\tarkov-auto-nvidia-settings
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true
```

Then copy into a clean folder (same layout as `release\`):

- `TarkovNvColor.exe`
- `config.json`
- the numbered `.bat` helpers (optional but recommended)

Framework-dependent (needs .NET 10 installed):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

### Start Menu (manual)

- Target: `...\TarkovNvColor.exe` (your Release folder)
- Arguments: `--session`
- Icon: `BsgLauncher.exe` is fine

Or use `6-Fix-StartMenu-Shortcut.bat` after launcher updates.

## How it works

- **Digital Vibrance** → NVIDIA NVAPI (`nvapi64.dll`), primary display
- **Contrast / Gamma** → NVIDIA desktop-color formula + `NvAPI_DISP_SetTargetGammaCorrection` on the GDI primary display (same math as Control Panel)
- Uses the driver already on your PC; nothing extra is downloaded

## Notes

- Best with borderless / windowed fullscreen Tarkov on the main monitor.
- Control Panel sliders may lag the real image briefly; the image path is NVAPI.
- Alt-tab focus-based restore is **not** supported (process-based only).
- Not affiliated with Battlestate Games or NVIDIA.
- Use at your own risk. If something looks wrong, run `5-Reset-Colors.bat` / `--reset` or restore defaults in NVIDIA Control Panel.

## License

MIT — see [LICENSE](LICENSE).
