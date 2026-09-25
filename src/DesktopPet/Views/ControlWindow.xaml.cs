using System.ComponentModel;
using System.Windows;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Views;

public partial class ControlWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly SpritePlayer _player;
    private readonly PetStateMachine _stateMachine;
    private readonly PetMovementManager _movementManager;
    private readonly ActivityDetector _activityDetector;
    private readonly ResourceMonitor _resourceMonitor;
    private bool _isPaused = false;

    public ControlWindow(
        MainWindow mainWindow,
        SpritePlayer player,
        PetStateMachine stateMachine,
        PetMovementManager movementManager,
        ActivityDetector activityDetector,
        ResourceMonitor resourceMonitor)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _player = player;
        _stateMachine = stateMachine;
        _movementManager = movementManager;
        _activityDetector = activityDetector;
        _resourceMonitor = resourceMonitor;

        _player.FrameUpdated += OnPlayerFrameUpdated;
        _player.StateChanged += OnPlayerStateChanged;
        _stateMachine.StateChanged += OnStateMachineChanged;
        _resourceMonitor.MetricsUpdated += OnResourceMetricsUpdated;

        var settings = SettingsService.Instance.Settings;
        SliderScale.Value = settings.Scale;
        ChkReducedMotion.IsChecked = settings.ReducedMotion;
        ChkAutoWander.IsChecked = settings.AutoWander;
        ChkGazeTracking.IsChecked = settings.GazeTracking;
        ChkTypingDetection.IsChecked = settings.TypingDetection;
        ChkResourceMonitoring.IsChecked = settings.ResourceMonitoring;
        ChkShowBadges.IsChecked = settings.ShowResourceBadges;

        SliderCpuThreshold.Value = settings.CpuHighThreshold;
        TxtCpuThresholdVal.Text = $"{settings.CpuHighThreshold:0}%";

        SliderRamThreshold.Value = settings.RamHighThreshold;
        TxtRamThresholdVal.Text = $"{settings.RamHighThreshold:0}%";

        UpdateStatusText();
    }

    private void OnPlayerFrameUpdated(System.Windows.Media.Imaging.BitmapSource _)
    {
        Dispatcher.InvokeAsync(UpdateStatusText);
    }

    private void OnPlayerStateChanged(PetAnimationState _)
    {
        Dispatcher.InvokeAsync(UpdateStatusText);
    }

    private void OnStateMachineChanged(PetPriority priority, PetAnimationState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string mode = _stateMachine.IsManualTestMode ? "[Manual Lock]" : "[Otomatis]";
            TxtPriority.Text = $"{mode} Prioritas: {priority} -> Animasi: {state}";
            UpdateStatusText();
        });
    }

    private void OnResourceMetricsUpdated(ResourceMetrics metrics)
    {
        Dispatcher.InvokeAsync(() =>
        {
            PbCpu.Value = metrics.CpuUsagePercentage;
            TxtLiveCpu.Text = $"{metrics.CpuUsagePercentage:0.0}%";

            PbRam.Value = metrics.RamUsagePercentage;
            TxtLiveRam.Text = $"{metrics.RamUsagePercentage:0.0}% ({metrics.RamUsedGb:0.0} / {metrics.RamTotalGb:0.0} GB)";

            TxtAppFootprint.Text = $"DesktopPet Overhead: RAM {metrics.AppWorkingSetMb:0.0} MB | CPU {metrics.AppCpuPercentage:0.00}%";
        });
    }

    private void UpdateStatusText()
    {
        if (_player.IsStaticGaze && _player.CurrentGaze.HasValue)
        {
            string gazeName = AnimationCatalog.GetGazeDisplayName(_player.CurrentGaze.Value);
            TxtStatus.Text = $"Status: Pose Arah Pandang [{gazeName}]";
        }
        else
        {
            string stateName = AnimationCatalog.Animations.TryGetValue(_player.CurrentState, out var def)
                ? def.DisplayName
                : _player.CurrentState.ToString();

            TxtStatus.Text = $"Status: {stateName} (Frame {_player.CurrentFrameIndex + 1}/{_player.TotalFrames})";
        }
    }

    // --- Resource Monitor Controls & Simulations ---
    private void SliderCpuThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCpuThresholdVal == null) return;
        double val = Math.Round(e.NewValue);
        TxtCpuThresholdVal.Text = $"{val:0}%";

        var settings = SettingsService.Instance.Settings;
        settings.CpuHighThreshold = val;
        settings.CpuLowThreshold = Math.Max(20, val - 20); // 20% hysteresis band
        _resourceMonitor.CpuHighThreshold = settings.CpuHighThreshold;
        _resourceMonitor.CpuLowThreshold = settings.CpuLowThreshold;
        SettingsService.Instance.Save();
    }

    private void SliderRamThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtRamThresholdVal == null) return;
        double val = Math.Round(e.NewValue);
        TxtRamThresholdVal.Text = $"{val:0}%";

        var settings = SettingsService.Instance.Settings;
        settings.RamHighThreshold = val;
        settings.RamLowThreshold = Math.Max(20, val - 10); // 10% hysteresis band
        _resourceMonitor.RamHighThreshold = settings.RamHighThreshold;
        _resourceMonitor.RamLowThreshold = settings.RamLowThreshold;
        SettingsService.Instance.Save();
    }

    private void BtnSimulateHighCpu_Click(object sender, RoutedEventArgs e)
    {
        _resourceMonitor.SimulatedCpuUsage = 92.0;
    }

    private void BtnSimulateHighRam_Click(object sender, RoutedEventArgs e)
    {
        _resourceMonitor.SimulatedRamUsage = 88.0;
    }

    private void BtnNormalizeResource_Click(object sender, RoutedEventArgs e)
    {
        _resourceMonitor.SimulatedCpuUsage = null;
        _resourceMonitor.SimulatedRamUsage = null;
    }

    private void ChkResourceMonitoring_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkResourceMonitoring.IsChecked == true;
        _resourceMonitor.IsEnabled = val;
        SettingsService.Instance.Settings.ResourceMonitoring = val;
        SettingsService.Instance.Save();
    }

    private void ChkShowBadges_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkShowBadges.IsChecked == true;
        SettingsService.Instance.Settings.ShowResourceBadges = val;
        SettingsService.Instance.Save();
    }

    // --- State Machine & Simulations ---
    private void ChkSimError_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.SetError(ChkSimError.IsChecked == true);
    }

    private void ChkSimWaiting_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.SetNeedsAction(ChkSimWaiting.IsChecked == true);
    }

    private void ChkSimWork_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.SetComputerWork(ChkSimWork.IsChecked == true);
    }

    private void BtnSimNotification_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.TriggerNotification();
    }

    private void BtnSimSuccess_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.TriggerJobSuccess();
    }

    private void BtnResetSimulations_Click(object sender, RoutedEventArgs e)
    {
        ChkSimError.IsChecked = false;
        ChkSimWaiting.IsChecked = false;
        ChkSimWork.IsChecked = false;
        _resourceMonitor.SimulatedCpuUsage = null;
        _resourceMonitor.SimulatedRamUsage = null;
        _stateMachine.ClearAllSimulations();
    }

    // --- Behavioral Settings ---
    private void ChkReducedMotion_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkReducedMotion.IsChecked == true;
        _mainWindow.SetReducedMotion(val);
    }

    private void ChkAutoWander_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkAutoWander.IsChecked == true;
        _mainWindow.SetAutoWander(val);
    }

    private void ChkGazeTracking_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkGazeTracking.IsChecked == true;
        _mainWindow.SetGazeTracking(val);
    }

    private void ChkTypingDetection_Click(object sender, RoutedEventArgs e)
    {
        bool val = ChkTypingDetection.IsChecked == true;
        _mainWindow.SetTypingDetection(val);
    }

    // --- Manual Animations (Locked from Gaze) ---
    private void BtnIdle_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Idle);
    private void BtnRunRight_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.RunningRight);
    private void BtnRunLeft_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.RunningLeft);
    private void BtnWaving_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Waving);
    private void BtnJumping_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Jumping);
    private void BtnFailed_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Failed);
    private void BtnWaiting_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Waiting);
    private void BtnRunWork_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Running);
    private void BtnReview_Click(object sender, RoutedEventArgs e) => _stateMachine.SetManualAnimation(PetAnimationState.Review);

    private void BtnGaze_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tagStr && int.TryParse(tagStr, out int index))
        {
            _stateMachine.SetManualGaze((GazeDirection)index);
        }
    }

    private void BtnResumeAuto_Click(object sender, RoutedEventArgs e)
    {
        _stateMachine.ClearManualTestMode();
    }

    private void SliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtScaleValue == null) return;
        double val = Math.Round(e.NewValue, 2);
        TxtScaleValue.Text = $"{val:0.00}x";
        _mainWindow.SetScale(val);
    }

    private void BtnPauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            _player.Resume();
            BtnPauseResume.Content = "Pause";
            _isPaused = false;
        }
        else
        {
            _player.Pause();
            BtnPauseResume.Content = "Resume";
            _isPaused = true;
        }
    }

    private void BtnMoveRight_Click(object sender, RoutedEventArgs e)
    {
        _movementManager.MoveTo(_mainWindow.Left + 150);
    }

    private void BtnMoveLeft_Click(object sender, RoutedEventArgs e)
    {
        _movementManager.MoveTo(_mainWindow.Left - 150);
    }

    private void BtnResetPos_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.ResetPosition();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
