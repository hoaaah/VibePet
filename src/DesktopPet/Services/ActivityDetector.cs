using System.Windows.Threading;

namespace DesktopPet.Services;

public class ActivityDetector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private uint _lastInputTime = 0;
    private NativeMethods.POINT _lastCursorPos = new() { X = 0, Y = 0 };
    private long _lastTypingTick = 0;
    private bool _isTyping = false;

    public bool IsEnabled { get; set; } = true;
    public int TypingTimeoutMs { get; set; } = 1500;
    public bool IsTyping => _isTyping;
    public (int X, int Y) CurrentCursorPosition => (_lastCursorPos.X, _lastCursorPos.Y);

    public event Action? TypingStarted;
    public event Action? TypingStopped;
    public event Action<int, int>? CursorMoved;

    public ActivityDetector()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        var lii = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (NativeMethods.GetLastInputInfo(ref lii))
        {
            _lastInputTime = lii.dwTime;
        }

        if (NativeMethods.GetCursorPos(out var pt))
        {
            _lastCursorPos = pt;
        }

        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_isTyping)
        {
            _isTyping = false;
            TypingStopped?.Invoke();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!IsEnabled)
        {
            if (_isTyping)
            {
                _isTyping = false;
                TypingStopped?.Invoke();
            }
            return;
        }

        var lii = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        bool gotInput = NativeMethods.GetLastInputInfo(ref lii);
        bool gotCursor = NativeMethods.GetCursorPos(out var curPos);

        long now = Environment.TickCount64;

        if (gotCursor)
        {
            if (curPos.X != _lastCursorPos.X || curPos.Y != _lastCursorPos.Y)
            {
                _lastCursorPos = curPos;
                CursorMoved?.Invoke(curPos.X, curPos.Y);
            }
        }

        if (gotInput)
        {
            // Input event occurred since last check
            if (lii.dwTime != _lastInputTime)
            {
                _lastInputTime = lii.dwTime;

                // If input timestamp changed but cursor position did not change, it indicates keyboard input
                bool cursorStatic = (curPos.X == _lastCursorPos.X && curPos.Y == _lastCursorPos.Y);
                if (cursorStatic)
                {
                    _lastTypingTick = now;
                    if (!_isTyping)
                    {
                        _isTyping = true;
                        TypingStarted?.Invoke();
                    }
                }
            }
        }

        // Check typing timeout / debounce
        if (_isTyping && (now - _lastTypingTick > TypingTimeoutMs))
        {
            _isTyping = false;
            TypingStopped?.Invoke();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
