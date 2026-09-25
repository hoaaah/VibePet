using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class GazeTrackerTests
{
    private readonly GazeTracker _tracker = new() { DeadzoneRadius = 50.0 };

    [Fact]
    public void CursorInsideDeadzoneReturnsNull()
    {
        double centerX = 200;
        double centerY = 200;

        // Exactly at center
        Assert.Null(_tracker.CalculateGaze(centerX, centerY, 200, 200));

        // 30px away (within 50px radius)
        Assert.Null(_tracker.CalculateGaze(centerX, centerY, 230, 200));
        Assert.Null(_tracker.CalculateGaze(centerX, centerY, 200, 170));
    }

    [Theory]
    // (targetX, targetY, expectedGaze)
    [InlineData(200, 50, GazeDirection.Deg0)]        // Directly Up (dy = -150)
    [InlineData(300, 100, GazeDirection.Deg45)]     // Up-Right 45°
    [InlineData(350, 200, GazeDirection.Deg90)]     // Directly Right (dx = +150)
    [InlineData(300, 300, GazeDirection.Deg135)]    // Down-Right 135°
    [InlineData(200, 350, GazeDirection.Deg180)]    // Directly Down (dy = +150)
    [InlineData(100, 300, GazeDirection.Deg225)]    // Down-Left 225°
    [InlineData(50, 200, GazeDirection.Deg270)]     // Directly Left (dx = -150)
    [InlineData(100, 100, GazeDirection.Deg315)]    // Up-Left 315°
    public void CalculateGazeIdentifiesCorrectSector(double cursorX, double cursorY, GazeDirection expected)
    {
        double centerX = 200;
        double centerY = 200;

        var result = _tracker.CalculateGaze(centerX, centerY, cursorX, cursorY);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }
}
