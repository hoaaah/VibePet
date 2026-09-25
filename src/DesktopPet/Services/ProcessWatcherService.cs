using System.Diagnostics;
using System.Windows.Threading;

namespace DesktopPet.Services;

public record WatchedProcessInfo(int Id, string ProcessName, DateTime StartTime);

public class ProcessWatcherService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _targetNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (Process Process, string ProcessName)> _activeProcesses = new();

    public bool IsEnabled { get; set; } = true;
    public IReadOnlyCollection<string> TargetProcessNames => _targetNames;

    public event Action<string, int>? ProcessStarted;
    public event Action<string, int, int>? ProcessExited; // name, pid, exitCode

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
        string clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }
        if (!string.IsNullOrEmpty(clean))
        {
            _targetNames.Add(clean);
        }
    }

    public void RemoveTargetProcess(string processName)
    {
        string clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }
        _targetNames.Remove(clean);
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        CleanupProcesses();
    }

    private void OnScanTick(object? sender, EventArgs e)
    {
        if (!IsEnabled || _targetNames.Count == 0) return;

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

                    ProcessExited?.Invoke(item.ProcessName, pid, exitCode);
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
            }
        }

        // 2. Discover newly started target processes
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
                            _activeProcesses[proc.Id] = (proc, targetName);
                            ProcessStarted?.Invoke(targetName, proc.Id);
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
