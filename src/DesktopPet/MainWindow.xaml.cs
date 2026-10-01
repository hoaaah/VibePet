using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

namespace DesktopPet;

public partial class MainWindow : Window
{
    private readonly SpriteSheetManager _sheetManager;
    private readonly SpritePlayer _player;
    private readonly SettingsService _settingsService;
    private readonly PetStateMachine _stateMachine;
    private readonly ActivityDetector _activityDetector;
    private readonly GazeTracker _gazeTracker;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly NamedPipeIpcServer _ipcServer;
    private readonly ProcessWatcherService _processWatcher;
    private readonly AutoStartService _autoStartService;
    private readonly DispatcherTimer _bubbleDismissTimer;
    private string? _currentBubbleActionCommand;

    private PetMovementManager? _movementManager;
    private ControlWindow? _controlWindow;
    private TrayIconManager? _trayManager;
    private MenuItem? _itemWorkPacing;

    private double _currentScale = 1.0;
    private bool _isDragging = false;
    private readonly DragMotionTracker _dragTracker = new();
    private readonly DispatcherTimer _dragStillnessTimer;
    private double _lastDragLeft;
    private double _lastDragTop;

    public MainWindow()
    {
        InitializeComponent();

        _sheetManager = SpriteSheetManager.Instance;
        _sheetManager.Load();

        _settingsService = SettingsService.Instance;
        _settingsService.Load();

        _player = new SpritePlayer(_sheetManager);
        _player.FrameUpdated += OnFrameUpdated;

        _stateMachine = new PetStateMachine(_player);
        _activityDetector = new ActivityDetector();
        _gazeTracker = new GazeTracker();
        _resourceMonitor = new ResourceMonitor(1000);
        _ipcServer = new NamedPipeIpcServer();
        _processWatcher = new ProcessWatcherService(_settingsService.Settings.WatchedProcesses);
        _autoStartService = new AutoStartService();

        _bubbleDismissTimer = new DispatcherTimer(DispatcherPriority.Background);
        _bubbleDismissTimer.Tick += (s, e) => HideSpeechBubble();

        _dragStillnessTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _dragStillnessTimer.Tick += (s, e) => ApplyDragPose(_dragTracker.Tick(DragClock));

        BuildContextMenu();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Win32 tool window styles (hide from Alt-Tab)
        var helper = new WindowInteropHelper(this);
        int exStyle = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);

        // Apply scale & position
        SetScale(_settingsService.Settings.Scale);
        System.Windows.Point pos = _settingsService.GetValidatedPosition(Width, Height);
        Left = pos.X;
        Top = pos.Y;

        // Initialize Movement Manager
        _movementManager = new PetMovementManager(this, _stateMachine)
        {
            IsEnabled = _settingsService.Settings.AutoWander,
            ReducedMotion = _settingsService.Settings.ReducedMotion,
            WorkStyle = _settingsService.Settings.WorkAnimationStyle
        };

        // Wire Activity Detector (Typing & Cursor tracking)
        _activityDetector.IsEnabled = _settingsService.Settings.TypingDetection;
        _activityDetector.TypingStarted += OnTypingStarted;
        _activityDetector.TypingStopped += OnTypingStopped;
        _activityDetector.CursorMoved += OnCursorMoved;
        _activityDetector.Start();

        // Wire Resource Monitor (CPU & RAM sampling)
        _resourceMonitor.IsEnabled = _settingsService.Settings.ResourceMonitoring;
        _resourceMonitor.CpuHighThreshold = _settingsService.Settings.CpuHighThreshold;
        _resourceMonitor.CpuLowThreshold = _settingsService.Settings.CpuLowThreshold;
        _resourceMonitor.RamHighThreshold = _settingsService.Settings.RamHighThreshold;
        _resourceMonitor.RamLowThreshold = _settingsService.Settings.RamLowThreshold;
        _resourceMonitor.MetricsUpdated += OnResourceMetricsUpdated;
        _resourceMonitor.Start();

        // Wire IPC Named Pipe Server
        _ipcServer.MessageReceived += OnIpcMessageReceived;
        if (_settingsService.Settings.EnableIpc)
        {
            _ipcServer.Start();
        }

