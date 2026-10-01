using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class PetSettings
{
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
    public double Scale { get; set; } = 1.0;
    public bool ReducedMotion { get; set; } = false;
    public bool AutoWander { get; set; } = true;
    public string SelectedSkin { get; set; } = SkinManagerService.BuiltInSkinId;
    /// <summary>"auto" (follow Windows display language), "en" or "id".</summary>
    public string Language { get; set; } = "auto";
    [JsonConverter(typeof(JsonStringEnumConverter<WorkAnimationStyle>))]
    public WorkAnimationStyle WorkAnimationStyle { get; set; } = WorkAnimationStyle.Pacing;
    public bool GazeTracking { get; set; } = true;
    public bool TypingDetection { get; set; } = true;
    public bool ResourceMonitoring { get; set; } = true;
    public bool ShowResourceBadges { get; set; } = true;
    public double CpuHighThreshold { get; set; } = 80.0;
    public double CpuLowThreshold { get; set; } = 60.0;
    public double RamHighThreshold { get; set; } = 85.0;
    public double RamLowThreshold { get; set; } = 75.0;
    public bool EnableIpc { get; set; } = true;
    public bool EnableProcessWatcher { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public List<string> WatchedProcesses { get; set; } =
    [
        // Developer tools & builds
        "dotnet", "node", "pwsh", "cargo", "ffmpeg",
        // Editors, Git client & AI coding CLIs
        "code", "gitkraken", "claude", "codex", "agy",
        // Documents & browser
        "winword", "msedge",
    ];
}

public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string _settingsFolder;
    private readonly string _settingsFile;
    private PetSettings _settings = new();

    public PetSettings Settings => _settings;

    public SettingsService()
    {
        _settingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopPet"
        );
        _settingsFile = Path.Combine(_settingsFolder, "settings.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                string json = File.ReadAllText(_settingsFile);
                var loaded = JsonSerializer.Deserialize<PetSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch
        {
            _settings = new PetSettings();
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(_settingsFolder))
            {
                Directory.CreateDirectory(_settingsFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFile, json);
        }
        catch
        {
            // Ignore settings save errors gracefully
        }
    }

    public System.Windows.Point GetValidatedPosition(double windowWidth, double windowHeight)
    {
        double primaryDefaultX = SystemParameters.WorkArea.Right - windowWidth - 20;
        double primaryDefaultY = SystemParameters.WorkArea.Bottom - windowHeight - 20;

        if (_settings.X < 0 || _settings.Y < 0)
        {
            return new System.Windows.Point(primaryDefaultX, primaryDefaultY);
        }

        // Multi-monitor awareness: verify position intersects an active screen's working area
        var targetRect = new System.Drawing.Rectangle(
            (int)_settings.X,
            (int)_settings.Y,
            (int)Math.Ceiling(windowWidth),
            (int)Math.Ceiling(windowHeight)
        );

        var screens = System.Windows.Forms.Screen.AllScreens;
        System.Windows.Forms.Screen? bestScreen = null;
        int maxIntersectionArea = 0;

        foreach (var screen in screens)
        {
            var intersection = System.Drawing.Rectangle.Intersect(screen.WorkingArea, targetRect);
            int area = intersection.Width * intersection.Height;
            if (area > maxIntersectionArea)
            {
                maxIntersectionArea = area;
                bestScreen = screen;
            }
        }

        // If completely disconnected from any active screen (e.g. secondary monitor unplugged), restore to primary screen
        if (bestScreen == null || maxIntersectionArea == 0)
        {
            return new System.Windows.Point(primaryDefaultX, primaryDefaultY);
        }

        // Clamp safely within the target screen's working area
        var work = bestScreen.WorkingArea;
        double clampedX = Math.Clamp(_settings.X, work.Left, Math.Max(work.Left, work.Right - windowWidth));
        double clampedY = Math.Clamp(_settings.Y, work.Top, Math.Max(work.Top, work.Bottom - windowHeight));

        return new System.Windows.Point(clampedX, clampedY);
    }
}
