using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class ProcessWatcherTests
{
    [Fact]
    public void ProcessWatcher_AddAndRemoveProcesses_NormalizesCorrectly()
    {
        using var watcher = new ProcessWatcherService(new[] { "dotnet", "NODE.EXE", "cargo" });

        Assert.Equal(3, watcher.TargetProcessNames.Count);
        Assert.Contains("dotnet", watcher.TargetProcessNames);
        Assert.Contains("NODE", watcher.TargetProcessNames);
        Assert.Contains("cargo", watcher.TargetProcessNames);

        // Add with .exe extension should normalize without .exe
        watcher.AddTargetProcess("ffmpeg.exe");
        Assert.Contains("ffmpeg", watcher.TargetProcessNames);
        Assert.Equal(4, watcher.TargetProcessNames.Count);

        // Deduplication (case-insensitive)
        watcher.AddTargetProcess("DotNet");
        Assert.Equal(4, watcher.TargetProcessNames.Count);

        // Remove
        watcher.RemoveTargetProcess("node");
        Assert.Equal(3, watcher.TargetProcessNames.Count);
        Assert.DoesNotContain("node", watcher.TargetProcessNames);
    }

    [Fact]
    public void ProcessWatcher_ToggleEnabled_UpdatesState()
    {
        using var watcher = new ProcessWatcherService();
        Assert.True(watcher.IsEnabled);

        watcher.IsEnabled = false;
        Assert.False(watcher.IsEnabled);
    }
}
