using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace PhosphorPet;

internal sealed class DesktopGooseMode : IDisposable
{
    private const int MouseLeftButton = 0x01;
    private const int MouseRightButton = 0x02;
    private const int MouseMiddleButton = 0x04;
    private const int MouseXButton1 = 0x05;
    private const int MouseXButton2 = 0x06;
    private readonly Func<Point> _getPetPosition;
    private readonly Func<Point, Point> _getPetPositionForCursor;
    private readonly Func<bool> _canGrabCursor;
    private readonly Action<Point> _movePet;
    private readonly Action<bool> _setPetGrabbed;
    private readonly Random _random = new();
    private readonly DispatcherTimer _eventTimer = new();
    private readonly DispatcherTimer _cursorSampleTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };
    private readonly DispatcherTimer _tugTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };
    private Point _lastCursorPosition;
    private long _lastCursorMovement;
    private long _animationStarted;
    private Point _tugOrigin;
    private Point _tugDestination;
    private Point _lastTugPosition;
    private Point _petStartPosition;
    private Point _petTargetPosition;
    private bool _doodlesEnabled = true;
    private bool _cursorGrabsEnabled = true;
    private bool _hasCursorSample;
    private bool _petIsGrabbing;
    private int _checkIntervalSeconds = 12;
    private GoosePeekWindow? _peekWindow;
    private bool _enabled;
    private TugStage _tugStage;

    public DesktopGooseMode(
        Func<Point> getPetPosition,
        Func<Point, Point> getPetPositionForCursor,
        Action<Point> movePet,
        Action<bool> setPetGrabbed,
        Func<bool> canGrabCursor)
    {
        _getPetPosition = getPetPosition;
        _getPetPositionForCursor = getPetPositionForCursor;
        _movePet = movePet;
        _setPetGrabbed = setPetGrabbed;
        _canGrabCursor = canGrabCursor;
        _eventTimer.Tick += EventTimer_Tick;
        _cursorSampleTimer.Tick += CursorSampleTimer_Tick;
        _tugTimer.Tick += TugTimer_Tick;
    }

    public int CheckIntervalSeconds
    {
        get => _checkIntervalSeconds;
        set
        {
            if (value is not (12 or 30 or 60))
                throw new ArgumentOutOfRangeException(nameof(value));

            _checkIntervalSeconds = value;
            if (_enabled)
                ScheduleNextEvent();
        }
    }

    public int IdleThresholdSeconds { get; set; } = 7;

    public bool DoodlesEnabled
    {
        get => _doodlesEnabled;
        set
        {
            _doodlesEnabled = value;
            if (!value)
            {
                _peekWindow?.Close();
                _peekWindow = null;
            }
        }
    }

    public bool CursorGrabsEnabled
    {
        get => _cursorGrabsEnabled;
        set
        {
            _cursorGrabsEnabled = value;
            if (!value)
                AbortTug();
        }
    }

    public void Start()
    {
        if (_enabled)
            return;

        _enabled = true;
        _hasCursorSample = false;
        _cursorSampleTimer.Start();
        ScheduleNextEvent();
    }

    public void Stop()
    {
        if (!_enabled)
            return;

        _enabled = false;
        _eventTimer.Stop();
        _cursorSampleTimer.Stop();
        AbortTug();
        _peekWindow?.Close();
        _peekWindow = null;
    }

    public void CancelCursorGrab() => AbortTug();

    public void Dispose()
    {
        Stop();
        _eventTimer.Tick -= EventTimer_Tick;
        _cursorSampleTimer.Tick -= CursorSampleTimer_Tick;
        _tugTimer.Tick -= TugTimer_Tick;
    }

    private void ScheduleNextEvent()
    {
        _eventTimer.Stop();
        _eventTimer.Interval = TimeSpan.FromSeconds(CheckIntervalSeconds);
        _eventTimer.Start();
    }

    private void EventTimer_Tick(object? sender, EventArgs e)
    {
        _eventTimer.Stop();
        if (!_enabled)
            return;

        if (CursorGrabsEnabled && _random.Next(4) == 0 && TryStartTug())
        {
            ScheduleNextEvent();
            return;
        }

        if (DoodlesEnabled)
            ShowPeek();
        ScheduleNextEvent();
    }

    private void CursorSampleTimer_Tick(object? sender, EventArgs e)
    {
        if (!TryGetCursorPosition(out var position))
            return;

        if (!_hasCursorSample || position != _lastCursorPosition)
        {
            _lastCursorPosition = position;
            _lastCursorMovement = Environment.TickCount64;
            _hasCursorSample = true;
        }
    }

    private void ShowPeek()
    {
        if (_peekWindow is not null)
            return;

        _peekWindow = new GoosePeekWindow(_random);
        _peekWindow.Closed += (_, _) => _peekWindow = null;
        _peekWindow.Show();
    }

    private bool TryStartTug()
    {
        if (!_canGrabCursor() ||
            !_hasCursorSample ||
            Environment.TickCount64 - _lastCursorMovement < IdleThresholdSeconds * 1000L ||
            !IsSystemIdle((uint)(IdleThresholdSeconds * 1000)) ||
            IsMouseButtonPressed() ||
            !TryGetCursorPosition(out var currentPosition) ||
            currentPosition != _lastCursorPosition)
        {
            return false;
        }

        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0)
            return false;

        var minimumDistance = Math.Clamp(
            screens.Min(screen => Math.Min(screen.Bounds.Width, screen.Bounds.Height)) * 0.25,
            160,
            420);
        _tugOrigin = currentPosition;
        if (!TryPickDistantDestination(screens, currentPosition, minimumDistance, out _tugDestination))
            return false;

        _petStartPosition = _getPetPosition();
        _petTargetPosition = _getPetPositionForCursor(currentPosition);
        _lastTugPosition = currentPosition;
        _animationStarted = Environment.TickCount64;
        _tugStage = TugStage.Approaching;
        _tugTimer.Start();
        return true;
    }

    private void TugTimer_Tick(object? sender, EventArgs e)
    {
        if (!_enabled || !_canGrabCursor() || !IsSystemIdle(1000) || IsMouseButtonPressed() ||
            !TryGetCursorPosition(out var currentPosition) ||
            currentPosition != _lastTugPosition)
        {
            AbortTug();
            return;
        }

        switch (_tugStage)
        {
            case TugStage.Approaching:
                AnimatePetApproach();
                break;
            case TugStage.GrabPause:
                if (Environment.TickCount64 - _animationStarted >= 360)
                {
                    _tugStage = TugStage.Carrying;
                    _animationStarted = Environment.TickCount64;
                }
                break;
            case TugStage.Carrying:
                AnimateCursorCarry();
                break;
        }
    }

    private void AnimatePetApproach()
    {
        const double durationMilliseconds = 1450;
        var progress = Math.Clamp((Environment.TickCount64 - _animationStarted) / durationMilliseconds, 0, 1);
        var easedProgress = progress * progress * (3 - 2 * progress);
        _movePet(Interpolate(_petStartPosition, _petTargetPosition, easedProgress));

        if (progress < 1)
            return;

        _movePet(_petTargetPosition);
        SetPetGrabbing(true);
        _tugStage = TugStage.GrabPause;
        _animationStarted = Environment.TickCount64;
    }

    private void AnimateCursorCarry()
    {
        const double durationMilliseconds = 2300;
        var progress = Math.Clamp((Environment.TickCount64 - _animationStarted) / durationMilliseconds, 0, 1);
        var easedProgress = progress * progress * (3 - 2 * progress);
        var nextPosition = Interpolate(_tugOrigin, _tugDestination, easedProgress);

        if (!SetCursorPos((int)nextPosition.X, (int)nextPosition.Y))
        {
            AbortTug();
            return;
        }

        _lastTugPosition = nextPosition;
        _movePet(_getPetPositionForCursor(nextPosition));
        if (progress >= 1)
        {
            _movePet(_getPetPositionForCursor(_tugDestination));
            AbortTug();
        }
    }

    private void AbortTug()
    {
        _tugTimer.Stop();
        _tugStage = TugStage.None;
        SetPetGrabbing(false);
    }

    private void SetPetGrabbing(bool grabbed)
    {
        if (_petIsGrabbing == grabbed)
            return;

        _petIsGrabbing = grabbed;
        _setPetGrabbed(grabbed);
    }

    private static Point Interpolate(Point from, Point to, double progress) =>
        new(
            Math.Round(from.X + (to.X - from.X) * progress),
            Math.Round(from.Y + (to.Y - from.Y) * progress));

    private enum TugStage
    {
        None,
        Approaching,
        GrabPause,
        Carrying
    }

    private bool TryPickDistantDestination(
        Forms.Screen[] screens,
        Point origin,
        double minimumDistance,
        out Point destination)
    {
        for (var attempt = 0; attempt < 48; attempt++)
        {
            var bounds = screens[_random.Next(screens.Length)].Bounds;
            var candidate = new Point(
                _random.Next(bounds.Left, bounds.Right),
                _random.Next(bounds.Top, bounds.Bottom));
            if (Distance(origin, candidate) >= minimumDistance)
            {
                destination = candidate;
                return true;
            }
        }

        var furthestCorner = default(Point);
        var furthestDistance = -1d;
        foreach (var bounds in screens.Select(screen => screen.Bounds))
        {
            foreach (var candidate in new[]
                     {
                         new Point(bounds.Left, bounds.Top),
                         new Point(bounds.Right - 1, bounds.Top),
                         new Point(bounds.Left, bounds.Bottom - 1),
                         new Point(bounds.Right - 1, bounds.Bottom - 1)
                     })
            {
                var distance = Distance(origin, candidate);
                if (distance > furthestDistance)
                {
                    furthestCorner = candidate;
                    furthestDistance = distance;
                }
            }
        }

        destination = furthestCorner;
        return furthestDistance >= minimumDistance;
    }

    private static double Distance(Point first, Point second)
    {
        var horizontal = first.X - second.X;
        var vertical = first.Y - second.Y;
        return Math.Sqrt(horizontal * horizontal + vertical * vertical);
    }

    private static bool IsMouseButtonPressed() =>
        IsKeyPressed(MouseLeftButton) ||
        IsKeyPressed(MouseRightButton) ||
        IsKeyPressed(MouseMiddleButton) ||
        IsKeyPressed(MouseXButton1) ||
        IsKeyPressed(MouseXButton2);

    private static bool IsKeyPressed(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool IsSystemIdle(uint minimumMilliseconds)
    {
        var inputInfo = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>()
        };
        if (!GetLastInputInfo(ref inputInfo))
            return false;

        var elapsed = unchecked((uint)Environment.TickCount - inputInfo.Time);
        return elapsed >= minimumMilliseconds;
    }

    private static bool TryGetCursorPosition(out Point position)
    {
        if (GetCursorPos(out var cursor))
        {
            position = new Point(cursor.X, cursor.Y);
            return true;
        }

        position = default;
        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo inputInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}
