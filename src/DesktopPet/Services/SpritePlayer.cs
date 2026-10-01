using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class SpritePlayer
{
    private readonly DispatcherTimer _timer;
    private readonly SpriteSheetManager _manager;

    private PetAnimationState _currentState = PetAnimationState.Idle;
    private AnimationDefinition? _currentDef;
    private BitmapSource[] _currentFrames = [];
    private int _currentFrameIndex = 0;
    private Action? _onCompletedCallback;

    public PetAnimationState CurrentState => _currentState;
    public int CurrentFrameIndex => _currentFrameIndex;
    public int TotalFrames => _currentFrames.Length;
    public bool IsStaticGaze { get; private set; }
    public GazeDirection? CurrentGaze { get; private set; }

    public event Action<BitmapSource>? FrameUpdated;
    public event Action<PetAnimationState>? StateChanged;

    public SpritePlayer(SpriteSheetManager manager)
    {
        _manager = manager;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnTimerTick;
        _manager.SkinChanged += OnSkinChanged;
    }

    /// <summary>
    /// Hot-swap: keep the current state, frame position, one-shot callback and pause status,
    /// only the pixels (and frame timing) come from the new skin.
    /// </summary>
    private void OnSkinChanged(SpriteSet _)
    {
        if (IsStaticGaze && CurrentGaze is GazeDirection gaze)
        {
            FrameUpdated?.Invoke(_manager.GetGazeFrame(gaze));
            return;
        }

        if (_currentDef == null) return;

        _currentDef = _manager.GetDefinition(_currentState);
        _currentFrames = _manager.GetAnimationFrames(_currentState);
        if (_currentFrames.Length == 0) return;

        _currentFrameIndex = Math.Min(_currentFrameIndex, _currentFrames.Length - 1);
        FrameUpdated?.Invoke(_currentFrames[_currentFrameIndex]);

        if (_timer.IsEnabled)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(_currentDef.GetDuration(_currentFrameIndex));
        }
    }

    public void PlayAnimation(PetAnimationState state, Action? onCompleted = null)
    {
        if (state == PetAnimationState.Gaze)
        {
            return;
        }

        _currentState = state;
        _currentDef = _manager.GetDefinition(state);
        _currentFrames = _manager.GetAnimationFrames(state);
        _currentFrameIndex = 0;
        _onCompletedCallback = onCompleted;
        IsStaticGaze = false;
        CurrentGaze = null;

        _timer.Stop();

        // Immediately show the first frame
        if (_currentFrames.Length > 0)
        {
            FrameUpdated?.Invoke(_currentFrames[0]);
            StateChanged?.Invoke(_currentState);

            // Schedule next frame
            int duration = _currentDef.GetDuration(0);
            _timer.Interval = TimeSpan.FromMilliseconds(duration);
            _timer.Start();
        }
    }

    public void ShowGaze(GazeDirection direction)
    {
        _timer.Stop();
        IsStaticGaze = true;
        CurrentGaze = direction;
        _currentState = PetAnimationState.Gaze;
        _currentDef = null;
        _currentFrames = [];
        _currentFrameIndex = 0;
        _onCompletedCallback = null;

        var frame = _manager.GetGazeFrame(direction);
        FrameUpdated?.Invoke(frame);
        StateChanged?.Invoke(PetAnimationState.Gaze);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_currentDef == null || _currentFrames.Length == 0)
        {
            _timer.Stop();
            return;
        }

        int nextIndex = _currentFrameIndex + 1;

        if (nextIndex >= _currentFrames.Length)
        {
            if (_currentDef.IsLooping)
            {
                nextIndex = 0;
            }
            else
            {
                // One-shot animation completed
                _timer.Stop();
                var callback = _onCompletedCallback;
                _onCompletedCallback = null;
                callback?.Invoke();
                return;
            }
        }

        _currentFrameIndex = nextIndex;
        FrameUpdated?.Invoke(_currentFrames[_currentFrameIndex]);

        // Dynamically adjust interval for next frame
        int duration = _currentDef.GetDuration(_currentFrameIndex);
        _timer.Interval = TimeSpan.FromMilliseconds(duration);
    }

    public void Pause()
    {
        _timer.Stop();
    }

    public void Resume()
    {
        if (!IsStaticGaze && _currentDef != null)
        {
            int duration = _currentDef.GetDuration(_currentFrameIndex);
            _timer.Interval = TimeSpan.FromMilliseconds(duration);
            _timer.Start();
        }
    }
}
