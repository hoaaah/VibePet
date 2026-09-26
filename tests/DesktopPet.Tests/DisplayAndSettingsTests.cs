using System.Windows;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class DisplayAndSettingsTests
{
    [Fact]
    public void DefaultSettingsIncludeStage5Properties()
    {
        var settings = new PetSettings();
        Assert.False(settings.StartWithWindows);
        Assert.True(settings.EnableIpc);
        Assert.True(settings.EnableProcessWatcher);
        Assert.NotEmpty(settings.WatchedProcesses);
    }

    [Fact]
    public void GetValidatedPosition_WithNegativeCoords_ReturnsPrimaryDefault()
    {
        var service = new SettingsService();
        service.Settings.X = -1;
        service.Settings.Y = -1;

        Point pos = service.GetValidatedPosition(192, 208);
        Assert.True(pos.X >= 0);
        Assert.True(pos.Y >= 0);
    }

    [Fact]
    public void GetValidatedPosition_WithExtremeOffscreenCoords_RecoversToActiveScreen()
    {
        var service = new SettingsService();
        // Place coordinates way off into outer space (e.g. disconnected 4K screen at X=99999, Y=99999)
        service.Settings.X = 99999;
        service.Settings.Y = 99999;

        Point pos = service.GetValidatedPosition(192, 208);

        // Should recover back to primary screen bounds
        double rightLimit = SystemParameters.WorkArea.Right;
        double bottomLimit = SystemParameters.WorkArea.Bottom;

        Assert.True(pos.X < rightLimit);
        Assert.True(pos.Y < bottomLimit);
        Assert.True(pos.X >= 0);
        Assert.True(pos.Y >= 0);
    }

    [Fact]
    public void GetValidatedPosition_WithinScreenBounds_PreservesPosition()
    {
        var service = new SettingsService();
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        Assert.NotNull(primary);

        double targetX = primary.WorkingArea.Left + 100;
        double targetY = primary.WorkingArea.Top + 100;

        service.Settings.X = targetX;
        service.Settings.Y = targetY;

        Point pos = service.GetValidatedPosition(192, 208);
        Assert.Equal(targetX, pos.X);
        Assert.Equal(targetY, pos.Y);
    }
}
