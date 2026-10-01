using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class WorkPacingTests
{
    [Fact]
    public void ComputePacingBounds_CentersOnHomeWithinWorkArea()
    {
        var bounds = PetMovementManager.ComputePacingBounds(home: 500, halfRange: 70, areaLeft: 0, areaRight: 1920, windowWidth: 200);
        Assert.Equal((430.0, 570.0), bounds);
    }

    [Fact]
    public void ComputePacingBounds_ClampsAtScreenEdges()
    {
        // Near the right edge: the window (200 wide) must stay inside 1920
        var right = PetMovementManager.ComputePacingBounds(home: 1700, halfRange: 70, areaLeft: 0, areaRight: 1920, windowWidth: 200);
        Assert.Equal((1630.0, 1720.0), right);

        // Secondary monitor on the left with negative coordinates
        var left = PetMovementManager.ComputePacingBounds(home: -1900, halfRange: 70, areaLeft: -1920, areaRight: 0, windowWidth: 200);
        Assert.Equal((-1920.0, -1830.0), left);
    }

    [Fact]
    public void ComputePacingBounds_NoRoom_ReturnsNull()
    {
        // Work area barely wider than the pet
        Assert.Null(PetMovementManager.ComputePacingBounds(home: 0, halfRange: 70, areaLeft: 0, areaRight: 210, windowWidth: 200));
    }

    [Fact]
    public void NextPacingStep_MovesAndTurnsAroundAtBounds()
    {
        Assert.Equal((102.5, PetAnimationState.RunningRight),
            PetMovementManager.NextPacingStep(100, PetAnimationState.RunningRight, 2.5, 0, 200));
        Assert.Equal((97.5, PetAnimationState.RunningLeft),
            PetMovementManager.NextPacingStep(100, PetAnimationState.RunningLeft, 2.5, 0, 200));

        Assert.Equal((200.0, PetAnimationState.RunningLeft),
            PetMovementManager.NextPacingStep(199, PetAnimationState.RunningRight, 2.5, 0, 200));
        Assert.Equal((0.0, PetAnimationState.RunningRight),
            PetMovementManager.NextPacingStep(1, PetAnimationState.RunningLeft, 2.5, 0, 200));
    }

    [Fact]
    public void NextPacingStep_FullCycle_StaysWithinBounds()
    {
        double left = 100;
        var direction = PetAnimationState.RunningRight;
        int turns = 0;

        for (int i = 0; i < 500; i++)
        {
            var (next, nextDirection) = PetMovementManager.NextPacingStep(left, direction, 2.5, 30, 170);
            Assert.InRange(next, 30, 170);
            if (nextDirection != direction) turns++;
            (left, direction) = (next, nextDirection);
        }

        Assert.True(turns >= 4);
    }

    [Theory]
    [InlineData(75, 1500, true)]    // 5% of one core
    [InlineData(74, 1500, false)]
    [InlineData(1500, 1500, true)]  // fully busy
    [InlineData(10, 0, false)]      // first sample has no interval yet
    public void IsCpuBusy_UsesFractionOfOneCore(int cpuMs, int wallMs, bool expected)
    {
        Assert.Equal(expected, ProcessWatcherService.IsCpuBusy(TimeSpan.FromMilliseconds(cpuMs), TimeSpan.FromMilliseconds(wallMs)));
    }

    [Fact]
    public void IsWithinLinger_KeepsWorkBrieflyAfterLastBusySample()
    {
        var busyAt = TimeSpan.FromSeconds(10);
        Assert.True(ProcessWatcherService.IsWithinLinger(busyAt, busyAt + ProcessWatcherService.WorkLinger));
        Assert.False(ProcessWatcherService.IsWithinLinger(busyAt, busyAt + ProcessWatcherService.WorkLinger + TimeSpan.FromMilliseconds(1)));
        Assert.False(ProcessWatcherService.IsWithinLinger(null, busyAt));
    }

    [Theory]
    [InlineData("dotnet", true)]
    [InlineData("node.exe", true)]
    [InlineData("claude", true)]
    [InlineData("my-custom-tool", true)]
    [InlineData("code", false)]
    [InlineData("WINWORD", false)]
    [InlineData("msedge", false)]
    [InlineData("gitkraken", false)]
    public void CountsAsWork_ExcludesInteractiveGuiApps(string processName, bool expected)
    {
        Assert.Equal(expected, ProcessNameFormatter.CountsAsWork(processName));
    }
}
