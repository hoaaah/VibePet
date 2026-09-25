using System.Windows;
using System.Windows.Threading;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class PetMovementManager : IDisposable
{
    private readonly Window _window;
    private readonly PetStateMachine _stateMachine;
    private readonly DispatcherTimer _wanderTimer;
    private readonly DispatcherTimer _stepTimer;
    private readonly Random _random = new();

    private double _targetX;
    private double _stepDelta;
    private bool _isWalking = false;

    public bool IsEnabled { get; set; } = true;
    public bool ReducedMotion { get; set; } = false;
    public double Speed { get; set; } = 3.0; // pixels per step

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
    }

    public void Start()
    {
        _wanderTimer.Start();
    }

    public void Stop()
    {
        _wanderTimer.Stop();
        CancelMovement();
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

    public void Dispose()
    {
        _wanderTimer.Stop();
        _stepTimer.Stop();
    }
}
