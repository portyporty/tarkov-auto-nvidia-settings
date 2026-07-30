using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class AppConfig
{
    [JsonPropertyName("gameProcess")]
    public string GameProcess { get; set; } = "EscapeFromTarkov";

    [JsonPropertyName("launcherProcess")]
    public string LauncherProcess { get; set; } = "BsgLauncher";

    [JsonPropertyName("launcherPath")]
    public string? LauncherPath { get; set; }

    [JsonPropertyName("pollMs")]
    public int PollMs { get; set; } = 3000;

    [JsonPropertyName("brightnessPercent")]
    public int BrightnessPercent { get; set; } = 50;

    [JsonPropertyName("default")]
    public PresetConfig Default { get; set; } = new() { Contrast = 50, Gamma = 1.0, DigitalVibrance = 50 };

    [JsonPropertyName("game")]
    public PresetConfig Game { get; set; } = new() { Contrast = 60, Gamma = 1.5, DigitalVibrance = 70 };

    public ColorPreset DefaultPreset => new(Default.Contrast, Default.Gamma, Default.DigitalVibrance);
    public ColorPreset GamePreset => new(Game.Contrast, Game.Gamma, Game.DigitalVibrance);

    public static string ResolvedPath { get; private set; } = "config.json";

    public static AppConfig Load()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
                if (cfg is null)
                    continue;

                ResolvedPath = path;
                cfg.Normalize();
                return cfg;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read {path}: {ex.Message}");
                Console.Error.WriteLine("Using built-in defaults.");
                return new AppConfig();
            }
        }

        Console.WriteLine("config.json not found next to the exe — using built-in defaults.");
        Console.WriteLine("Edit/create config.json beside TarkovNvColor.exe to customize presets.\n");
        return new AppConfig();
    }

    private void Normalize()
    {
        if (string.IsNullOrWhiteSpace(GameProcess))
            GameProcess = "EscapeFromTarkov";
        if (string.IsNullOrWhiteSpace(LauncherProcess))
            LauncherProcess = "BsgLauncher";
        if (PollMs < 500)
            PollMs = 500;
        BrightnessPercent = Math.Clamp(BrightnessPercent, 0, 100);
        Default.Clamp();
        Game.Clamp();
        if (string.IsNullOrWhiteSpace(LauncherPath))
            LauncherPath = null;
    }
}

internal sealed class PresetConfig
{
    [JsonPropertyName("contrast")]
    public int Contrast { get; set; } = 50;

    [JsonPropertyName("gamma")]
    public double Gamma { get; set; } = 1.0;

    [JsonPropertyName("digitalVibrance")]
    public int DigitalVibrance { get; set; } = 50;

    public void Clamp()
    {
        Contrast = Math.Clamp(Contrast, 0, 100);
        Gamma = Math.Clamp(Gamma, 0.30, 1.80);
        DigitalVibrance = Math.Clamp(DigitalVibrance, 0, 100);
    }
}

[JsonSerializable(typeof(AppConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AppConfigJsonContext : JsonSerializerContext;

internal readonly record struct ColorPreset(int ContrastPercent, double Gamma, int DigitalVibrancePercent);
