using System.Diagnostics;
using System.Windows.Threading;

namespace DesktopPet.Services;

public class ProcessWatcherService : IDisposable
{
    private sealed class TrackedProcess(Process process, string processName, ProcessLaunchInfo launchInfo)
    {
        public Process Process { get; } = process;
        public string ProcessName { get; } = processName;
        public ProcessLaunchInfo LaunchInfo { get; } = launchInfo;
        public string? CommandLineContext => LaunchInfo.CommandLineContext;
        public ProcessIdentity Identity { get; set; } = new(ProcessNameFormatter.GetFriendlyAppName(processName), launchInfo.CommandLineContext);

        /// <summary>
        /// Proses pembantu (renderer/GPU Electron, worker MSBuild, dsb.) tidak memicu notifikasi sendiri.
        /// </summary>
        public bool IsHelper { get; set; }

        public bool CountsAsWork { get; } = ProcessNameFormatter.CountsAsWork(processName);
        public TimeSpan? LastCpuTime { get; set; }
        public TimeSpan? LastBusyAt { get; set; }
    }

    /// <summary>Fraksi satu core CPU yang dianggap "sedang bekerja".</summary>
    public const double BusyCpuFraction = 0.05;
    /// <summary>Lama status kerja dipertahankan setelah proses terakhir kali sibuk (anti-kedip).</summary>
    public static readonly TimeSpan WorkLinger = TimeSpan.FromSeconds(4);

    private TimeSpan? _lastScanAt;

    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _targetNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, TrackedProcess> _activeProcesses = new();
    private bool _isInitialScan = true;

    public bool IsEnabled { get; set; } = true;
    public IReadOnlyCollection<string> TargetProcessNames => _targetNames;

    /// <summary>
    /// Identitas aplikasi utama yang sedang berjalan (tanpa proses pembantu), unik per tampilan.
    /// </summary>
    public IReadOnlyList<ProcessIdentity> ActiveProcesses => _activeProcesses.Values
        .Where(p => !p.IsHelper)
        .Select(p => p.Identity)
        .Distinct()
        .ToList();

    /// <summary>
    /// Aplikasi baru yang terdeteksi dalam satu scan, digabung per identitas.
    /// isInitialScan = true untuk proses yang sudah berjalan saat watcher mulai.
    /// </summary>
    public event Action<IReadOnlyList<ProcessIdentity>, bool>? ProcessesStarted;
    public event Action<ProcessIdentity, int, int>? ProcessExited; // identity, pid, exitCode
    public event Action<IReadOnlyList<ProcessIdentity>>? ActiveProcessesChanged;

    /// <summary>
    /// True selama ada tool CLI/build/agent yang dipantau (termasuk proses pembantunya) sedang memakai CPU.
    /// Aplikasi GUI tidak dihitung. Proses yang hidup tetapi diam tidak dianggap bekerja.
    /// </summary>
    public bool IsWorkActive { get; private set; }
    public event Action<bool>? WorkActivityChanged;

