using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class DragMotionTrackerTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Begin_StartsHeldWithoutDirection()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));

        Assert.True(tracker.IsHeld);
        Assert.Null(tracker.Direction);
        Assert.Equal(DragPoseAction.None, tracker.Tick(Ms(1000)));
    }

    [Theory]
    [InlineData(5, PetAnimationState.RunningRight)]
    [InlineData(-5, PetAnimationState.RunningLeft)]
    public void FirstHorizontalMove_PlaysRunningInThatDirection(double deltaX, PetAnimationState expected)
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));

        Assert.Equal(DragPoseAction.PlayRunning, tracker.OnMoved(deltaX, 0, Ms(10)));
        Assert.Equal(expected, tracker.Direction);
        Assert.False(tracker.IsHeld);
    }

    [Fact]
    public void ContinuedMoveSameDirection_DoesNotRestartAnimation()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));
        tracker.OnMoved(5, 0, Ms(10));

        Assert.Equal(DragPoseAction.None, tracker.OnMoved(4, 1, Ms(20)));
        Assert.Equal(DragPoseAction.None, tracker.OnMoved(3, -2, Ms(30)));
        Assert.Equal(PetAnimationState.RunningRight, tracker.Direction);
    }

    [Fact]
    public void SmallReverseJitter_DoesNotFlipDirection()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));
        tracker.OnMoved(10, 0, Ms(10));

        Assert.Equal(DragPoseAction.None, tracker.OnMoved(-2, 0, Ms(20)));
        Assert.Equal(DragPoseAction.None, tracker.OnMoved(-2, 0, Ms(30)));
        Assert.Equal(PetAnimationState.RunningRight, tracker.Direction);

        // Moving forward again resets the accumulated reverse distance
        tracker.OnMoved(3, 0, Ms(40));
        Assert.Equal(DragPoseAction.None, tracker.OnMoved(-4, 0, Ms(50)));
        Assert.Equal(PetAnimationState.RunningRight, tracker.Direction);
    }

    [Fact]
    public void SustainedReverseMove_FlipsDirection()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));
        tracker.OnMoved(10, 0, Ms(10));

        Assert.Equal(DragPoseAction.None, tracker.OnMoved(-3, 0, Ms(20)));
        Assert.Equal(DragPoseAction.PlayRunning, tracker.OnMoved(-3, 0, Ms(30)));
        Assert.Equal(PetAnimationState.RunningLeft, tracker.Direction);
    }

    [Fact]
    public void CursorStill_HoldsPoseAfterTimeout_ThenResumesOnMove()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));
        tracker.OnMoved(5, 0, Ms(100));

        Assert.Equal(DragPoseAction.None, tracker.Tick(Ms(100) + DragMotionTracker.StillnessTimeout - Ms(1)));
        Assert.Equal(DragPoseAction.Hold, tracker.Tick(Ms(100) + DragMotionTracker.StillnessTimeout));
        Assert.True(tracker.IsHeld);
        Assert.Equal(DragPoseAction.None, tracker.Tick(Ms(1000)));

        Assert.Equal(DragPoseAction.ResumeRunning, tracker.OnMoved(5, 0, Ms(1100)));
        Assert.Equal(PetAnimationState.RunningRight, tracker.Direction);
    }

    [Fact]
    public void VerticalOnlyMove_BeforeAnyDirection_StaysHeld()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));

        Assert.Equal(DragPoseAction.None, tracker.OnMoved(0, 8, Ms(10)));
        Assert.True(tracker.IsHeld);
        Assert.Null(tracker.Direction);
    }

    [Fact]
    public void VerticalMove_AfterHold_ResumesLastDirection()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));
        tracker.OnMoved(-5, 0, Ms(10));
        tracker.Tick(Ms(500));

        Assert.Equal(DragPoseAction.ResumeRunning, tracker.OnMoved(0, 6, Ms(600)));
        Assert.Equal(PetAnimationState.RunningLeft, tracker.Direction);
    }

    [Fact]
    public void SubPixelMove_IsIgnored()
    {
        var tracker = new DragMotionTracker();
        tracker.Begin(Ms(0));

        Assert.Equal(DragPoseAction.None, tracker.OnMoved(0.2, 0.1, Ms(10)));
        Assert.Null(tracker.Direction);
    }
}
