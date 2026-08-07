using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Applies NVIDIA desktop color presets when Escape From Tarkov runs, then restores defaults.
/// User knobs live in config.json next to the exe — no rebuild needed to change values.
/// </summary>
internal static class Program
{
    private static AppConfig _cfg = null!;
    private static bool _tarkovActive;

    private static int Main(string[] args)
    {
        _cfg = AppConfig.Load();

        if (args.Length is 1 && args[0].Equals("--reset", StringComparison.OrdinalIgnoreCase))
            return ResetAndExit();

        if (args.Length >= 1 && args[0].Equals("--session", StringComparison.OrdinalIgnoreCase))
        {
            string? launcherPath = _cfg.LauncherPath;
            string? forceMode = null;
            var gameOnly = _cfg.GameOnly;
            var hideConsole = _cfg.HideConsole;
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--launcher", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    launcherPath = args[++i];
                else if (args[i].Equals("--steam", StringComparison.OrdinalIgnoreCase))
                    forceMode = "steam";
                else if (args[i].Equals("--bsg", StringComparison.OrdinalIgnoreCase))
                    forceMode = "bsg";
                else if (args[i].Equals("--game-only", StringComparison.OrdinalIgnoreCase))
                    gameOnly = true;
                else if (args[i].Equals("--hidden", StringComparison.OrdinalIgnoreCase))
                    hideConsole = true;
                else if (args[i].Equals("--show-console", StringComparison.OrdinalIgnoreCase))
                    hideConsole = false;
            }

            if (hideConsole)
                HideConsoleWindow();

            return RunSession(launcherPath, forceMode, startLauncherIfNeeded: !gameOnly, exitWhenBothClosed: true, gameOnly: gameOnly);
        }

        if (args.Length is 1 && args[0].Equals("--watch", StringComparison.OrdinalIgnoreCase))
        {
            if (_cfg.HideConsole)
                HideConsoleWindow();

            return RunSession(null, null, startLauncherIfNeeded: false, exitWhenBothClosed: false, gameOnly: true);
        }

        if (args.Length >= 2 && args[0].Equals("--apply", StringComparison.OrdinalIgnoreCase))
        {
            return args[1].ToLowerInvariant() switch
            {
                "tarkov" or "game" => ApplyAndExit(_cfg.GamePreset, "Game", Parts.All),
                "default" or "defaults" => ApplyAndExit(_cfg.DefaultPreset, "Default", Parts.All),
                "cg-tarkov" or "cg-game" => ApplyAndExit(_cfg.GamePreset, "Game CG only", Parts.ContrastGamma),
                "cg-default" => ApplyAndExit(_cfg.DefaultPreset, "Default CG only", Parts.ContrastGamma),
                "dv-tarkov" or "dv-game" => ApplyAndExit(_cfg.GamePreset, "Game DV only", Parts.DigitalVibrance),
                "dv-default" => ApplyAndExit(_cfg.DefaultPreset, "Default DV only", Parts.DigitalVibrance),
                _ => Usage()
            };
        }

