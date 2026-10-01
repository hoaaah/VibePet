using DesktopPet.Models;

namespace DesktopPet.Services;

public enum DragPoseAction
{
    None,
    /// <summary>Mulai/ganti animasi lari ke arah <see cref="DragMotionTracker.Direction"/>.</summary>
    PlayRunning,
    /// <summary>Lanjutkan animasi lari yang sedang ditahan.</summary>
    ResumeRunning,
    /// <summary>Kursor diam: tahan frame lari yang sedang tampil.</summary>
    Hold,
}

/// <summary>
/// Menentukan pose pet saat diseret berdasarkan pergeseran jendela.
/// Arah hanya berbalik setelah pergeseran berlawanan terkumpul cukup jauh, agar getaran tangan tidak membuat animasi berkedip.
/// </summary>
public sealed class DragMotionTracker
{
    public const double MovementEpsilon = 0.5;          // DIP
    public const double DirectionFlipThreshold = 6.0;   // DIP berlawanan arah sebelum berbalik
    public static readonly TimeSpan StillnessTimeout = TimeSpan.FromMilliseconds(180);

    private double _reverseDistance;
    private TimeSpan _lastMoveTime;

    public PetAnimationState? Direction { get; private set; }
    public bool IsHeld { get; private set; } = true;

    public void Begin(TimeSpan now)
    {
        Direction = null;
        IsHeld = true;
        _reverseDistance = 0;
        _lastMoveTime = now;
    }

    public DragPoseAction OnMoved(double deltaX, double deltaY, TimeSpan now)
    {
        bool movedX = Math.Abs(deltaX) >= MovementEpsilon;
        if (!movedX && Math.Abs(deltaY) < MovementEpsilon) return DragPoseAction.None;

        _lastMoveTime = now;

        if (movedX)
        {
            var moveDirection = deltaX > 0 ? PetAnimationState.RunningRight : PetAnimationState.RunningLeft;

            if (Direction == null)
            {
                Direction = moveDirection;
                IsHeld = false;
                return DragPoseAction.PlayRunning;
            }

            if (moveDirection != Direction)
            {
                _reverseDistance += Math.Abs(deltaX);
                if (_reverseDistance >= DirectionFlipThreshold)
                {
                    Direction = moveDirection;
                    _reverseDistance = 0;
                    IsHeld = false;
                    return DragPoseAction.PlayRunning;
                }
            }
            else
            {
                _reverseDistance = 0;
            }
        }

        // Gerak vertikal atau jitter kecil tetap dianggap bergerak bila arah sudah diketahui.
        if (IsHeld && Direction != null)
        {
            IsHeld = false;
            return DragPoseAction.ResumeRunning;
        }

        return DragPoseAction.None;
    }

    public DragPoseAction Tick(TimeSpan now)
    {
        if (!IsHeld && Direction != null && now - _lastMoveTime >= StillnessTimeout)
        {
            IsHeld = true;
            return DragPoseAction.Hold;
        }
        return DragPoseAction.None;
    }
}
