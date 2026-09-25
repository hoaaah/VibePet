using DesktopPet.Models;

namespace DesktopPet.Services;

public class GazeTracker
{
    public double DeadzoneRadius { get; set; } = 75.0;

    public GazeDirection? CalculateGaze(
        double petCenterX,
        double petCenterY,
        double cursorX,
        double cursorY)
    {
        double dx = cursorX - petCenterX;
        double dy = cursorY - petCenterY;

        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < DeadzoneRadius)
        {
            // Inside deadzone -> neutral / no forced gaze
            return null;
        }

        // Angle clockwise starting from North (0° is Up)
        // Screen coords: dx > 0 is East/Right, dy > 0 is South/Down
        // Vector pointing Up has dx = 0, dy < 0
        double rad = Math.Atan2(dx, -dy);
        if (rad < 0)
        {
            rad += 2 * Math.PI;
        }

        double deg = rad * (180.0 / Math.PI);

        // 16 sectors: 360 / 16 = 22.5 degrees each
        int index = ((int)Math.Round(deg / 22.5)) % 16;
        return (GazeDirection)index;
    }
}
