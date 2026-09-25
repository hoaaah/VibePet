using System.IO;
using System.Text.Json;
using System.Windows;

namespace DesktopPet.Services;

public class PetSettings
{
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
    public double Scale { get; set; } = 1.0;
    public bool ReducedMotion { get; set; } = false;
    public bool AutoWander { get; set; } = true;
    public bool GazeTracking { get; set; } = true;
    public bool TypingDetection { get; set; } = true;
    public bool ResourceMonitoring { get; set; } = true;
    public bool ShowResourceBadges { get; set; } = true;
    public double CpuHighThreshold { get; set; } = 80.0;
    public double CpuLowThreshold { get; set; } = 60.0;
    public double RamHighThreshold { get; set; } = 85.0;
    public double RamLowThreshold { get; set; } = 75.0;
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
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        // Default position: bottom-right corner of primary screen above taskbar
        double defaultX = SystemParameters.WorkArea.Right - windowWidth - 20;
        double defaultY = SystemParameters.WorkArea.Bottom - windowHeight - 20;

        if (_settings.X < 0 || _settings.Y < 0)
        {
            return new System.Windows.Point(defaultX, defaultY);
        }

        // Clamp to virtual screen boundaries so it remains visible
        double clampedX = Math.Clamp(_settings.X, vLeft, vLeft + vWidth - windowWidth);
        double clampedY = Math.Clamp(_settings.Y, vTop, vTop + vHeight - windowHeight);

        return new System.Windows.Point(clampedX, clampedY);
    }
}
