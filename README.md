# TarkovNvColor

Small Windows utility that applies your Escape From Tarkov NVIDIA desktop color preset automatically, then restores defaults when you quit.

| Setting | Default | Game (shipped) |
|--------|---------|----------------|
| Contrast | 50% | 60% |
| Gamma | 1.00 | 1.50 |
| Digital Vibrance | 50% | 70% |

## Requirements

- Windows
- NVIDIA GPU + driver (`nvapi64.dll` is already installed with the driver — nothing extra to download)
- [.NET 10 runtime](https://dotnet.microsoft.com/download), **or** a self-contained publish (see Build)

## Usage

Recommended — one play session (starts/attaches to Battlestate Launcher, exits when launcher + game are both closed):

```powershell
.\TarkovNvColor.exe --session
```

If the launcher is not found automatically, it falls back to **Steam** (`steam://rungameid/3932890`). Force a mode:

```powershell
.\TarkovNvColor.exe --session --steam
.\TarkovNvColor.exe --session --bsg
.\TarkovNvColor.exe --session --launcher "C:\Battlestate Games\BsgLauncher\BsgLauncher.exe"
```

Or set in `config.json`: `"launchMode": "steam"` / `"bsg"` / `"auto"`.

Test presets manually:

```powershell
.\TarkovNvColor.exe --apply game
.\TarkovNvColor.exe --apply default
.\TarkovNvColor.exe --reset
```

| Flag | What it does |
|------|----------------|
| `--session` | Start BSG or Steam Tarkov (`launchMode`), apply game colors while Tarkov runs, restore, exit |
| `--session --steam` | Force Steam launch (`steam://rungameid/3932890`) |
| `--session --bsg` | Force Battlestate launcher only |
| `--apply game` | One-shot apply the **game** preset from `config.json` |
| `--apply default` | One-shot apply the **default** preset from `config.json` |
| `--reset` | Neutral baseline (contrast/gamma 50 / 1.00, DV 50) |
| `--watch` | Forever watch (prefer `--session`) |

From source instead of a published exe:

```powershell
dotnet run -c Release -- --session
dotnet run -c Release -- --apply game
```

## Edit your colors (`config.json`)

Want different numbers? Edit `config.json` next to the exe in Notepad. **No rebuild.**

Example: bump DV to 80, lower gamma to 1.2 → save → run again.

```json
{
  "gameProcess": "EscapeFromTarkov",
  "launcherProcess": "BsgLauncher",
  "launcherPath": null,
  "launchMode": "auto",
  "steamAppId": 3932890,
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
| `game` | Values while Tarkov is running |
| `default` | Values when Tarkov is not running |
| `launchMode` | `auto` (BSG if found, else Steam), `bsg`, or `steam` |
| `steamAppId` | Steam App ID for Escape From Tarkov (`3932890`) |
| `launcherPath` | Optional full path to `BsgLauncher.exe` if auto-detect fails |
| `gameProcess` / `launcherProcess` | Process names without `.exe` |
| `pollMs` | How often to check (ms) while a session is alive |
| `brightnessPercent` | Desktop brightness left at this value on apply (usually 50) |

`config.json` is copied next to the exe on build/publish. Keep it beside `TarkovNvColor.exe`.

## Build from source

```powershell
cd path\to\tarkov-auto-nvidia-settings
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Output: `publish\TarkovNvColor.exe` + `publish\config.json`

Self-contained (no separate .NET install for users):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

### Start Menu shortcut (optional)

Point your Battlestate Games Launcher shortcut at:

- Target: `...\publish\TarkovNvColor.exe`
- Arguments: `--session`

Icon can stay `BsgLauncher.exe`.

## How it works

- **Digital Vibrance** → NVIDIA NVAPI (`nvapi64.dll`)
- **Contrast / Gamma** → NVIDIA desktop-color formula + `NvAPI_DISP_SetTargetGammaCorrection` (same math as Control Panel)
- **Session mode** → not a 24/7 background service; lives only for one play session

## Notes

- Best with borderless / windowed fullscreen Tarkov on the main monitor.
- Uses `nvapi64.dll` already on your PC (same idea as other desktop-color utilities / Control Panel). One entry point is not listed in the public SDK docs; nothing extra is downloaded.
- Control Panel sliders may lag the real image briefly; the image path is NVAPI.
- Not affiliated with Battlestate Games or NVIDIA.
- Use at your own risk. If something looks wrong, run `--reset` or restore defaults in NVIDIA Control Panel.

## License

MIT — see [LICENSE](LICENSE).
