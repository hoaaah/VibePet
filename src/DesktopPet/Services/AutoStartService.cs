using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DesktopPet.Services;

public class AutoStartService
{
    public const string DefaultAppName = "DesktopPet";
    public const string DefaultRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _appName;
    private readonly string _subKeyPath;

    public AutoStartService(string appName = DefaultAppName, string subKeyPath = DefaultRegistrySubKey)
    {
        _appName = appName;
        _subKeyPath = subKeyPath;
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath, false);
            if (key == null) return false;

            object? value = key.GetValue(_appName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    public string? GetRegisteredPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath, false);
            return key?.GetValue(_appName) as string;
        }
        catch
        {
            return null;
        }
    }

    public bool SetAutoStart(bool enable, string? customExecutablePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(_subKeyPath);

            if (enable)
            {
                string exePath = customExecutablePath ?? Environment.ProcessPath ?? "";
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    // Fallback to base directory executable
                    string baseExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DesktopPet.exe");
                    if (File.Exists(baseExe))
                    {
                        exePath = baseExe;
                    }
                }

                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(_appName, $"\"{exePath}\"");
                    return true;
                }
                return false;
            }
            else
            {
                if (key.GetValue(_appName) != null)
                {
                    key.DeleteValue(_appName, false);
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStartService] Error: {ex.Message}");
            return false;
        }
    }
}
