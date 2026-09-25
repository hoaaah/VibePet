using System.Threading;
using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class SpriteSheetManagerTests
{
    [Fact]
    public void LoadAndSliceAllFramesSuccessfully()
    {
        // WPF BitmapSource operations require STA thread
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var manager = SpriteSheetManager.Instance;
                manager.Load();
                Assert.True(manager.IsLoaded);

                // Verify all 9 animations have the correct number of frames
                foreach (var (state, def) in AnimationCatalog.Animations)
                {
                    var frames = manager.GetAnimationFrames(state);
                    Assert.NotNull(frames);
                    Assert.Equal(def.FrameCount, frames.Length);

                    foreach (var frame in frames)
                    {
                        Assert.NotNull(frame);
                        Assert.Equal(AnimationCatalog.CellWidth, frame.PixelWidth);
                        Assert.Equal(AnimationCatalog.CellHeight, frame.PixelHeight);
                    }
                }

                // Verify all 16 gaze poses
                foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
                {
                    var frame = manager.GetGazeFrame(gaze);
                    Assert.NotNull(frame);
                    Assert.Equal(AnimationCatalog.CellWidth, frame.PixelWidth);
                    Assert.Equal(AnimationCatalog.CellHeight, frame.PixelHeight);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw threadEx;
        }
    }
}
