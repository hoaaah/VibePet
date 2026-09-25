using System.ComponentModel;
using System.Windows;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.Views;

public partial class ControlWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly SpritePlayer _player;
    private bool _isPaused = false;

    public ControlWindow(MainWindow mainWindow, SpritePlayer player)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _player = player;

        _player.FrameUpdated += OnPlayerFrameUpdated;
        _player.StateChanged += OnPlayerStateChanged;

        SliderScale.Value = SettingsService.Instance.Settings.Scale;
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
        // Don't dispose on close, just hide so it can be reopened from tray/pet
        e.Cancel = true;
        Hide();
    }
}
