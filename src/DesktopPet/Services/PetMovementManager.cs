using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class PetMovementManager : IDisposable
{
    private readonly Window _window;
    private readonly PetStateMachine _stateMachine;
    private readonly DispatcherTimer _wanderTimer;
    private readonly DispatcherTimer _stepTimer;
    private readonly DispatcherTimer _paceTimer;
    private readonly Random _random = new();

    private double _targetX;
    private double _stepDelta;
    private bool _isWalking = false;

    private bool _isPacing = false;
    private double _paceMinX;
    private double _paceMaxX;
    private PetAnimationState _paceDirection = PetAnimationState.RunningRight;
    private WorkAnimationStyle _workStyle = WorkAnimationStyle.Pacing;
    private bool _reducedMotion = false;

    public bool IsEnabled { get; set; } = true;
    public double Speed { get; set; } = 3.0; // pixels per step

    public const double PacingHalfRange = 70.0;   // DIP each side of the home position
    public const double PacingSpeed = 2.5;        // DIP per 30 ms step (~83 DIP/s, a relaxed jog)
    public const double MinPacingSpan = 24.0;     // narrower than this -> stay in place

    public bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            _reducedMotion = value;
            SyncPacing();
        }
    }

    public WorkAnimationStyle WorkStyle
    {
        get => _workStyle;
        set
        {
            _workStyle = value;
            SyncPacing();
        }
    }

    public bool IsPacing => _isPacing;

    public PetMovementManager(Window window, PetStateMachine stateMachine)
    {
        _window = window;
        _stateMachine = stateMachine;

        // Periodic wander trigger (checks every 12 seconds)
        _wanderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(14)
        };
        _wanderTimer.Tick += OnWanderCheck;

        // Movement stepping timer (approx 33 fps smooth movement)
        _stepTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _stepTimer.Tick += OnStep;

        _paceTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _paceTimer.Tick += OnPaceStep;

        // Deferred so pacing never re-enters PetStateMachine.EvaluateState
        _stateMachine.StateChanged += (_, _) => _window.Dispatcher.InvokeAsync(SyncPacing);
    }

    public void Start()
    {
        _wanderTimer.Start();
    }

    public void Stop()
    {
        _wanderTimer.Stop();
        CancelMovement();
        StopPacing();
    }

    public void CancelMovement()
    {
        if (_isWalking)
        {
            _stepTimer.Stop();
            _isWalking = false;
            _stateMachine.SetMoving(false);
        }
    }

    public void MoveTo(double targetX)
    {
        if (ReducedMotion || !IsEnabled) return;

        double currentX = _window.Left;
        double diff = targetX - currentX;

        if (Math.Abs(diff) < 20) return; // Already there

        _targetX = targetX;
        _isWalking = true;

        PetAnimationState moveState = diff > 0 ? PetAnimationState.RunningRight : PetAnimationState.RunningLeft;
        _stepDelta = diff > 0 ? Speed : -Speed;

        _stateMachine.SetMoving(true, moveState);
        _stepTimer.Start();
    }

    private void OnWanderCheck(object? sender, EventArgs e)
    {
        if (!IsEnabled || ReducedMotion || _isWalking) return;

        // Only wander if pet is currently idle or gaze
        if (_stateMachine.CurrentPriority is not (PetPriority.Idle or PetPriority.Gaze)) return;

        // 50% chance to start wandering on tick
        if (_random.NextDouble() > 0.5) return;

        var workArea = SystemParameters.WorkArea;
        double minX = workArea.Left + 40;
        double maxX = workArea.Right - _window.Width - 40;

        if (maxX <= minX) return;

        double randomTargetX = minX + (_random.NextDouble() * (maxX - minX));
        MoveTo(randomTargetX);
    }

    private void OnStep(object? sender, EventArgs e)
    {
        // Cancel if higher priority activity intervened
        if (_stateMachine.CurrentPriority > PetPriority.Moving)
        {
            CancelMovement();
            return;
        }

        double currentX = _window.Left;
        double remaining = _targetX - currentX;

        if (Math.Abs(remaining) <= Math.Abs(_stepDelta))
        {
            // Reached target
            _window.Left = _targetX;
            CancelMovement();
        }
        else
        {
            _window.Left = currentX + _stepDelta;
        }
    }

    // ---- Work pacing (Tahap 6.3) -------------------------------------------------------

    private bool ShouldPace =>
        WorkStyle == WorkAnimationStyle.Pacing
        && !ReducedMotion
        && _stateMachine.HasComputerWork
        && !_stateMachine.HasDirectInteraction
        && !_stateMachine.IsManualTestMode
        && _stateMachine.CurrentPriority == PetPriority.ComputerWork;

    /// <summary>
    /// Starts or stops pacing to match the current state. Safe to call at any time.
    /// </summary>
    public void SyncPacing()
    {
        if (ShouldPace)
        {
            if (!_isPacing) StartPacing();
        }
        else if (_isPacing || _stateMachine.WorkPacingDirection != null)
        {
            StopPacing();
        }
    }

    private void StartPacing()
    {
        if (!TryGetWorkArea(out double areaLeft, out double areaRight)) return;

        double width = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
        var bounds = ComputePacingBounds(_window.Left, PacingHalfRange, areaLeft, areaRight, width);
        if (bounds == null) return; // No room: keep the static work animation

        CancelMovement();
        (_paceMinX, _paceMaxX) = bounds.Value;

        // Head toward the side with more room first
        _paceDirection = (_paceMaxX - _window.Left) >= (_window.Left - _paceMinX)
            ? PetAnimationState.RunningRight
            : PetAnimationState.RunningLeft;

        _isPacing = true;
        _paceTimer.Start();
        _stateMachine.SetWorkPacingDirection(_paceDirection);
    }

    private void StopPacing()
    {
        _paceTimer.Stop();
        _isPacing = false;
        _stateMachine.SetWorkPacingDirection(null);
    }

    private void OnPaceStep(object? sender, EventArgs e)
    {
        if (!ShouldPace)
        {
            StopPacing();
            return;
        }

        var (newLeft, newDirection) = NextPacingStep(_window.Left, _paceDirection, PacingSpeed, _paceMinX, _paceMaxX);
        _window.Left = newLeft;

        if (newDirection != _paceDirection)
        {
            _paceDirection = newDirection;
            _stateMachine.SetWorkPacingDirection(newDirection);
        }
    }

    /// <summary>
    /// Pacing range around <paramref name="home"/>, clamped to the monitor work area. Null when there is no room to pace.
    /// </summary>
    public static (double Min, double Max)? ComputePacingBounds(double home, double halfRange, double areaLeft, double areaRight, double windowWidth)
    {
        double min = Math.Max(home - halfRange, areaLeft);
        double max = Math.Min(home + halfRange, areaRight - windowWidth);
        return max - min >= MinPacingSpan ? (min, max) : null;
    }

    /// <summary>
    /// One pacing step; turns around at the bounds.
    /// </summary>
    public static (double Left, PetAnimationState Direction) NextPacingStep(double left, PetAnimationState direction, double speed, double min, double max)
    {
        double next = direction == PetAnimationState.RunningRight ? left + speed : left - speed;

        if (next >= max) return (max, PetAnimationState.RunningLeft);
        if (next <= min) return (min, PetAnimationState.RunningRight);
        return (next, direction);
    }

    /// <summary>
    /// Horizontal work area of the monitor the pet is on, in the window's DIP coordinates.
    /// </summary>
    private bool TryGetWorkArea(out double left, out double right)
    {
        try
        {
            var handle = new WindowInteropHelper(_window).Handle;
            var source = PresentationSource.FromVisual(_window);
            if (handle != IntPtr.Zero && source?.CompositionTarget != null)
            {
                var work = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                var fromDevice = source.CompositionTarget.TransformFromDevice;
                var topLeft = fromDevice.Transform(new System.Windows.Point(work.Left, work.Top));
                var bottomRight = fromDevice.Transform(new System.Windows.Point(work.Right, work.Bottom));
                left = topLeft.X;
                right = bottomRight.X;
                return right > left;
            }
        }
        catch
        {
            // Fall back to the primary work area below
        }

        left = SystemParameters.WorkArea.Left;
        right = SystemParameters.WorkArea.Right;
        return right > left;
    }

    public void Dispose()
    {
        _wanderTimer.Stop();
        _stepTimer.Stop();
        _paceTimer.Stop();
    }
}
