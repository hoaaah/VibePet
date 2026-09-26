using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Models;
using DesktopPet.Services;
using MediaBrushes = System.Windows.Media.Brushes;

namespace DesktopPet.Views;

public partial class ControlWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly SpritePlayer _player;
    private readonly PetStateMachine _stateMachine;
    private readonly PetMovementManager _movementManager;
    private readonly ActivityDetector _activityDetector;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly NamedPipeIpcServer _ipcServer;
    private readonly ProcessWatcherService _processWatcher;
    private readonly AutoStartService _autoStartService;
    private bool _isPaused = false;

    public ControlWindow(
        MainWindow mainWindow,
        SpritePlayer player,
        PetStateMachine stateMachine,
        PetMovementManager movementManager,
        ActivityDetector activityDetector,
        ResourceMonitor resourceMonitor,
        NamedPipeIpcServer ipcServer,
        ProcessWatcherService processWatcher,
        AutoStartService autoStartService)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _player = player;
        _stateMachine = stateMachine;
        _movementManager = movementManager;
        _activityDetector = activityDetector;
        _resourceMonitor = resourceMonitor;
        _ipcServer = ipcServer;
        _processWatcher = processWatcher;
        _autoStartService = autoStartService;

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

        // Tahap 5: Windows Startup & Display Info
        ChkStartWithWindows.IsChecked = _autoStartService.IsAutoStartEnabled();
        UpdateDisplayAndDpiInfo();

        // Tahap 4: IPC & Process Watcher Settings
        ChkIpcEnabled.IsChecked = settings.EnableIpc;
        TxtIpcStatus.Text = settings.EnableIpc ? "Aktif" : "Nonaktif";
        TxtIpcStatus.Foreground = settings.EnableIpc ? MediaBrushes.LimeGreen : MediaBrushes.Gray;

        ChkProcessWatcher.IsChecked = settings.EnableProcessWatcher;
        TxtWatcherStatus.Text = settings.EnableProcessWatcher ? "Aktif" : "Nonaktif";
        TxtWatcherStatus.Foreground = settings.EnableProcessWatcher ? MediaBrushes.LimeGreen : MediaBrushes.Gray;

        RefreshWatchedProcessList();

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

    // --- Tahap 4: IPC & Process Watcher Handlers ---
    private void ChkIpcEnabled_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ChkIpcEnabled.IsChecked == true;
        SettingsService.Instance.Settings.EnableIpc = isChecked;
        if (isChecked)
        {
            _ipcServer.Start();
            TxtIpcStatus.Text = "Aktif";
            TxtIpcStatus.Foreground = MediaBrushes.LimeGreen;
        }
        else
        {
            _ipcServer.Stop();
            TxtIpcStatus.Text = "Nonaktif";
            TxtIpcStatus.Foreground = MediaBrushes.Gray;
        }
        SettingsService.Instance.Save();
    }

    private async void BtnSendIpcEvent_Click(object sender, RoutedEventArgs e)
    {
        if (CmbEventType.SelectedItem is not ComboBoxItem selectedItem) return;
        string eventType = (selectedItem.Tag as string) ?? "start";

        int timeout = 5;
        if (int.TryParse(TxtIpcTimeout.Text, out int parsedTimeout))
        {
            timeout = parsedTimeout;
        }

        var message = new PetEventMessage
        {
            Event = eventType,
            Title = string.IsNullOrWhiteSpace(TxtIpcTitle.Text) ? null : TxtIpcTitle.Text.Trim(),
            Message = string.IsNullOrWhiteSpace(TxtIpcMessage.Text) ? null : TxtIpcMessage.Text.Trim(),
            ActionLabel = string.IsNullOrWhiteSpace(TxtIpcActionLabel.Text) ? null : TxtIpcActionLabel.Text.Trim(),
            TimeoutSeconds = timeout
        };

        TxtIpcResult.Text = "Mengirim pesan via named pipe...";
        TxtIpcResult.Foreground = MediaBrushes.Yellow;

        try
        {
            using var client = new NamedPipeClientStream(".", NamedPipeIpcServer.DefaultPipeName, PipeDirection.InOut);
            await client.ConnectAsync(1500);

            using var writer = new StreamWriter(client, System.Text.Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, System.Text.Encoding.UTF8, leaveOpen: true);

            string json = JsonSerializer.Serialize(message);
            await writer.WriteLineAsync(json);

            string? responseJson = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(responseJson))
            {
                var response = JsonSerializer.Deserialize<PetEventResponse>(responseJson);
                TxtIpcResult.Text = $"Respons Pipe: {response?.Status} - {response?.Message}";
                TxtIpcResult.Foreground = MediaBrushes.LimeGreen;
            }
            else
            {
                TxtIpcResult.Text = "Pesan terkirim (tanpa teks balasan).";
                TxtIpcResult.Foreground = MediaBrushes.LightSkyBlue;
            }
        }
        catch (Exception ex)
        {
            TxtIpcResult.Text = $"Gagal terhubung ke pipe: {ex.Message}";
            TxtIpcResult.Foreground = MediaBrushes.IndianRed;
        }
    }

    private void ChkProcessWatcher_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ChkProcessWatcher.IsChecked == true;
        _processWatcher.IsEnabled = isChecked;
        SettingsService.Instance.Settings.EnableProcessWatcher = isChecked;
        TxtWatcherStatus.Text = isChecked ? "Aktif" : "Nonaktif";
        TxtWatcherStatus.Foreground = isChecked ? MediaBrushes.LimeGreen : MediaBrushes.Gray;
        SettingsService.Instance.Save();
    }

    private void BtnAddProcess_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtNewProcessName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _processWatcher.AddTargetProcess(name);
        SettingsService.Instance.Settings.WatchedProcesses = _processWatcher.TargetProcessNames.ToList();
        SettingsService.Instance.Save();
        RefreshWatchedProcessList();
        TxtNewProcessName.Clear();
    }

    private void BtnRemoveProcess_Click(object sender, RoutedEventArgs e)
    {
        if (LstWatchedProcesses.SelectedItem is string selectedProcess)
        {
            _processWatcher.RemoveTargetProcess(selectedProcess);
            SettingsService.Instance.Settings.WatchedProcesses = _processWatcher.TargetProcessNames.ToList();
            SettingsService.Instance.Save();
            RefreshWatchedProcessList();
        }
    }

    private void RefreshWatchedProcessList()
    {
        LstWatchedProcesses.ItemsSource = _processWatcher.TargetProcessNames.OrderBy(p => p).ToList();
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

    private void ChkStartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ChkStartWithWindows.IsChecked == true;
        _mainWindow.SetStartWithWindows(enable);
        ChkStartWithWindows.IsChecked = _autoStartService.IsAutoStartEnabled();
    }

    private void BtnRecoverDisplays_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.ValidateAndReposition();
        UpdateDisplayAndDpiInfo();
        System.Windows.MessageBox.Show(
            $"Posisi Pet berhasil diselaraskan pada koordinat X={_mainWindow.Left:0}, Y={_mainWindow.Top:0} di area monitor aktif.",
            "Pemulihan Multi-Monitor",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopPet"
            );
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Gagal membuka folder data: {ex.Message}");
        }
    }

    public void UpdateDisplayAndDpiInfo()
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(_mainWindow);
            double scalePercent = dpi.DpiScaleX * 100.0;
            TxtDpiInfo.Text = $"Resolusi & DPI: {scalePercent:0}% ({dpi.PixelsPerInchX:0} DPI) | Mode: PerMonitorV2";

            var screens = System.Windows.Forms.Screen.AllScreens;
            TxtMonitorsInfo.Text = $"Monitor Aktif: {screens.Length} Layar Terdeteksi (Utama: {System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width}x{System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height})";
        }
        catch
        {
            // Ignore UI metric inspection issues
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateDisplayAndDpiInfo();
        ChkStartWithWindows.IsChecked = _autoStartService.IsAutoStartEnabled();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
