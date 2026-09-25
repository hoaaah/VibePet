using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class ResourceMonitorTests
{
    [Fact]
    public void SampleNowReturnsValidSystemMetrics()
    {
        using var monitor = new ResourceMonitor(1000);
        var metrics = monitor.SampleNow();

        Assert.NotNull(metrics);
        Assert.InRange(metrics.CpuUsagePercentage, 0.0, 100.0);
        Assert.InRange(metrics.RamUsagePercentage, 0.0, 100.0);
        Assert.True(metrics.RamTotalGb > 0, "Total RAM should be greater than 0 GB");
        Assert.True(metrics.AppWorkingSetMb > 0, "App Working Set should be greater than 0 MB");
    }

    [Fact]
    public void CpuHysteresisRequiresSustainedSamplesBeforeTriggering()
    {
        using var monitor = new ResourceMonitor(1000)
        {
            CpuHighThreshold = 80.0,
            CpuLowThreshold = 60.0,
            CpuSustainSamples = 3
        };

        // Initially false
        Assert.False(monitor.IsCpuSustainedHigh);

        // High load sample 1 -> still false (needs 3 consecutive)
        monitor.SimulatedCpuUsage = 85.0;
        monitor.SampleNow();
        Assert.False(monitor.IsCpuSustainedHigh);

        // High load sample 2 -> still false
        monitor.SampleNow();
        Assert.False(monitor.IsCpuSustainedHigh);

        // High load sample 3 -> now TRUE!
        monitor.SampleNow();
        Assert.True(monitor.IsCpuSustainedHigh);

        // Load drops into hysteresis deadband (70% is between 60% and 80%)
        // Should REMAIN TRUE without oscillating!
        monitor.SimulatedCpuUsage = 70.0;
        monitor.SampleNow();
        Assert.True(monitor.IsCpuSustainedHigh);

        // Load drops below low threshold (55% < 60%)
        // Should now reset to FALSE!
        monitor.SimulatedCpuUsage = 55.0;
        monitor.SampleNow();
        Assert.False(monitor.IsCpuSustainedHigh);
    }

    [Fact]
    public void RamHysteresisFunctionsCorrectly()
    {
        using var monitor = new ResourceMonitor(1000)
        {
            RamHighThreshold = 85.0,
            RamLowThreshold = 75.0,
            RamSustainSamples = 2
        };

        Assert.False(monitor.IsRamSustainedHigh);

        // Sample 1
        monitor.SimulatedRamUsage = 90.0;
        monitor.SampleNow();
        Assert.False(monitor.IsRamSustainedHigh);

        // Sample 2
        monitor.SampleNow();
        Assert.True(monitor.IsRamSustainedHigh);

        // Deadband at 80% (between 75% and 85%) -> remains TRUE
        monitor.SimulatedRamUsage = 80.0;
        monitor.SampleNow();
        Assert.True(monitor.IsRamSustainedHigh);

        // Below low threshold (70% < 75%) -> resets to FALSE
        monitor.SimulatedRamUsage = 70.0;
        monitor.SampleNow();
        Assert.False(monitor.IsRamSustainedHigh);
    }
}
