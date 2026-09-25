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
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly SpriteSheetManager _sheetManager;
    private readonly SpritePlayer _player;
    private readonly SettingsService _settingsService;
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

        BuildContextMenu();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Win32 tool window styles (hide from Alt-Tab)
        var helper = new WindowInteropHelper(this);
        int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Apply scale & position
        SetScale(_settingsService.Settings.Scale);
        System.Windows.Point pos = _settingsService.GetValidatedPosition(Width, Height);
        Left = pos.X;
        Top = pos.Y;

        // Initialize ControlWindow and TrayManager
        _controlWindow = new ControlWindow(this, _player);
        _trayManager = new TrayIconManager(this, _controlWindow);

        // Start default Idle animation
        _player.PlayAnimation(PetAnimationState.Idle);
    }

    private void OnFrameUpdated(System.Windows.Media.Imaging.BitmapSource frame)
    {
        Dispatcher.InvokeAsync(() =>
        {
            PetImage.Source = frame;
        });
    }

    public void SetScale(double scale)
    {
        _currentScale = Math.Clamp(scale, 0.5, 3.0);
        double w = AnimationCatalog.CellWidth * _currentScale;
        double h = AnimationCatalog.CellHeight * _currentScale;

        PetImage.Width = w;
        PetImage.Height = h;
        Width = w;
        Height = h;

        _settingsService.Settings.Scale = _currentScale;
        _settingsService.Save();
    }

    public void ResetPosition()
    {
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
            _isDragging = true;
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
                _player.PlayAnimation(PetAnimationState.Idle);
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
                if (targetState is PetAnimationState.Waving or PetAnimationState.Jumping)
                {
                    _player.PlayAnimation(targetState, () => _player.PlayAnimation(PetAnimationState.Idle));
                }
                else
                {
                    _player.PlayAnimation(targetState);
                }
            };
            MenuAnimations.Items.Add(item);
        }

        // 2. Gaze directions
        foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
        {
            var (row, col) = AnimationCatalog.GetGazeCell(gaze);
            string name = AnimationCatalog.GetGazeDisplayName(gaze);
            var item = new MenuItem { Header = $"{name} (Baris {row}, Kolom {col})" };
            var targetGaze = gaze;
            item.Click += (s, e) => _player.ShowGaze(targetGaze);
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
        _settingsService.Settings.X = Left;
        _settingsService.Settings.Y = Top;
        _settingsService.Save();

        _trayManager?.Dispose();
    }
}