using System.Threading;
using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;
using Xunit.Abstractions;

namespace DesktopPet.Tests;

public class BenchmarkExecutionTests
{
    private readonly ITestOutputHelper _output;

    public BenchmarkExecutionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecuteOfficialBenchmarks()
    {
        // 1. Scenario: Idle with Monitoring
        SpriteSheetManager.Instance.Load();
        SpritePlayer? player = null;
        ResourceMonitor? monitor = null;

        var idleResult = await BenchmarkService.RunScenarioAsync(
            "Skenario 1: Idle (Overlay & Monitoring Aktif)",
            durationSeconds: 2,
            setup: () =>
            {
                player = new SpritePlayer(SpriteSheetManager.Instance);
                player.PlayAnimation(PetAnimationState.Idle);
                monitor = new ResourceMonitor(1000);
                monitor.Start();
            },
            cleanup: () =>
            {
                player?.Pause();
                monitor?.Stop();
                monitor?.Dispose();
            }
        );

        _output.WriteLine($"[BENCHMARK] {idleResult.ScenarioName}:");
        _output.WriteLine($"   Working Set Rata-rata : {idleResult.AvgWorkingSetMb:0.0} MB");
        _output.WriteLine($"   Working Set Puncak     : {idleResult.PeakWorkingSetMb:0.0} MB");
        _output.WriteLine($"   CPU Rata-rata          : {idleResult.AvgCpuPercentage:0.00}%");
        _output.WriteLine($"   GC Gen0/1/2            : {idleResult.Gen0Collections}/{idleResult.Gen1Collections}/{idleResult.Gen2Collections}");

        // 2. Scenario: Active Animation (Running Right)
        var animResult = await BenchmarkService.RunScenarioAsync(
            "Skenario 2: Animasi Aktif (Running Right Loop)",
            durationSeconds: 2,
            setup: () =>
            {
                player = new SpritePlayer(SpriteSheetManager.Instance);
                player.PlayAnimation(PetAnimationState.RunningRight);
            },
            cleanup: () =>
            {
                player?.Pause();
            }
        );

        _output.WriteLine($"[BENCHMARK] {animResult.ScenarioName}:");
        _output.WriteLine($"   Working Set Rata-rata : {animResult.AvgWorkingSetMb:0.0} MB");
        _output.WriteLine($"   Working Set Puncak     : {animResult.PeakWorkingSetMb:0.0} MB");
        _output.WriteLine($"   CPU Rata-rata          : {animResult.AvgCpuPercentage:0.00}%");
        _output.WriteLine($"   GC Gen0/1/2            : {animResult.Gen0Collections}/{animResult.Gen1Collections}/{animResult.Gen2Collections}");

        // 3. Scenario: High-Frequency Monitoring (Sampling 250ms)
        var monResult = await BenchmarkService.RunScenarioAsync(
            "Skenario 3: High-Frequency Monitoring (250ms interval)",
            durationSeconds: 2,
            setup: () =>
            {
                monitor = new ResourceMonitor(250);
                monitor.Start();
            },
            cleanup: () =>
            {
                monitor?.Stop();
                monitor?.Dispose();
            }
        );

        _output.WriteLine($"[BENCHMARK] {monResult.ScenarioName}:");
        _output.WriteLine($"   Working Set Rata-rata : {monResult.AvgWorkingSetMb:0.0} MB");
        _output.WriteLine($"   Working Set Puncak     : {monResult.PeakWorkingSetMb:0.0} MB");
        _output.WriteLine($"   CPU Rata-rata          : {monResult.AvgCpuPercentage:0.00}%");
        _output.WriteLine($"   GC Gen0/1/2            : {monResult.Gen0Collections}/{monResult.Gen1Collections}/{monResult.Gen2Collections}");

        Assert.True(idleResult.AvgWorkingSetMb > 0);
        Assert.True(animResult.AvgWorkingSetMb > 0);
        Assert.True(monResult.AvgWorkingSetMb > 0);
    }
}
