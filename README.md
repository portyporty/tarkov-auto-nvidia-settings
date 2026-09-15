# TarkovNvColor

Applies your Escape From Tarkov NVIDIA desktop color preset when the game runs, then restores defaults when you quit.

| Setting | Default | Game (shipped) |
|--------|---------|----------------|
| Contrast | 50% | 60% |
| Gamma | 1.00 | 1.50 |
| Digital Vibrance | 50% | 70% |

Colors apply to the **Windows primary (main) monitor only**.

## Requirements

- Windows
- NVIDIA GPU + driver (`nvapi64.dll` — already installed with the driver)

## Quick start (Release zip)

1. Download `TarkovNvColor-windows-x64.zip` from [Releases](https://github.com/portyporty/tarkov-auto-nvidia-settings/releases).
2. Extract anywhere and open the folder that contains `TarkovNvColor.exe` + `config.json`.
3. Double-click **`1-Start-Session.bat`**.
4. Play Tarkov. Colors flip on game start and restore when you quit.

**Want Start Menu “Escape From Tarkov” to do this automatically?**  
Also run **`6-Fix-StartMenu-Shortcut.bat`** once (see [Start Menu shortcut](#start-menu-shortcut) below). After that, launching Tarkov from Start opens this app + colors with it.

Keep every file in the **same folder**. Don’t move the exe away from `config.json`.

| File | What it does |
|------|----------------|
| `1-Start-Session.bat` | Normal use — BSG launcher (or Steam if BSG missing) |
| `2-Start-GameOnly.bat` | You start Tarkov yourself; colors follow the game only |
| `3-Start-Steam.bat` | Force Steam Tarkov |
| `4-Apply-Game-Preset.bat` | Apply game colors once (test) |
| `5-Reset-Colors.bat` | Neutral colors (C50 / G1.00 / DV50) |
| `6-Fix-StartMenu-Shortcut.bat` | **Main feature:** make Start Menu Tarkov launch this app + color presets (re-run after Tarkov/BSG updates) |
| `config.json` | Edit presets in Notepad — no rebuild |
| `TarkovNvColor.exe` | The app |

### Start Menu shortcut

This is one of the main ways to use the tool day-to-day — not just a repair script.

Run **`6-Fix-StartMenu-Shortcut.bat`** once. It replaces your Start Menu Tarkov / Battlestate shortcut so that:

- Start → Escape From Tarkov (or similar) starts **`TarkovNvColor.exe --session`**
- Launcher + game colors are handled automatically
- You don’t need to open the release folder every time

After a Tarkov or BSG update, Windows often recreates a plain launcher shortcut. If colors stop applying from Start Menu, run **`6-...bat` again**.

May ask for Admin once (needed to clean/recreate Start Menu shortcuts).

### Change colors

Edit `config.json` → save → run the bat / command again.

Allowed ranges:

| Setting | Range |
|---------|--------|
| Contrast / Digital Vibrance | 0–100 |
| Gamma | **0.30–2.80** (values outside this are clamped) |

## PowerShell tip

From the exe folder, commands need `.\` in front:

```powershell
cd path\to\folder\with\TarkovNvColor.exe
.\TarkovNvColor.exe --apply game
.\TarkovNvColor.exe --reset
.\TarkovNvColor.exe --session
```

Easy way to open PowerShell there: click the folder address bar, type `powershell`, Enter.

`--apply` and `--session` print a `Config:` line showing which `config.json` was loaded.

## What `--session` does

1. Starts/attaches to Battlestate launcher, or Steam if BSG is missing (`launchMode: auto`).
2. `EscapeFromTarkov.exe` running → **game** preset.
3. Game closes → **default** preset.
4. Exits when the session ends (not a 24/7 background service).

## Launch modes

| Mode | Behavior |
|------|----------|
| `auto` (default) | Prefer BSG; else Steam Tarkov |
| `bsg` | Battlestate only |
| `steam` | Steam only |

```powershell
.\TarkovNvColor.exe --session --bsg
.\TarkovNvColor.exe --session --steam
.\TarkovNvColor.exe --session --game-only
.\TarkovNvColor.exe --session --launcher "C:\Battlestate Games\BsgLauncher\BsgLauncher.exe"
```

Or set `"gameOnly": true` / `"launchMode": "steam"` in `config.json`.

## Hidden console (optional, not recommended)

`"hideConsole": true` hides the console for session/watch modes.

Prefer leaving it visible: you see errors and can `Ctrl+C` (restores colors). If hidden and something sticks, end `TarkovNvColor.exe` in Task Manager, then run `5-Reset-Colors.bat`.

```powershell
.\TarkovNvColor.exe --session --hidden
.\TarkovNvColor.exe --session --show-console
```

## CLI

| Flag | What it does |
|------|----------------|
| `--session` | Launcher-aware session |
| `--session --steam` / `--bsg` / `--game-only` | Launch / watch variants |
| `--session --hidden` / `--show-console` | Console visibility |
| `--session --launcher "path"` | Explicit `BsgLauncher.exe` |
| `--apply game` / `--apply default` | One-shot presets from `config.json` |
| `--reset` | Neutral baseline on the primary monitor |
| `--watch` | Forever game-only watch; prefer `--session` |

## `config.json`

Must sit next to `TarkovNvColor.exe`.

```json
{
  "gameProcess": "EscapeFromTarkov",
  "launcherProcess": "BsgLauncher",
  "launcherPath": null,
  "launchMode": "auto",
  "steamAppId": 3932890,
  "gameOnly": false,
  "hideConsole": false,
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

## Build from source

```powershell
cd path\to\tarkov-auto-nvidia-settings
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true
```

Copy into a clean folder:

- `TarkovNvColor.exe`
- `config.json`
- the numbered `.bat` helpers (optional)

## How it works

- **Digital Vibrance** → NVIDIA NVAPI (`nvapi64.dll`), primary display
- **Contrast / Gamma** → NVIDIA desktop-color formula + `NvAPI_DISP_SetTargetGammaCorrection` on the GDI primary display
- Uses the driver already on your PC

## Notes

- Best with borderless / windowed fullscreen Tarkov on the main monitor.
- Control Panel sliders may lag the real image briefly; the image path is NVAPI.
- Alt-tab focus-based restore is **not** supported (process-based only).
- Not affiliated with Battlestate Games or NVIDIA.
- Use at your own risk. If something looks wrong, run `5-Reset-Colors.bat` / `--reset` or restore defaults in NVIDIA Control Panel.

## License

MIT — see [LICENSE](LICENSE).
