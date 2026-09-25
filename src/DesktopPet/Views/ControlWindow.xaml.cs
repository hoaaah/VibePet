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
    private bool _isPaused = false;

    public ControlWindow(
        MainWindow mainWindow,
        SpritePlayer player,
        PetStateMachine stateMachine,
        PetMovementManager movementManager,
        ActivityDetector activityDetector)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _player = player;
        _stateMachine = stateMachine;
        _movementManager = movementManager;
        _activityDetector = activityDetector;

        _player.FrameUpdated += OnPlayerFrameUpdated;
        _player.StateChanged += OnPlayerStateChanged;
        _stateMachine.StateChanged += OnStateMachineChanged;

        var settings = SettingsService.Instance.Settings;
        SliderScale.Value = settings.Scale;
        ChkReducedMotion.IsChecked = settings.ReducedMotion;
        ChkAutoWander.IsChecked = settings.AutoWander;
        ChkGazeTracking.IsChecked = settings.GazeTracking;
        ChkTypingDetection.IsChecked = settings.TypingDetection;

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
            TxtPriority.Text = $"Prioritas Aktif: {priority} -> Animasi: {state}";
            UpdateStatusText();
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

    // --- Manual Animations ---
    private void BtnIdle_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Idle);
    private void BtnRunRight_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.RunningRight);
    private void BtnRunLeft_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.RunningLeft);
    private void BtnWaving_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Waving, () => _player.PlayAnimation(PetAnimationState.Idle));
    private void BtnJumping_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Jumping, () => _player.PlayAnimation(PetAnimationState.Idle));
    private void BtnFailed_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Failed);
    private void BtnWaiting_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Waiting);
    private void BtnRunWork_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Running);
    private void BtnReview_Click(object sender, RoutedEventArgs e) => _player.PlayAnimation(PetAnimationState.Review);

    private void BtnGaze_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tagStr && int.TryParse(tagStr, out int index))
        {
            _player.ShowGaze((GazeDirection)index);
        }
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
