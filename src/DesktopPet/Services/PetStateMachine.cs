using DesktopPet.Models;

namespace DesktopPet.Services;

public enum PetPriority
{
    Idle = 0,
    Gaze = 1,
    Moving = 2,
    UserTyping = 3,
    ComputerWork = 4,
    JobSuccess = 5,
    Notification = 6,
    NeedsAction = 7,
    Error = 8,
    DirectInteraction = 9
}

public class PetStateMachine
{
    private readonly SpritePlayer _player;

    // Active conditions
    public bool HasDirectInteraction { get; private set; }
    public bool HasError { get; private set; }
    public bool HasNeedsAction { get; private set; }
    public bool HasNotification { get; private set; }
    public bool HasJobSuccess { get; private set; }
    public WorkSource ActiveWorkSources { get; private set; }
    public bool HasComputerWork => ActiveWorkSources != WorkSource.None;
    /// <summary>Arah lari saat mode kerja bolak-balik aktif; null = animasi kerja statis (Row 7).</summary>
    public PetAnimationState? WorkPacingDirection { get; private set; }
    public bool HasUserTyping { get; private set; }
    public bool IsMoving { get; private set; }
    public PetAnimationState MovingState { get; private set; } = PetAnimationState.RunningRight;
    public GazeDirection? CurrentGaze { get; private set; }

    // Manual test mode (locks animation so mouse gaze does not override during testing)
    public bool IsManualTestMode { get; private set; }
    public PetAnimationState? ManualAnimation { get; private set; }
    public GazeDirection? ManualGaze { get; private set; }

    public PetPriority CurrentPriority { get; private set; } = PetPriority.Idle;
    public PetAnimationState CurrentVisualState => _player.CurrentState;

    public event Action<PetPriority, PetAnimationState>? StateChanged;

    public PetStateMachine(SpritePlayer player)
    {
        _player = player;
    }

    public void SetManualAnimation(PetAnimationState state)
    {
        IsManualTestMode = true;
        ManualAnimation = state;
        ManualGaze = null;
        EvaluateState();
    }

    public void SetManualGaze(GazeDirection gaze)
    {
        IsManualTestMode = true;
        ManualGaze = gaze;
        ManualAnimation = null;
        EvaluateState();
    }

    public void ClearManualTestMode()
    {
        IsManualTestMode = false;
        ManualAnimation = null;
        ManualGaze = null;
        EvaluateState();
    }

    public void SetDirectInteraction(bool active)
    {
        HasDirectInteraction = active;
        EvaluateState();
    }

    public void SetError(bool active)
    {
        HasError = active;
        EvaluateState();
    }

    public void SetNeedsAction(bool active)
    {
        HasNeedsAction = active;
        EvaluateState();
    }

    public void TriggerNotification()
    {
        HasNotification = true;
        EvaluateState();
    }

    public void TriggerJobSuccess()
    {
        HasJobSuccess = true;
        EvaluateState();
    }

    public void SetComputerWork(bool active) => SetWorkSource(WorkSource.Simulation, active);

    public void SetWorkSource(WorkSource source, bool active)
    {
        var updated = active ? ActiveWorkSources | source : ActiveWorkSources & ~source;
        if (updated == ActiveWorkSources) return;
        ActiveWorkSources = updated;
        EvaluateState();
    }

    public void SetWorkPacingDirection(PetAnimationState? direction)
    {
        if (direction is not (null or PetAnimationState.RunningRight or PetAnimationState.RunningLeft))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        if (WorkPacingDirection == direction) return;
        WorkPacingDirection = direction;
        EvaluateState();
    }

    public void SetUserTyping(bool active)
    {
        HasUserTyping = active;
        EvaluateState();
    }

    public void SetMoving(bool moving, PetAnimationState direction = PetAnimationState.RunningRight)
    {
        IsMoving = moving;
        MovingState = direction;
        EvaluateState();
    }