    public ProcessWatcherService(IEnumerable<string>? initialProcesses = null)
    {
        if (initialProcesses != null)
        {
            foreach (var p in initialProcesses)
            {
                AddTargetProcess(p);
            }
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _timer.Tick += OnScanTick;
    }

    public void AddTargetProcess(string processName)
    {
        string clean = ProcessNameFormatter.NormalizeProcessName(processName);
        if (!string.IsNullOrEmpty(clean))
        {
            _targetNames.Add(clean);
        }
    }

    public void RemoveTargetProcess(string processName)
    {
        _targetNames.Remove(ProcessNameFormatter.NormalizeProcessName(processName));
    }

    public void Start()
    {
        _isInitialScan = true;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        bool hadProcesses = _activeProcesses.Count > 0;
        CleanupProcesses();
        _lastScanAt = null;
        if (hadProcesses) ActiveProcessesChanged?.Invoke(ActiveProcesses);
        SetWorkActive(false);
    }

    public static bool IsCpuBusy(TimeSpan cpuDelta, TimeSpan wallDelta) =>
        wallDelta > TimeSpan.Zero && cpuDelta.TotalMilliseconds / wallDelta.TotalMilliseconds >= BusyCpuFraction;

    public static bool IsWithinLinger(TimeSpan? lastBusyAt, TimeSpan now) =>
        lastBusyAt is TimeSpan busy && now - busy <= WorkLinger;

    private void SetWorkActive(bool active)
    {
        if (IsWorkActive == active) return;
        IsWorkActive = active;
        WorkActivityChanged?.Invoke(active);
    }

    private void UpdateWorkActivity()
    {
        var now = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var wallDelta = _lastScanAt is TimeSpan last ? now - last : TimeSpan.Zero;
        _lastScanAt = now;

        bool anyWorking = false;
        foreach (var item in _activeProcesses.Values)
        {
            if (!item.CountsAsWork) continue;

            try
            {
                var cpu = item.Process.TotalProcessorTime;
                if (item.LastCpuTime is TimeSpan previous && IsCpuBusy(cpu - previous, wallDelta))
                {
                    item.LastBusyAt = now;
                }
                item.LastCpuTime = cpu;
            }
            catch
            {
                // Access denied (elevated process) or already exited: treat as idle
            }

            anyWorking |= IsWithinLinger(item.LastBusyAt, now);
        }

        SetWorkActive(anyWorking);
    }

    private void OnScanTick(object? sender, EventArgs e)
    {
        if (!IsEnabled || _targetNames.Count == 0)
        {
            SetWorkActive(false);
            return;
        }

        bool changed = false;

        // 1. Check existing tracked processes for exit
        var exitedPids = new List<int>();
        foreach (var (pid, item) in _activeProcesses)
        {
            try
            {
                if (item.Process.HasExited)
                {
                    int exitCode = 0;
                    try
                    {
                        exitCode = item.Process.ExitCode;
                    }
                    catch
                    {
                        // ExitCode may throw if process was terminated abruptly or insufficient rights
                    }

                    // Helper exit codes are noise (Electron renderers often exit non-zero on close).
                    if (!item.IsHelper)
                    {
                        ProcessExited?.Invoke(item.Identity, pid, exitCode);
                    }
                    exitedPids.Add(pid);
                }
            }
            catch
            {
                exitedPids.Add(pid);
            }
        }

        foreach (var pid in exitedPids)
        {
            if (_activeProcesses.TryGetValue(pid, out var item))
            {
                item.Process.Dispose();
                _activeProcesses.Remove(pid);
                changed = true;
            }
        }

        // 2. Discover newly started target processes
        var newPids = new List<int>();
        foreach (var targetName in _targetNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(targetName);
                foreach (var proc in processes)
                {
                    if (!_activeProcesses.ContainsKey(proc.Id))
                    {
                        try
                        {
                            var launchInfo = ProcessIdentityResolver.ReadLaunchInfo(proc.Id);
                            _activeProcesses[proc.Id] = new TrackedProcess(proc, targetName, launchInfo);
                            newPids.Add(proc.Id);
                        }
                        catch
                        {
                            proc.Dispose();
                        }
                    }
                    else
                    {
                        proc.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore process enumeration glitches
            }
        }

        // A process spawned by a same-named watched process (Code -> Code renderer,
        // dotnet build -> dotnet MSBuild node) is a helper of that application.
        foreach (var pid in newPids)
        {
            var item = _activeProcesses[pid];
            item.IsHelper = item.LaunchInfo.IsHelperByCommandLine
                || (item.LaunchInfo.ParentPid is int parent
                    && _activeProcesses.TryGetValue(parent, out var parentItem)
                    && parentItem.ProcessName.Equals(item.ProcessName, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Refresh identities from window titles (one EnumWindows pass per tick).
        //    Titles change as the user switches files/projects, and the last known identity
        //    is reused for the exit notification after the window is gone.
        if (_activeProcesses.Count > 0)
        {
            changed |= RefreshIdentities();
        }

        var started = newPids
            .Select(pid => _activeProcesses[pid])
            .Where(p => !p.IsHelper)
            .Select(p => p.Identity)
            .Distinct()
            .ToList();

        if (started.Count > 0)
        {
            ProcessesStarted?.Invoke(started, _isInitialScan);
        }
        _isInitialScan = false;

        if (changed || newPids.Count > 0)
        {
            ActiveProcessesChanged?.Invoke(ActiveProcesses);
        }

        UpdateWorkActivity();
    }

    private bool RefreshIdentities()
    {
        Dictionary<int, string> titles;
        try
        {
            titles = ProcessIdentityResolver.SnapshotWindowTitles();
        }
        catch
        {
            return false;
        }

        // Processes without a window of their own borrow the title of a same-named process that has one.
        var siblingTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pid, item) in _activeProcesses)
        {
            if (titles.TryGetValue(pid, out var title) && !siblingTitles.ContainsKey(item.ProcessName))
            {
                siblingTitles[item.ProcessName] = title;
            }
        }

        bool changed = false;
        foreach (var (pid, item) in _activeProcesses)
        {
            titles.TryGetValue(pid, out var ownTitle);
            siblingTitles.TryGetValue(item.ProcessName, out var siblingTitle);

            var identity = ProcessNameFormatter.Compose(item.ProcessName, ownTitle, item.CommandLineContext, siblingTitle);

            // Keep the last known context once the window disappears (e.g. while the app is closing).
            if (identity.Context == null && item.Identity.Context != null) continue;

            if (identity != item.Identity)
            {
                item.Identity = identity;
                changed |= !item.IsHelper;
            }
        }
        return changed;
    }

    private void CleanupProcesses()
    {
        foreach (var (_, item) in _activeProcesses)
        {
            try { item.Process.Dispose(); } catch { }
        }
        _activeProcesses.Clear();
    }

    public void Dispose()
    {
        Stop();
    }
}
