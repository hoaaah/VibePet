using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DesktopPet.Services;

public record ResourceMetrics(
    double CpuUsagePercentage,
    double RamUsagePercentage,
    double RamUsedGb,
    double RamTotalGb,
    double AppWorkingSetMb,
    double AppCpuPercentage,
    bool IsCpuHigh,
    bool IsRamHigh
);

public class ResourceMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Process _currentProcess;

    private NativeMethods.FILETIME _prevSysIdle;
    private NativeMethods.FILETIME _prevSysKernel;
    private NativeMethods.FILETIME _prevSysUser;
    private TimeSpan _prevAppCpuTime;
    private DateTime _prevSampleTime = DateTime.UtcNow;

    private int _cpuHighCounter = 0;
    private int _ramHighCounter = 0;

    public bool IsEnabled { get; set; } = true;
    public double CpuHighThreshold { get; set; } = 80.0;
    public double CpuLowThreshold { get; set; } = 60.0;
    public int CpuSustainSamples { get; set; } = 3;

    public double RamHighThreshold { get; set; } = 85.0;
    public double RamLowThreshold { get; set; } = 75.0;
    public int RamSustainSamples { get; set; } = 3;

    public bool IsCpuSustainedHigh { get; private set; } = false;
    public bool IsRamSustainedHigh { get; private set; } = false;

    // Simulation overrides
    public double? SimulatedCpuUsage { get; set; }
    public double? SimulatedRamUsage { get; set; }

    public ResourceMetrics LatestMetrics { get; private set; } = new(0, 0, 0, 0, 0, 0, false, false);

    public event Action<ResourceMetrics>? MetricsUpdated;
    public event Action<bool, bool>? HighLoadChanged;

    public ResourceMonitor(int intervalMs = 1000)
    {
        _currentProcess = Process.GetCurrentProcess();
        _prevAppCpuTime = _currentProcess.TotalProcessorTime;

        NativeMethods.GetSystemTimes(out _prevSysIdle, out _prevSysKernel, out _prevSysUser);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(500, intervalMs))
        };
        _timer.Tick += OnSampleTick;
    }

    public void Start()
    {
        NativeMethods.GetSystemTimes(out _prevSysIdle, out _prevSysKernel, out _prevSysUser);
        _prevAppCpuTime = _currentProcess.TotalProcessorTime;
        _prevSampleTime = DateTime.UtcNow;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public ResourceMetrics SampleNow()
    {
        // 1. System CPU Usage
        double cpuUsage = 0.0;
        if (NativeMethods.GetSystemTimes(out var curIdle, out var curKernel, out var curUser))
        {
            ulong idleDelta = curIdle.ToULong() - _prevSysIdle.ToULong();
            ulong kernelDelta = curKernel.ToULong() - _prevSysKernel.ToULong();
            ulong userDelta = curUser.ToULong() - _prevSysUser.ToULong();
            ulong totalDelta = kernelDelta + userDelta;

            if (totalDelta > 0 && totalDelta >= idleDelta)
            {
                cpuUsage = 100.0 - (idleDelta * 100.0 / totalDelta);
            }

            _prevSysIdle = curIdle;
            _prevSysKernel = curKernel;
            _prevSysUser = curUser;
        }

        // Apply simulation if set
        if (SimulatedCpuUsage.HasValue)
        {
            cpuUsage = SimulatedCpuUsage.Value;
        }
        cpuUsage = Math.Clamp(cpuUsage, 0.0, 100.0);

        // 2. System RAM Usage
        double ramUsage = 0.0;
        double ramUsedGb = 0.0;
        double ramTotalGb = 0.0;

        var mem = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (NativeMethods.GlobalMemoryStatusEx(ref mem))
        {
            ramUsage = mem.dwMemoryLoad;
            ramTotalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            ulong usedBytes = mem.ullTotalPhys > mem.ullAvailPhys ? mem.ullTotalPhys - mem.ullAvailPhys : 0;
            ramUsedGb = usedBytes / (1024.0 * 1024.0 * 1024.0);
        }

        if (SimulatedRamUsage.HasValue)
        {
            ramUsage = SimulatedRamUsage.Value;
        }
        ramUsage = Math.Clamp(ramUsage, 0.0, 100.0);

        // 3. App Footprint Metrics
        _currentProcess.Refresh();
        double appWorkingSetMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);

        DateTime now = DateTime.UtcNow;
        double elapsedSeconds = (now - _prevSampleTime).TotalSeconds;
        _prevSampleTime = now;

        TimeSpan curAppCpu = _currentProcess.TotalProcessorTime;
        double appCpuSeconds = (curAppCpu - _prevAppCpuTime).TotalSeconds;
        _prevAppCpuTime = curAppCpu;

        double appCpuPercentage = 0.0;
        if (elapsedSeconds > 0)
        {
            appCpuPercentage = Math.Clamp((appCpuSeconds / (elapsedSeconds * Environment.ProcessorCount)) * 100.0, 0.0, 100.0);
        }

        // 4. Hysteresis & Debounce Evaluation
        // CPU Hysteresis
        if (cpuUsage >= CpuHighThreshold)
        {
            _cpuHighCounter++;
            if (_cpuHighCounter >= CpuSustainSamples)
            {
                IsCpuSustainedHigh = true;
            }
        }
        else if (cpuUsage <= CpuLowThreshold)
        {
            _cpuHighCounter = 0;
            IsCpuSustainedHigh = false;
        }

        // RAM Hysteresis
        if (ramUsage >= RamHighThreshold)
        {
            _ramHighCounter++;
            if (_ramHighCounter >= RamSustainSamples)
            {
                IsRamSustainedHigh = true;
            }
        }
        else if (ramUsage <= RamLowThreshold)
        {
            _ramHighCounter = 0;
            IsRamSustainedHigh = false;
        }

        var metrics = new ResourceMetrics(
            Math.Round(cpuUsage, 1),
            Math.Round(ramUsage, 1),
            Math.Round(ramUsedGb, 2),
            Math.Round(ramTotalGb, 2),
            Math.Round(appWorkingSetMb, 1),
            Math.Round(appCpuPercentage, 2),
            IsCpuSustainedHigh,
            IsRamSustainedHigh
        );

        LatestMetrics = metrics;
        return metrics;
    }

    private void OnSampleTick(object? sender, EventArgs e)
    {
        if (!IsEnabled) return;

        bool prevCpuHigh = IsCpuSustainedHigh;
        bool prevRamHigh = IsRamSustainedHigh;

        var metrics = SampleNow();
        MetricsUpdated?.Invoke(metrics);

        if (prevCpuHigh != IsCpuSustainedHigh || prevRamHigh != IsRamSustainedHigh)
        {
            HighLoadChanged?.Invoke(IsCpuSustainedHigh, IsRamSustainedHigh);
        }
    }

    public void Dispose()
    {
        Stop();
        _currentProcess.Dispose();
    }
}