    public void SetGaze(GazeDirection? gaze)
    {
        if (IsManualTestMode) return; // Do not overwrite manual testing
        CurrentGaze = gaze;
        EvaluateState();
    }

    public void ClearAllSimulations()
    {
        IsManualTestMode = false;
        ManualAnimation = null;
        ManualGaze = null;
        HasError = false;
        HasNeedsAction = false;
        HasNotification = false;
        HasJobSuccess = false;
        // CPU load and watched-process activity are measurements, not simulations; their
        // services report changes only, so wiping them here would leave the pet out of sync.
        ActiveWorkSources &= WorkSource.CpuLoad | WorkSource.Process;
        HasUserTyping = false;
        IsMoving = false;
        CurrentGaze = null;
        EvaluateState();
    }

    public void EvaluateState()
    {
        PetPriority highest;
        PetAnimationState targetAnimation;
        Action? oneShotCallback = null;

        if (IsManualTestMode)
        {
            highest = PetPriority.DirectInteraction;
            if (ManualGaze.HasValue)
            {
                CurrentPriority = highest;
                _player.ShowGaze(ManualGaze.Value);
                StateChanged?.Invoke(highest, PetAnimationState.Gaze);
                return;
            }
            else if (ManualAnimation.HasValue)
            {
                targetAnimation = ManualAnimation.Value;
                if (targetAnimation is PetAnimationState.Waving)
                {
                    oneShotCallback = () =>
                    {
                        IsManualTestMode = false;
                        ManualAnimation = null;
                        EvaluateState();
                    };
                }
                else if (targetAnimation is PetAnimationState.Jumping)
                {
                    oneShotCallback = () =>
                    {
                        IsManualTestMode = false;
                        ManualAnimation = null;
                        EvaluateState();
                    };
                }
            }
            else
            {
                targetAnimation = PetAnimationState.Idle;
            }
        }
        else if (HasDirectInteraction)
        {
            highest = PetPriority.DirectInteraction;
            return;
        }
        else if (HasError)
        {
            highest = PetPriority.Error;
            targetAnimation = PetAnimationState.Failed;
        }
        else if (HasNeedsAction)
        {
            highest = PetPriority.NeedsAction;
            targetAnimation = PetAnimationState.Waiting;
        }
        else if (HasNotification)
        {
            highest = PetPriority.Notification;
            targetAnimation = PetAnimationState.Waving;
            oneShotCallback = () =>
            {
                HasNotification = false;
                EvaluateState();
            };
        }
        else if (HasJobSuccess)
        {
            highest = PetPriority.JobSuccess;
            targetAnimation = PetAnimationState.Jumping;
            oneShotCallback = () =>
            {
                HasJobSuccess = false;
                EvaluateState();
            };
        }
        else if (HasComputerWork)
        {
            highest = PetPriority.ComputerWork;
            // Row 7 in place, or Row 1/2 while PetMovementManager paces the pet back and forth
            targetAnimation = WorkPacingDirection ?? PetAnimationState.Running;
        }
        else if (HasUserTyping)
        {
            highest = PetPriority.UserTyping;
            targetAnimation = PetAnimationState.Review; // Row 8: Typing
        }
        else if (IsMoving)
        {
            highest = PetPriority.Moving;
            targetAnimation = MovingState; // Row 1: RunningRight or Row 2: RunningLeft
        }
        else if (CurrentGaze.HasValue)
        {
            highest = PetPriority.Gaze;
            CurrentPriority = highest;
            _player.ShowGaze(CurrentGaze.Value);
            StateChanged?.Invoke(highest, PetAnimationState.Gaze);
            return;
        }
        else
        {
            highest = PetPriority.Idle;
            targetAnimation = PetAnimationState.Idle; // Row 0
        }

        // Apply transition if different
        if (CurrentPriority != highest || _player.CurrentState != targetAnimation)
        {
            CurrentPriority = highest;
            _player.PlayAnimation(targetAnimation, oneShotCallback);
            StateChanged?.Invoke(highest, targetAnimation);
        }
    }
}