        // Wire Process Watcher
        _processWatcher.IsEnabled = _settingsService.Settings.EnableProcessWatcher;
        _processWatcher.ProcessesStarted += OnWatchedProcessesStarted;
        _processWatcher.ProcessExited += OnWatchedProcessExited;
        _processWatcher.WorkActivityChanged += active => _stateMachine.SetWorkSource(WorkSource.Process, active);
        _processWatcher.Start();

        // Initialize ControlWindow and TrayManager
        _controlWindow = new ControlWindow(this, _player, _stateMachine, _movementManager, _activityDetector, _resourceMonitor, _ipcServer, _processWatcher, _autoStartService);
        _trayManager = new TrayIconManager(this, _controlWindow);
        _processWatcher.ActiveProcessesChanged += processes => _trayManager?.UpdateActiveProcesses(processes);

        _movementManager.Start();

        // Wire Multi-Monitor, Power Management, and DPI Events (Tahap 5)
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        DpiChanged += OnWindowDpiChanged;

        // Start default state evaluation
        _stateMachine.EvaluateState();
    }

    private void OnFrameUpdated(System.Windows.Media.Imaging.BitmapSource frame)
    {
        Dispatcher.InvokeAsync(() =>
        {
            PetImage.Source = frame;
        });
    }

    private void OnTypingStarted()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_settingsService.Settings.TypingDetection)
            {
                _movementManager?.CancelMovement();
                _stateMachine.SetUserTyping(true);
            }
        });
    }

    private void OnTypingStopped()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _stateMachine.SetUserTyping(false);
        });
    }

    private void OnCursorMoved(int cursorX, int cursorY)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (!_settingsService.Settings.GazeTracking) return;

            // Only track gaze when idle or already in gaze state
            if (_stateMachine.CurrentPriority <= PetPriority.Gaze)
            {
                double centerX = Left + Width / 2;
                double centerY = Top + Height / 2;

                var gaze = _gazeTracker.CalculateGaze(centerX, centerY, cursorX, cursorY);
                _stateMachine.SetGaze(gaze);
            }
        });
    }

    private void OnResourceMetricsUpdated(ResourceMetrics metrics)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (!_settingsService.Settings.ShowResourceBadges)
            {
                SweatCanvas.Visibility = Visibility.Collapsed;
                CpuBadge.Visibility = Visibility.Collapsed;
                RamBadge.Visibility = Visibility.Collapsed;
                _stateMachine.SetWorkSource(WorkSource.CpuLoad, false);
                return;
            }

            // CPU high load -> sweat drop + CPU badge + trigger state machine ComputerWork
            if (metrics.IsCpuHigh)
            {
                SweatCanvas.Visibility = Visibility.Visible;
                CpuBadge.Visibility = Visibility.Visible;
                TxtCpuBadge.Text = $"🔥 CPU {metrics.CpuUsagePercentage:0}%";
                _stateMachine.SetWorkSource(WorkSource.CpuLoad, true);
            }
            else
            {
                SweatCanvas.Visibility = Visibility.Collapsed;
                CpuBadge.Visibility = Visibility.Collapsed;
                // Only clears the CPU source; IPC/process work keeps the pet busy (AGENTS.md rule 5)
                _stateMachine.SetWorkSource(WorkSource.CpuLoad, false);
            }

            // RAM high load -> RAM badge
            if (metrics.IsRamHigh)
            {
                RamBadge.Visibility = Visibility.Visible;
                TxtRamBadge.Text = $"⚡ RAM {metrics.RamUsagePercentage:0}% ({metrics.RamUsedGb:0.0} GB)";
            }
            else
            {
                RamBadge.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnIpcMessageReceived(PetEventMessage msg)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string evt = msg.Event.ToLowerInvariant().Trim();
            switch (evt)
            {
                case "start":
                case "work_started":
                    _stateMachine.SetWorkSource(WorkSource.Ipc, true);
                    ShowSpeechBubble(
                        msg.Title ?? "Pekerjaan Dimulai",
                        msg.Message ?? "Proses sedang berjalan...",
                        msg.ActionLabel,
                        msg.ActionCommand,
                        msg.TimeoutSeconds ?? 5,
                        "work"
                    );
                    break;

                case "success":
                case "work_completed":
                    _stateMachine.SetWorkSource(WorkSource.Ipc, false);
                    _stateMachine.TriggerJobSuccess();
                    ShowSpeechBubble(
                        msg.Title ?? "Pekerjaan Selesai",
                        msg.Message ?? "Tugas berhasil diselesaikan dengan sukses!",
                        msg.ActionLabel,
                        msg.ActionCommand,
                        msg.TimeoutSeconds ?? 6,
                        "success"
                    );
                    break;

                case "error":
                case "work_failed":
                    _stateMachine.SetWorkSource(WorkSource.Ipc, false);
                    _stateMachine.SetError(true);
                    ShowSpeechBubble(
                        msg.Title ?? "Terjadi Error",
                        msg.Message ?? "Tugas atau proses mengalami kegagalan.",
                        msg.ActionLabel ?? "Tutup Error",
                        msg.ActionCommand,
                        msg.TimeoutSeconds ?? 10,
                        "error"
                    );
                    break;

                case "needs_action":
                case "waiting":
                case "prompt":
                    _stateMachine.SetNeedsAction(true);
                    ShowSpeechBubble(
                        msg.Title ?? "Butuh Tindakan",
                        msg.Message ?? "Menunggu respons atau persetujuan Anda.",
                        msg.ActionLabel ?? "Tindak Lanjuti",
                        msg.ActionCommand,
                        msg.TimeoutSeconds ?? 0, // 0 = persistent
                        "action"
                    );
                    break;

                case "notify":
                case "waving":
                    _stateMachine.TriggerNotification();
                    ShowSpeechBubble(
                        msg.Title ?? "Notifikasi",
                        msg.Message ?? "Ada informasi baru untuk Anda.",
                        msg.ActionLabel,
                        msg.ActionCommand,
                        msg.TimeoutSeconds ?? 5,
                        "notify"
                    );
                    break;

                case "clear":
                    _stateMachine.ClearAllSimulations();
                    HideSpeechBubble();
                    break;
            }
        });
    }

    private void OnWatchedProcessesStarted(IReadOnlyList<ProcessIdentity> identities, bool isInitialScan)
    {
        // Bubble only: whether the computer is actually working comes from WorkActivityChanged (CPU use of watched tools)
        Dispatcher.InvokeAsync(() =>
        {
            ShowSpeechBubble(
                isInitialScan ? "Proses Terdeteksi" : "Proses Dimulai",
                ProcessNameFormatter.FormatStartedSummary(identities),
                timeoutSeconds: 4,
                type: "work"
            );
        });
    }

    private void OnWatchedProcessExited(ProcessIdentity identity, int pid, int exitCode)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (exitCode == 0)
            {
                _stateMachine.TriggerJobSuccess();
                ShowSpeechBubble(
                    "Proses Selesai",
                    $"{identity.DisplayName} selesai dengan sukses (exit code: 0)!",
                    timeoutSeconds: 5,
                    type: "success"
                );
            }
            else
            {
                _stateMachine.SetError(true);
                ShowSpeechBubble(
                    "Proses Gagal",
                    $"{identity.DisplayName} keluar dengan error (exit code: {exitCode}).",
                    actionLabel: "Tutup",
                    timeoutSeconds: 8,
                    type: "error"
                );
            }
        });
    }

    public void ShowSpeechBubble(
        string title,
        string message,
        string? actionLabel = null,
        string? actionCommand = null,
        int timeoutSeconds = 6,
        string type = "notify")
    {
        _bubbleDismissTimer.Stop();

        TxtBubbleTitle.Text = title;
        TxtBubbleMessage.Text = message;
        _currentBubbleActionCommand = actionCommand;

        // Apply type-specific colors
        var accentBrush = type switch
        {
            "error" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0x8B, 0xA8)), // red
            "action" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0xB3, 0x87)), // peach
            "success" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA6, 0xE3, 0xA1)), // green
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x89, 0xB4, 0xFA)) // blue
        };

        BubbleBorder.BorderBrush = accentBrush;
        BubbleTail.Stroke = accentBrush;
        TxtBubbleTitle.Foreground = accentBrush;

        if (!string.IsNullOrWhiteSpace(actionLabel))
        {
            BtnBubbleAction.Content = actionLabel;
            BtnBubbleAction.Foreground = accentBrush;
            BtnBubbleAction.Visibility = Visibility.Visible;
        }
        else
        {
            BtnBubbleAction.Visibility = Visibility.Collapsed;
        }

        BubbleGrid.Visibility = Visibility.Visible;

        if (timeoutSeconds > 0)
        {
            _bubbleDismissTimer.Interval = TimeSpan.FromSeconds(timeoutSeconds);
            _bubbleDismissTimer.Start();
        }
    }

    public void HideSpeechBubble()
    {
        _bubbleDismissTimer.Stop();
        BubbleGrid.Visibility = Visibility.Collapsed;
    }

    private void BubbleGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _bubbleDismissTimer.Stop();
    }

    private void BubbleGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _bubbleDismissTimer.Interval = TimeSpan.FromSeconds(3);
        _bubbleDismissTimer.Start();
    }

    private void BtnCloseBubble_Click(object sender, MouseButtonEventArgs e)
    {
        HideSpeechBubble();
        if (_stateMachine.HasError) _stateMachine.SetError(false);
        if (_stateMachine.HasNeedsAction) _stateMachine.SetNeedsAction(false);
    }

    private void BtnBubbleAction_Click(object sender, RoutedEventArgs e)
    {
        HideSpeechBubble();
        if (_stateMachine.HasError) _stateMachine.SetError(false);
        if (_stateMachine.HasNeedsAction) _stateMachine.SetNeedsAction(false);

        if (!string.IsNullOrWhiteSpace(_currentBubbleActionCommand))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _currentBubbleActionCommand,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore shell execute errors
            }
        }
    }

    public void SetScale(double scale)
    {
        _currentScale = Math.Clamp(scale, 0.5, 3.0);
        double w = AnimationCatalog.CellWidth * _currentScale;
        double h = AnimationCatalog.CellHeight * _currentScale;

        PetImage.Width = w;
        PetImage.Height = h;
        SweatCanvas.Width = w;
        SweatCanvas.Height = h;
        Width = double.NaN;
        Height = double.NaN;
        SizeToContent = SizeToContent.WidthAndHeight;

        _settingsService.Settings.Scale = _currentScale;
        _settingsService.Save();
    }

    public void SetReducedMotion(bool val)
    {
        _settingsService.Settings.ReducedMotion = val;
        if (_movementManager != null)
        {
            _movementManager.ReducedMotion = val;
            if (val) _movementManager.CancelMovement();
        }
        _settingsService.Save();
    }

    public void SetWorkAnimationStyle(WorkAnimationStyle style)
    {
        _settingsService.Settings.WorkAnimationStyle = style;
        if (_movementManager != null)
        {
            _movementManager.WorkStyle = style;
        }
        _settingsService.Save();
        _controlWindow?.SyncWorkAnimationStyle(style);
        if (_itemWorkPacing != null) _itemWorkPacing.IsChecked = style == WorkAnimationStyle.Pacing;
    }

    public void SetAutoWander(bool val)
    {
        _settingsService.Settings.AutoWander = val;
        if (_movementManager != null)
        {
            _movementManager.IsEnabled = val;
            if (!val) _movementManager.CancelMovement();
        }
        _settingsService.Save();
    }

    public void SetGazeTracking(bool val)
    {
        _settingsService.Settings.GazeTracking = val;
        if (!val)
        {
            _stateMachine.SetGaze(null);
        }
        _settingsService.Save();
    }

    public void SetTypingDetection(bool val)
    {
        _settingsService.Settings.TypingDetection = val;
        _activityDetector.IsEnabled = val;
        if (!val)
        {
            _stateMachine.SetUserTyping(false);
        }
        _settingsService.Save();
    }

    public void ResetPosition()
    {
        _movementManager?.CancelMovement();
        double defaultX = SystemParameters.WorkArea.Right - Width - 20;
        double defaultY = SystemParameters.WorkArea.Bottom - Height - 20;
        Left = defaultX;
        Top = defaultY;

        _settingsService.Settings.X = Left;
        _settingsService.Settings.Y = Top;
        _settingsService.Save();
    }

    private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _movementManager?.CancelMovement();
            _isDragging = true;
            _stateMachine.SetDirectInteraction(true);

            // Hold current pose until the cursor actually moves
            _player.Pause();

            _dragTracker.Begin(DragClock);
            _lastDragLeft = Left;
            _lastDragTop = Top;
            LocationChanged += OnDragLocationChanged;
            _dragStillnessTimer.Start();

            try
            {
                // Windows' modal move loop; LocationChanged and timers keep firing while it runs
                DragMove();
            }
            catch
            {
                // DragMove can throw if mouse button was already released
            }
            finally
            {
                LocationChanged -= OnDragLocationChanged;
                _dragStillnessTimer.Stop();
            }

            _isDragging = false;

            // Save new position
            _settingsService.Settings.X = Left;
            _settingsService.Settings.Y = Top;
            _settingsService.Save();

            // Play jumping response after release (as designed in AGENTS.md)
            _player.PlayAnimation(PetAnimationState.Jumping, () =>
            {
                _stateMachine.SetDirectInteraction(false);
            });
        }
    }

    private static TimeSpan DragClock => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private void OnDragLocationChanged(object? sender, EventArgs e)
    {
        double deltaX = Left - _lastDragLeft;
        double deltaY = Top - _lastDragTop;
        _lastDragLeft = Left;
        _lastDragTop = Top;

        if (!_isDragging) return;
        ApplyDragPose(_dragTracker.OnMoved(deltaX, deltaY, DragClock));
    }

    private void ApplyDragPose(DragPoseAction action)
    {
        // Reduced motion: keep the single held pose for the whole drag
        if (_settingsService.Settings.ReducedMotion) return;

        switch (action)
        {
            case DragPoseAction.PlayRunning when _dragTracker.Direction is PetAnimationState direction:
                _player.PlayAnimation(direction);
                break;
            case DragPoseAction.ResumeRunning:
                _player.Resume();
                break;
            case DragPoseAction.Hold:
                _player.Pause();
                break;
        }
    }

    private void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
        }
    }

    private void BuildContextMenu()
    {
        // 1. Animations
        foreach (var (state, def) in AnimationCatalog.Animations)
        {
            var item = new MenuItem { Header = $"{def.DisplayName} (Baris {def.RowIndex})" };
            var targetState = state;
            item.Click += (s, e) =>
            {
                _movementManager?.CancelMovement();
                _stateMachine.SetManualAnimation(targetState);
            };
            MenuAnimations.Items.Add(item);
        }

        var itemResume = new MenuItem { Header = "▶️ Kembali ke Mode Otomatis", FontWeight = FontWeights.Bold };
        itemResume.Click += (s, e) => _stateMachine.ClearManualTestMode();
        MenuAnimations.Items.Add(new Separator());
        MenuAnimations.Items.Add(itemResume);

        // 2. Gaze directions
        foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
        {
            var (row, col) = AnimationCatalog.GetGazeCell(gaze);
            string name = AnimationCatalog.GetGazeDisplayName(gaze);
            var item = new MenuItem { Header = $"{name} (Baris {row}, Kolom {col})" };
            var targetGaze = gaze;
            item.Click += (s, e) =>
            {
                _movementManager?.CancelMovement();
                _stateMachine.SetManualGaze(targetGaze);
            };
            MenuGaze.Items.Add(item);
        }

        // 3. Scales
        double[] scales = [0.75, 1.0, 1.25, 1.5, 2.0];
        foreach (double sc in scales)
        {
            var item = new MenuItem { Header = $"{sc:0.00}x" };
            double targetScale = sc;
            item.Click += (s, e) => SetScale(targetScale);
            MenuScale.Items.Add(item);
        }

        // 4. Behavioral Toggles
        var settings = _settingsService.Settings;

        var itemReducedMotion = new MenuItem
        {
            Header = "Reduced Motion",
            IsCheckable = true,
            IsChecked = settings.ReducedMotion
        };
        itemReducedMotion.Click += (s, e) => SetReducedMotion(itemReducedMotion.IsChecked);
        MenuBehavior.Items.Add(itemReducedMotion);

        var itemAutoWander = new MenuItem
        {
            Header = "Auto Wander (Jalan Otomatis)",
            IsCheckable = true,
            IsChecked = settings.AutoWander
        };
        itemAutoWander.Click += (s, e) => SetAutoWander(itemAutoWander.IsChecked);
        MenuBehavior.Items.Add(itemAutoWander);

        _itemWorkPacing = new MenuItem
        {
            Header = "Kerja: Berlari Bolak-balik",
            IsCheckable = true,
            IsChecked = settings.WorkAnimationStyle == WorkAnimationStyle.Pacing
        };
        _itemWorkPacing.Click += (s, e) => SetWorkAnimationStyle(
            _itemWorkPacing.IsChecked ? WorkAnimationStyle.Pacing : WorkAnimationStyle.Static);
        MenuBehavior.Items.Add(_itemWorkPacing);

        var itemGaze = new MenuItem
        {
            Header = "Gaze Tracking (Ikuti Mouse)",
            IsCheckable = true,
            IsChecked = settings.GazeTracking
        };
        itemGaze.Click += (s, e) => SetGazeTracking(itemGaze.IsChecked);
        MenuBehavior.Items.Add(itemGaze);

        var itemTyping = new MenuItem
        {
            Header = "Deteksi Mengetik (Typing Review)",
            IsCheckable = true,
            IsChecked = settings.TypingDetection
        };
        itemTyping.Click += (s, e) => SetTypingDetection(itemTyping.IsChecked);
        MenuBehavior.Items.Add(itemTyping);

        var itemResource = new MenuItem
        {
            Header = "Resource Monitoring (CPU & RAM)",
            IsCheckable = true,
            IsChecked = settings.ResourceMonitoring
        };
        itemResource.Click += (s, e) =>
        {
            settings.ResourceMonitoring = itemResource.IsChecked;
            _resourceMonitor.IsEnabled = itemResource.IsChecked;
            _settingsService.Save();
        };
        MenuBehavior.Items.Add(itemResource);

        var itemBadges = new MenuItem
        {
            Header = "Tampilkan Badge Beban (Keringat)",
            IsCheckable = true,
            IsChecked = settings.ShowResourceBadges
        };
        itemBadges.Click += (s, e) =>
        {
            settings.ShowResourceBadges = itemBadges.IsChecked;
            if (!settings.ShowResourceBadges)
            {
                SweatCanvas.Visibility = Visibility.Collapsed;
                CpuBadge.Visibility = Visibility.Collapsed;
                RamBadge.Visibility = Visibility.Collapsed;
            }
            _settingsService.Save();
        };
        var itemAutoStart = new MenuItem
        {
            Header = "Mulai Otomatis bersama Windows",
            IsCheckable = true,
            IsChecked = _autoStartService.IsAutoStartEnabled()
        };
        itemAutoStart.Click += (s, e) => SetStartWithWindows(itemAutoStart.IsChecked);
        MenuBehavior.Items.Add(itemAutoStart);
    }

    public void SetStartWithWindows(bool val)
    {
        _settingsService.Settings.StartWithWindows = val;
        _autoStartService.SetAutoStart(val);
        _settingsService.Save();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(ValidateAndReposition);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    // Stop movement first: ending pacing re-evaluates the state and would restart the player
                    _movementManager?.Stop();
                    _resourceMonitor.Stop();
                    _processWatcher.Stop();
                    _activityDetector.Stop();
                    _player.Pause();
                    break;

                case PowerModes.Resume:
                    _player.Resume();
                    if (_settingsService.Settings.ResourceMonitoring) _resourceMonitor.Start();
                    if (_settingsService.Settings.EnableProcessWatcher) _processWatcher.Start();
                    if (_settingsService.Settings.TypingDetection) _activityDetector.Start();
                    if (_settingsService.Settings.AutoWander) _movementManager?.Start();
                    _stateMachine.EvaluateState();
                    ValidateAndReposition();
                    _movementManager?.SyncPacing();
                    break;
            }
        });
    }

    private void OnWindowDpiChanged(object sender, System.Windows.DpiChangedEventArgs e)
    {
        ValidateAndReposition();
    }

    public void ValidateAndReposition()
    {
        double targetW = ActualWidth > 0 ? ActualWidth : Width;
        double targetH = ActualHeight > 0 ? ActualHeight : Height;
        System.Windows.Point pos = _settingsService.GetValidatedPosition(targetW, targetH);
        Left = pos.X;
        Top = pos.Y;
        _settingsService.Settings.X = Left;
        _settingsService.Settings.Y = Top;
        _settingsService.Save();
    }

    private void MenuOpenControl_Click(object sender, RoutedEventArgs e)
    {
        if (_controlWindow != null)
        {
            _controlWindow.Show();
            _controlWindow.Activate();
        }
    }

    private void MenuResetPos_Click(object sender, RoutedEventArgs e)
    {
        ResetPosition();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        DpiChanged -= OnWindowDpiChanged;

        _ipcServer.Dispose();
        _processWatcher.Dispose();
        _resourceMonitor.Dispose();
        _activityDetector.Dispose();
        _movementManager?.Dispose();

        _settingsService.Settings.X = Left;
        _settingsService.Settings.Y = Top;
        _settingsService.Save();

        _trayManager?.Dispose();
    }
}