        return Usage();
    }

    private static int RunSession(string? launcherPath, string? forceMode, bool startLauncherIfNeeded, bool exitWhenBothClosed, bool gameOnly)
    {
        var game = _cfg.GameProcess;
        var launcher = _cfg.LauncherProcess;
        var g = _cfg.Game;
        var d = _cfg.Default;
        var mode = forceMode ?? _cfg.LaunchMode;
        var steamMode = false;

        Console.WriteLine("TarkovNvColor — session mode");
        Console.WriteLine($"  Config: {AppConfig.ResolvedPath}");
        if (gameOnly)
        {
            Console.WriteLine("  Mode: game-only (ignore launcher / Steam — colors follow EscapeFromTarkov only)");
        }
        else
        {
            Console.WriteLine($"  Launch mode: {mode} (Steam AppId {_cfg.SteamAppId})");
        }

        Console.WriteLine($"  Game preset:     C{g.Contrast}%  G{g.Gamma:0.00}  DV{g.DigitalVibrance}%");
        Console.WriteLine($"  Default preset:  C{d.Contrast}%  G{d.Gamma:0.00}  DV{d.DigitalVibrance}%");
        if (gameOnly)
        {
            Console.WriteLine($"  1) Wait for {game}.exe (launcher ignored)");
            Console.WriteLine("  2) Game running → game colors");
            Console.WriteLine("  3) Game closes → default colors, then exit");
        }
        else
        {
            Console.WriteLine($"  1) Start BSG launcher or Steam Tarkov → we stay alive");
            Console.WriteLine($"  2) Play → {game}.exe → game colors");
            Console.WriteLine($"  3) Game closes → default colors");
            Console.WriteLine($"  4) BSG: exit when launcher+game closed | Steam: exit when game closed");
        }

        Console.WriteLine($"  Poll every {_cfg.PollMs / 1000.0:0.#}s while session is alive.\n");

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            try { Apply(_cfg.DefaultPreset, Parts.All); } catch { /* ignore */ }
            Environment.Exit(0);
        };

        if (!gameOnly && startLauncherIfNeeded && !IsProcessRunning(game))
        {
            var started = TryStartSessionHost(mode, launcherPath, launcher, out steamMode);
            if (!started)
                return 1;
        }
        else if (!gameOnly && IsProcessRunning(launcher))
        {
            Console.WriteLine($"{launcher} already running — attaching.");
        }
        else if (IsProcessRunning(game))
        {
            Console.WriteLine($"{game} already running — attaching.");
        }
        else if (gameOnly || !exitWhenBothClosed)
        {
            Console.WriteLine($"Waiting for {game}.exe (launcher ignored).");
        }

        if (!gameOnly)
        {
            for (var i = 0; i < 40 && startLauncherIfNeeded &&
                            !IsProcessRunning(launcher) &&
                            !IsProcessRunning("steam") &&
                            !IsProcessRunning(game); i++)
                Thread.Sleep(250);
        }

        _tarkovActive = IsProcessRunning(game);
        try
        {
            if (_tarkovActive)
            {
                Apply(_cfg.GamePreset, Parts.All);
                Log("Game already running → game preset");
            }
            else if (!gameOnly)
            {
                Apply(_cfg.DefaultPreset, Parts.All);
                Log("waiting for Play → default");
            }
            else
            {
                Log("game-only: desktop unchanged until game starts");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        while (true)
        {
            var launcherUp = IsProcessRunning(launcher);
            var steamUp = IsProcessRunning("steam");
            var gameUp = IsProcessRunning(game);

            if (gameUp && !_tarkovActive)
            {
                try
                {
                    Apply(_cfg.GamePreset, Parts.All);
                    _tarkovActive = true;
                    Log("Game started → game preset");
                }
                catch (Exception ex)
                {
                    Log($"apply failed: {ex.Message}");
                }
            }
            else if (!gameUp && _tarkovActive)
            {
                try
                {
                    Apply(_cfg.DefaultPreset, Parts.All);
                    _tarkovActive = false;
                    Log("Game closed → default preset");
                }
                catch (Exception ex)
                {
                    Log($"restore failed: {ex.Message}");
                }

                if (exitWhenBothClosed && (gameOnly || steamMode))
                {
                    Log(gameOnly
                        ? "game-only: game closed → exiting."
                        : "Steam session: game closed → exiting.");
                    return 0;
                }
            }

            if (gameOnly)
            {
                Thread.Sleep(_cfg.PollMs);
                continue;
            }

            if (exitWhenBothClosed && !steamMode && !launcherUp && !gameUp)
            {
                try { Apply(_cfg.DefaultPreset, Parts.All); } catch { /* ignore */ }
                Log("launcher + game both closed → session end, exiting.");
                return 0;
            }

            if (exitWhenBothClosed && steamMode && !_tarkovActive && !gameUp && !steamUp && !launcherUp)
            {
                Thread.Sleep(_cfg.PollMs);
                if (!IsProcessRunning("steam") && !IsProcessRunning(game) && !IsProcessRunning(launcher))
                {
                    try { Apply(_cfg.DefaultPreset, Parts.All); } catch { /* ignore */ }
                    Log("Steam not running and game never started → exiting.");
                    return 0;
                }
            }

            Thread.Sleep(_cfg.PollMs);
        }
    }

    /// <summary>Starts BSG and/or Steam Tarkov depending on launchMode.</summary>
    private static bool TryStartSessionHost(string mode, string? launcherPath, string launcherName, out bool steamMode)
    {
        steamMode = false;

        if (mode is "steam")
        {
            steamMode = true;
            return TryLaunchSteamTarkov();
        }

        if (IsProcessRunning(launcherName))
        {
            Console.WriteLine($"{launcherName} already running — attaching.");
            return true;
        }

        if (mode is "bsg")
        {
            launcherPath ??= FindBsgLauncher();
            if (launcherPath is null || !File.Exists(launcherPath))
            {
                Console.Error.WriteLine(
                    $"{launcherName}.exe not found. Set launcherPath in config.json, pass --launcher, or use launchMode \"steam\" / --steam.");
                return false;
            }

            Console.WriteLine($"Starting BSG launcher: {launcherPath}");
            Process.Start(new ProcessStartInfo(launcherPath) { UseShellExecute = true });
            return true;
        }

        // auto: prefer BSG, fall back to Steam
        launcherPath ??= FindBsgLauncher();
        if (launcherPath is not null && File.Exists(launcherPath))
        {
            Console.WriteLine($"Starting BSG launcher: {launcherPath}");
            Process.Start(new ProcessStartInfo(launcherPath) { UseShellExecute = true });
            return true;
        }

        Console.WriteLine("BSG launcher not found — trying Steam Tarkov...");
        steamMode = true;
        if (TryLaunchSteamTarkov())
            return true;

        Console.Error.WriteLine("""
            Could not start BSG launcher or Steam Tarkov.

              BSG: set launcherPath in config.json
                   or: TarkovNvColor.exe --session --launcher "C:\path\BsgLauncher.exe"

              Steam: install Steam + Escape From Tarkov (AppId 3932890)
                     or set "launchMode": "steam" in config.json
                     or: TarkovNvColor.exe --session --steam
            """);
        return false;
    }

    private static bool TryLaunchSteamTarkov()
    {
        var url = $"steam://rungameid/{_cfg.SteamAppId}";
        try
        {
            Console.WriteLine($"Starting Steam Tarkov: {url}");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Steam launch failed: {ex.Message}");
            var steamExe = FindSteamExe();
            if (steamExe is null)
                return false;

            try
            {
                Console.WriteLine($"Retry via steam.exe -applaunch {_cfg.SteamAppId}");
                Process.Start(new ProcessStartInfo(steamExe, $"-applaunch {_cfg.SteamAppId}")
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex2)
            {
                Console.Error.WriteLine($"steam.exe launch failed: {ex2.Message}");
                return false;
            }
        }
    }

    private static string? FindSteamExe()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string
                       ?? key?.GetValue("SteamExe") as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var exe = path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : Path.Combine(path.Replace('/', '\\'), "steam.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }
        catch
        {
            // ignore
        }

        foreach (var c in new[]
                 {
                     @"C:\Program Files (x86)\Steam\steam.exe",
                     @"C:\Program Files\Steam\steam.exe",
                 })
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    private static bool IsProcessRunning(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindBsgLauncher()
    {
        if (!string.IsNullOrWhiteSpace(_cfg.LauncherPath) && File.Exists(_cfg.LauncherPath))
            return _cfg.LauncherPath;

        var candidates = new[]
        {
            @"C:\Battlestate Games\BsgLauncher\BsgLauncher.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Battlestate Games", "BsgLauncher", "BsgLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Battlestate Games", "BsgLauncher", "BsgLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Battlestate Games", "BsgLauncher", "BsgLauncher.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Battlestate Games", "BsgLauncher", "BsgLauncher.exe"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                 })
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                try
                {
                    var fileName = Path.GetFileName(lnk);
                    if (fileName.Contains("Epic", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var interesting =
                        fileName.Contains("Battle", StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains("Tarkov", StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains("Bsg", StringComparison.OrdinalIgnoreCase);
                    if (!interesting)
                        continue;

                    var target = ReadShortcutTarget(lnk);
                    if (target is not null &&
                        target.EndsWith("BsgLauncher.exe", StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(target))
                        return target;
                }
                catch
                {
                    // ignore bad shortcuts
                }
            }
        }

        return null;
    }

    private static string? ReadShortcutTarget(string lnkPath)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null)
            return null;

        var shell = Activator.CreateInstance(type);
        if (shell is null)
            return null;

        var create = type.GetMethod("CreateShortcut");
        if (create is null)
            return null;

        var shortcut = create.Invoke(shell, [lnkPath]);
        if (shortcut is null)
            return null;

        var targetProp = shortcut.GetType().GetProperty("TargetPath");
        var target = targetProp?.GetValue(shortcut) as string;
        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    private static int ResetAndExit()
    {
        try
        {
            NvidiaDesktopColor.Apply(brightnessPercent: 50, contrastPercent: 50, gamma: 1.00);
            Console.WriteLine("  OK  contrast/gamma → NVIDIA neutral (50 / 1.00)");
            NvidiaDvc.SetDigitalVibrancePercent(50);
            Console.WriteLine("  OK  digital vibrance → 50%");
            Console.WriteLine();
            Console.WriteLine("Clean baseline. Test with --apply game / --apply default");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reset failed: {ex.Message}");
            return 1;
        }
    }

    private static int ApplyAndExit(ColorPreset preset, string label, Parts parts)
    {
        try
        {
            Apply(preset, parts);
            Console.WriteLine($"Applied {label}.");
            if (parts.HasFlag(Parts.ContrastGamma))
                Console.WriteLine($"  contrast {preset.ContrastPercent}%  gamma {preset.Gamma:0.00}");
            if (parts.HasFlag(Parts.DigitalVibrance))
                Console.WriteLine($"  digital vibrance {preset.DigitalVibrancePercent}%");
            else if (parts == Parts.ContrastGamma)
                Console.WriteLine("  (DV not changed — this is CG-only)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Apply failed: {ex.Message}");
            return 1;
        }
    }

    private static void Apply(ColorPreset preset, Parts parts)
    {
        string? cgErr = null, dvErr = null;

        if (parts.HasFlag(Parts.ContrastGamma))
        {
            try
            {
                NvidiaDesktopColor.Apply(_cfg.BrightnessPercent, preset.ContrastPercent, preset.Gamma);
                Console.WriteLine($"  OK  contrast/gamma (NVIDIA formula → C{preset.ContrastPercent} G{preset.Gamma:0.00})");
            }
            catch (Exception ex)
            {
                cgErr = ex.Message;
                Console.WriteLine($"  FAIL contrast/gamma: {ex.Message}");
            }
        }

        if (parts.HasFlag(Parts.DigitalVibrance))
        {
            try
            {
                NvidiaDvc.SetDigitalVibrancePercent(preset.DigitalVibrancePercent);
                Console.WriteLine("  OK  digital vibrance (NVAPI)");
            }
            catch (Exception ex)
            {
                dvErr = ex.Message;
                Console.WriteLine($"  FAIL digital vibrance: {ex.Message}");
            }
        }

        if (cgErr is not null && (parts == Parts.ContrastGamma || dvErr is not null))
            throw new InvalidOperationException(cgErr);
        if (dvErr is not null && parts == Parts.DigitalVibrance)
            throw new InvalidOperationException(dvErr);
        if (cgErr is not null && dvErr is not null)
            throw new InvalidOperationException($"CG: {cgErr} | DV: {dvErr}");
    }

    private static void Log(string msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

    /// <summary>
    /// Once hidden there is no Ctrl+C: killing the process from Task Manager leaves the game
    /// preset applied, so the user has to run --reset. Only used for long-running modes.
    /// </summary>
    private static void HideConsoleWindow()
    {
        try
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
                ShowWindow(handle, SwHide);
        }
        catch
        {
            // no console to hide (already detached) — keep running
        }
    }

    private const int SwHide = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static int Usage()
    {
        var g = _cfg.Game;
        var d = _cfg.Default;
        Console.WriteLine($"""
            TarkovNvColor

              Edit presets in config.json (next to the exe) — no rebuild needed.
              Current game:    C{g.Contrast}%  G{g.Gamma:0.00}  DV{g.DigitalVibrance}%
              Current default: C{d.Contrast}%  G{d.Gamma:0.00}  DV{d.DigitalVibrance}%
              Launch mode:     {_cfg.LaunchMode} (steam AppId {_cfg.SteamAppId})
              Hide console:    {(_cfg.HideConsole ? "on" : "off")} ("hideConsole" in config.json)

              --session [--steam | --bsg | --game-only] [--hidden | --show-console]
                        [--launcher "C:\path\BsgLauncher.exe"]
                  Start BSG/Steam or game-only watch; restore colors; exit.

              --session --game-only
                  Ignore launcher. Colors only while EscapeFromTarkov.exe runs.

              --session --hidden
                  No console window. Stop it from Task Manager, then --reset.

              --apply game | default
              --apply cg-game | cg-default | dv-game | dv-default
              --reset
              --watch   (forever; prefer --session)
            """);
        return 1;
    }

    [Flags]
    private enum Parts
    {
        ContrastGamma = 1,
        DigitalVibrance = 2,
        All = ContrastGamma | DigitalVibrance
    }
}
