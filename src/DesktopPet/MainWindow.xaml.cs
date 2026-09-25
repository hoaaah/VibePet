using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private PetMovementManager? _movementManager;
    private ControlWindow? _controlWindow;
    private TrayIconManager? _trayManager;

    private double _currentScale = 1.0;
    private bool _isDragging = false;

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
            ReducedMotion = _settingsService.Settings.ReducedMotion
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

        // Initialize ControlWindow and TrayManager
        _controlWindow = new ControlWindow(this, _player, _stateMachine, _movementManager, _activityDetector, _resourceMonitor);
        _trayManager = new TrayIconManager(this, _controlWindow);

        _movementManager.Start();

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
                return;
            }

            // CPU high load -> sweat drop + CPU badge + trigger state machine ComputerWork
            if (metrics.IsCpuHigh)
            {
                SweatCanvas.Visibility = Visibility.Visible;
                CpuBadge.Visibility = Visibility.Visible;
                TxtCpuBadge.Text = $"🔥 CPU {metrics.CpuUsagePercentage:0}%";
                _stateMachine.SetComputerWork(true);
            }
            else
            {
                SweatCanvas.Visibility = Visibility.Collapsed;
                CpuBadge.Visibility = Visibility.Collapsed;
                _stateMachine.SetComputerWork(false);
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

    public void SetScale(double scale)
    {
        _currentScale = Math.Clamp(scale, 0.5, 3.0);
        double w = AnimationCatalog.CellWidth * _currentScale;
        double h = AnimationCatalog.CellHeight * _currentScale;

        PetImage.Width = w;
        PetImage.Height = h;
        SweatCanvas.Width = w;
        SweatCanvas.Height = h;
        Width = w;
        Height = h;

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

            // Hold current pose during drag
            _player.Pause();

            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if mouse button was already released
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
        MenuBehavior.Items.Add(itemBadges);
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
        _resourceMonitor.Dispose();
        _activityDetector.Dispose();
        _movementManager?.Dispose();

        _settingsService.Settings.X = Left;
        _settingsService.Settings.Y = Top;
        _settingsService.Save();

        _trayManager?.Dispose();
    }
}