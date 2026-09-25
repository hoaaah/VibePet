using System.Diagnostics;
using DesktopPet.Models;

namespace DesktopPet.Services;

public record BenchmarkResult(
    string ScenarioName,
    int DurationSeconds,
    double AvgWorkingSetMb,
    double PeakWorkingSetMb,
    double AvgCpuPercentage,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections
);

public class BenchmarkService
{
    public static async Task<BenchmarkResult> RunScenarioAsync(
        string scenarioName,
        int durationSeconds,
        Action setup,
        Action cleanup)
    {
        setup();

        var proc = Process.GetCurrentProcess();
        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);

        var wsSamples = new List<double>();
        var cpuSamples = new List<double>();

        var sw = Stopwatch.StartNew();
        var prevTime = proc.TotalProcessorTime;
        var prevTimestamp = DateTime.UtcNow;

        while (sw.Elapsed.TotalSeconds < durationSeconds)
        {
            await Task.Delay(250);

            proc.Refresh();
            double wsMb = proc.WorkingSet64 / (1024.0 * 1024.0);
            wsSamples.Add(wsMb);

            var now = DateTime.UtcNow;
            var curTime = proc.TotalProcessorTime;
            double elapsedSec = (now - prevTimestamp).TotalSeconds;

            if (elapsedSec > 0)
            {
                double cpuSec = (curTime - prevTime).TotalSeconds;
                double cpuPercent = Math.Clamp((cpuSec / (elapsedSec * Environment.ProcessorCount)) * 100.0, 0.0, 100.0);
                cpuSamples.Add(cpuPercent);
            }

            prevTime = curTime;
            prevTimestamp = now;
        }

        cleanup();

        return new BenchmarkResult(
            scenarioName,
            durationSeconds,
            wsSamples.Count > 0 ? Math.Round(wsSamples.Average(), 2) : 0,
            wsSamples.Count > 0 ? Math.Round(wsSamples.Max(), 2) : 0,
            cpuSamples.Count > 0 ? Math.Round(cpuSamples.Average(), 3) : 0,
            GC.CollectionCount(0) - initialGen0,
            GC.CollectionCount(1) - initialGen1,
            GC.CollectionCount(2) - initialGen2
        );
    }
}
