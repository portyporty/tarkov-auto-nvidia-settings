using System.Diagnostics;

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
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--launcher", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    launcherPath = args[++i];
            }

            return RunSession(launcherPath, startLauncherIfNeeded: true, exitWhenBothClosed: true);
        }

        if (args.Length is 1 && args[0].Equals("--watch", StringComparison.OrdinalIgnoreCase))
            return RunSession(null, startLauncherIfNeeded: false, exitWhenBothClosed: false);

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

    private static int RunSession(string? launcherPath, bool startLauncherIfNeeded, bool exitWhenBothClosed)
    {
        var game = _cfg.GameProcess;
        var launcher = _cfg.LauncherProcess;
        var g = _cfg.Game;
        var d = _cfg.Default;

        Console.WriteLine("TarkovNvColor — session mode");
        Console.WriteLine($"  Config: {AppConfig.ResolvedPath}");
        Console.WriteLine($"  Game preset:     C{g.Contrast}%  G{g.Gamma:0.00}  DV{g.DigitalVibrance}%");
        Console.WriteLine($"  Default preset:  C{d.Contrast}%  G{d.Gamma:0.00}  DV{d.DigitalVibrance}%");
        Console.WriteLine($"  1) {launcher} starts (or already open) → we stay alive");
        Console.WriteLine($"  2) Play → {game}.exe → game colors");
        Console.WriteLine($"  3) Game closes → default colors (launcher may stay open)");
        Console.WriteLine($"  4) Launcher + game both closed → we exit");
        Console.WriteLine($"  Poll every {_cfg.PollMs / 1000.0:0.#}s while session is alive.\n");

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            try { Apply(_cfg.DefaultPreset, Parts.All); } catch { /* ignore */ }
            Environment.Exit(0);
        };

        if (startLauncherIfNeeded && !IsProcessRunning(launcher))
        {
            launcherPath ??= FindBsgLauncher();
            if (launcherPath is null || !File.Exists(launcherPath))
            {
                Console.Error.WriteLine(
                    $"{launcher}.exe not found automatically. Set launcherPath in config.json or pass:\n" +
                    "  TarkovNvColor.exe --session --launcher \"C:\\path\\to\\BsgLauncher.exe\"");
                return 1;
            }

            Console.WriteLine($"Starting launcher: {launcherPath}");
            Process.Start(new ProcessStartInfo(launcherPath) { UseShellExecute = true });
        }
        else if (IsProcessRunning(launcher))
        {
            Console.WriteLine($"{launcher} already running — attaching.");
        }
        else if (!exitWhenBothClosed)
        {
            Console.WriteLine($"Forever watch: waiting for {game}.exe (no auto-exit).");
        }

        for (var i = 0; i < 20 && startLauncherIfNeeded &&
                        !IsProcessRunning(launcher) &&
                        !IsProcessRunning(game); i++)
            Thread.Sleep(250);

        _tarkovActive = IsProcessRunning(game);
        try
        {
            Apply(_tarkovActive ? _cfg.GamePreset : _cfg.DefaultPreset, Parts.All);
            Log(_tarkovActive ? "Game already running → game preset" : "waiting for Play → default");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        while (true)
        {
            var launcherUp = IsProcessRunning(launcher);
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
            }

            if (exitWhenBothClosed && !launcherUp && !gameUp)
            {
                try { Apply(_cfg.DefaultPreset, Parts.All); } catch { /* ignore */ }
                Log("launcher + game both closed → session end, exiting.");
                return 0;
            }

            Thread.Sleep(_cfg.PollMs);
        }
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

    private static int Usage()
    {
        var g = _cfg.Game;
        var d = _cfg.Default;
        Console.WriteLine($"""
            TarkovNvColor

              Edit presets in config.json (next to the exe) — no rebuild needed.
              Current game:    C{g.Contrast}%  G{g.Gamma:0.00}  DV{g.DigitalVibrance}%
              Current default: C{d.Contrast}%  G{d.Gamma:0.00}  DV{d.DigitalVibrance}%

              --session [--launcher "C:\path\BsgLauncher.exe"]
                  Start/attach Battlestate Launcher, watch Tarkov, exit when both closed.

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
