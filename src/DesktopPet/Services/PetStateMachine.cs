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
    public bool HasComputerWork { get; private set; }
    public bool HasUserTyping { get; private set; }
    public bool IsMoving { get; private set; }
    public PetAnimationState MovingState { get; private set; } = PetAnimationState.RunningRight;
    public GazeDirection? CurrentGaze { get; private set; }

    public PetPriority CurrentPriority { get; private set; } = PetPriority.Idle;
    public PetAnimationState CurrentVisualState => _player.CurrentState;

    public event Action<PetPriority, PetAnimationState>? StateChanged;

    public PetStateMachine(SpritePlayer player)
    {
        _player = player;
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

    public void SetComputerWork(bool active)
    {
        HasComputerWork = active;
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
        CurrentGaze = gaze;
        EvaluateState();
    }

    public void ClearAllSimulations()
    {
        HasError = false;
        HasNeedsAction = false;
        HasNotification = false;
        HasJobSuccess = false;
        HasComputerWork = false;
        HasUserTyping = false;
        IsMoving = false;
        CurrentGaze = null;
        EvaluateState();
    }

    public void EvaluateState()
    {
        // Calculate highest priority
        PetPriority highest;
        PetAnimationState targetAnimation;
        Action? oneShotCallback = null;

        if (HasDirectInteraction)
        {
            highest = PetPriority.DirectInteraction;
            // Hold current or Jumping when released
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
            targetAnimation = PetAnimationState.Running; // Row 7: PC Work
        }
        else if (HasUserTyping)
        {
            highest = PetPriority.UserTyping;
            targetAnimation = PetAnimationState.Review; // Row 8: Typing
        }
        else if (IsMoving)
        {
            highest = PetPriority.Moving;
            targetAnimation = MovingState; // RunningRight or RunningLeft
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
