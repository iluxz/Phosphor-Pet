using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using MenuItem = System.Windows.Controls.MenuItem;
using Point = System.Windows.Point;

namespace PhosphorPet;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _wanderTimer = new() { Interval = TimeSpan.FromMilliseconds(32) };
    private readonly DispatcherTimer _destinationTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly DispatcherTimer _bobTimer = new() { Interval = TimeSpan.FromMilliseconds(32) };
    private readonly DispatcherTimer _sleepTransitionTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _wakeAngerTimer = new() { Interval = TimeSpan.FromMilliseconds(2400) };
    private readonly DesktopGooseMode _funMode;
    private readonly Random _random = new();
    private DateTime _travelStarted;
    private TimeSpan _travelDuration;
    private Point _travelFrom;
    private Point _travelTo;
    private Point _sleepFrom;
    private Point _sleepTo;
    private DateTime _sleepStarted;
    private DateTime _bobStarted = DateTime.UtcNow;
    private bool _isDragging;
    private bool _isBeingCarried;
    private bool _isAngry;
    private bool _isWakingAngry;
    private bool _isGoingToSleep;
    private bool _isSleeping;
    private bool _isWandering = true;
    private bool _largo;
    private string _hat = "none";

    public MainWindow()
    {
        InitializeComponent();
        Glow.Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(120, 180, 255),
            BlurRadius = 28,
            ShadowDepth = 0,
            Opacity = 0.8
        };
        _funMode = new DesktopGooseMode(
            () => new Point(Left, Top),
            CursorPositionToWindowPosition,
            position =>
            {
                Left = position.X;
                Top = position.Y;
            },
            SetCursorGrabbed,
            () => !_isSleeping && !_isGoingToSleep && !_isWakingAngry);
        PlaceNearScreenCorner();
        _wanderTimer.Tick += WanderTimer_Tick;
        _destinationTimer.Tick += DestinationTimer_Tick;
        _bobTimer.Tick += BobTimer_Tick;
        _sleepTransitionTimer.Tick += SleepTransitionTimer_Tick;
        _wakeAngerTimer.Tick += WakeAngerTimer_Tick;
        Loaded += (_, _) =>
        {
            BeginDrift();
            _destinationTimer.Start();
            _bobTimer.Start();
        };
        Closed += (_, _) =>
        {
            _wanderTimer.Stop();
            _destinationTimer.Stop();
            _bobTimer.Stop();
            _sleepTransitionTimer.Stop();
            _wakeAngerTimer.Stop();
            _funMode.Dispose();
        };
    }

    private void PlaceNearScreenCorner()
    {
        Left = Math.Max(0, SystemParameters.PrimaryScreenWidth - Width - 48);
        Top = Math.Max(0, SystemParameters.PrimaryScreenHeight * 0.28);
    }

    private void BeginDrift()
    {
        _travelFrom = new Point(Left, Top);
        _travelTo = PickDestination();
        _travelStarted = DateTime.UtcNow;
        _travelDuration = TimeSpan.FromSeconds(3.8 + _random.NextDouble() * 2.8);
        _wanderTimer.Start();
    }

    private Point PickDestination()
    {
        var maxLeft = Math.Max(0, SystemParameters.PrimaryScreenWidth - Width);
        var maxTop = Math.Max(0, SystemParameters.PrimaryScreenHeight - Height);
        return new Point(_random.NextDouble() * maxLeft, _random.NextDouble() * maxTop);
    }

    private Point CursorPositionToWindowPosition(Point cursorPosition) =>
        new(cursorPosition.X - Width / 2, cursorPosition.Y - Height / 2);

    private void WanderTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isWandering || _isDragging || _isBeingCarried || _isGoingToSleep || _isSleeping ||
            _isWakingAngry || PetMenu.IsOpen)
            return;

        var progress = Math.Clamp((DateTime.UtcNow - _travelStarted).TotalMilliseconds /
                                  _travelDuration.TotalMilliseconds, 0, 1);
        var eased = progress * progress * (3 - 2 * progress);
        Left = _travelFrom.X + (_travelTo.X - _travelFrom.X) * eased;
        Top = _travelFrom.Y + (_travelTo.Y - _travelFrom.Y) * eased;

        if (progress >= 1)
            _wanderTimer.Stop();
    }

    private void DestinationTimer_Tick(object? sender, EventArgs e)
    {
        if (_isWandering && !_isDragging && !_isBeingCarried && !_isGoingToSleep && !_isSleeping &&
            !_isWakingAngry && !PetMenu.IsOpen && !_wanderTimer.IsEnabled)
            BeginDrift();
    }

    private void BobTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _bobStarted).TotalSeconds;
        var breathing = Math.Sin(elapsed * Math.PI * 2 / 2.4);
        var isAngryAnimation = _isDragging || _isBeingCarried || _isWakingAngry;
        BobTransform.Y = _isSleeping
            ? -10 * (1 + breathing) / 2
            : isAngryAnimation
            ? Math.Sin(elapsed * Math.PI * 2 * (_isWakingAngry ? 15 : 12)) * (_isWakingAngry ? 4 : 2)
            : -5 * (1 + Math.Sin(elapsed * Math.PI * 2 / 3)) / 2;
        var squish = Math.Sin(elapsed * Math.PI * 2 / 3);
        SquishTransform.ScaleX = _isSleeping
            ? 1 + 0.035 * breathing
            : isAngryAnimation ? (_isWakingAngry ? 0.88 : 0.94) : 1 + 0.025 * squish;
        SquishTransform.ScaleY = _isSleeping
            ? 1 - 0.035 * breathing
            : isAngryAnimation ? (_isWakingAngry ? 1.14 : 1.06) : 1 - 0.025 * squish;
        ShakeTransform.Angle = isAngryAnimation
            ? Math.Sin(elapsed * Math.PI * 2 * (_isWakingAngry ? 15 : 12)) * (_isWakingAngry ? 9 : 4)
            : 0;
        if (_isSleeping)
        {
            SleepMark.Opacity = 0.55 + 0.35 * (1 + Math.Sin(elapsed * Math.PI * 2 / 2.4)) / 2;
            SleepMarkTransform.Y = -4 - 6 * (1 + Math.Sin(elapsed * Math.PI * 2 / 2.4)) / 2;
        }
    }

    private void PetCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isWakingAngry)
        {
            e.Handled = true;
            return;
        }

        if (_isSleeping || _isGoingToSleep)
        {
            WakeUp();
            e.Handled = true;
            return;
        }

        _isDragging = true;
        SetAngry(true);
        _wanderTimer.Stop();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _isDragging = false;
            SetAngry(false);
            if (_isWandering && !PetMenu.IsOpen)
                BeginDrift();
        }
    }

    private void PetCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        SetAngry(false);
    }

    private void SetAngry(bool angry)
    {
        _isAngry = angry || _isWakingAngry;
        UpdateFace();
    }

    private void UpdateFace()
    {
        AngryGlow.Opacity = _isAngry ? 1 : 0;
        AngryMark.Opacity = _isAngry ? 1 : 0;
        SleepMark.Visibility = _isSleeping ? Visibility.Visible : Visibility.Collapsed;
        Face.Text = _isAngry ? ">.<" : _isSleeping ? "-.-" : _hat == "none" ? ".." : "^..^";
        Face.Foreground = _isAngry
            ? new SolidColorBrush(Color.FromRgb(255, 130, 145))
            : _isSleeping
                ? new SolidColorBrush(Color.FromRgb(207, 226, 255))
                : _hat == "gold"
                    ? new SolidColorBrush(Color.FromRgb(255, 215, 112))
                    : new SolidColorBrush(Color.FromRgb(232, 255, 255));
        Face.Effect = _isAngry
            ? new DropShadowEffect { Color = Color.FromRgb(255, 70, 95), BlurRadius = 10, ShadowDepth = 0 }
            : !_isSleeping && _hat == "gold"
                ? new DropShadowEffect { Color = Color.FromRgb(255, 200, 90), BlurRadius = 8, ShadowDepth = 0 }
                : null;
    }

    private void SetCursorGrabbed(bool grabbed)
    {
        _isBeingCarried = grabbed;
        _wanderTimer.Stop();
        SetAngry(grabbed);

        if (!grabbed && IsVisible && _isWandering && !PetMenu.IsOpen)
            BeginDrift();
    }

    private void SleepMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_isSleeping || _isGoingToSleep)
            WakeUp();
        else
            BeginSleep();
    }

    private void BeginSleep()
    {
        if (_isSleeping || _isGoingToSleep)
            return;

        _funMode.CancelCursorGrab();
        _wanderTimer.Stop();
        _destinationTimer.Stop();
        _wakeAngerTimer.Stop();
        _isWakingAngry = false;
        SetAngry(false);
        _isGoingToSleep = true;
        SleepMenuItem.Header = "wake up";
        _sleepFrom = new Point(Left, Top);
        var workArea = SystemParameters.WorkArea;
        _sleepTo = new Point(
            Math.Max(workArea.Left, workArea.Right - Width - 16),
            Math.Max(workArea.Top, workArea.Bottom - Height - 16));
        _sleepStarted = DateTime.UtcNow;
        _sleepTransitionTimer.Start();
    }

    private void SleepTransitionTimer_Tick(object? sender, EventArgs e)
    {
        const double durationMilliseconds = 1100;
        var progress = Math.Clamp((DateTime.UtcNow - _sleepStarted).TotalMilliseconds / durationMilliseconds, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        var position = Interpolate(_sleepFrom, _sleepTo, eased);
        Left = position.X;
        Top = position.Y;
        if (progress < 1)
            return;

        Left = _sleepTo.X;
        Top = _sleepTo.Y;
        _sleepTransitionTimer.Stop();
        _isGoingToSleep = false;
        _isSleeping = true;
        UpdateFace();
    }

    private void WakeUp()
    {
        if (!_isSleeping && !_isGoingToSleep)
            return;

        _sleepTransitionTimer.Stop();
        _isGoingToSleep = false;
        _isSleeping = false;
        SleepMenuItem.Header = "go to sleep";
        _isWakingAngry = true;
        SetAngry(false);
        _wakeAngerTimer.Stop();
        _wakeAngerTimer.Start();
        _wanderTimer.Stop();
        _destinationTimer.Stop();
    }

    private void WakeAngerTimer_Tick(object? sender, EventArgs e)
    {
        _wakeAngerTimer.Stop();
        _isWakingAngry = false;
        SetAngry(_isDragging || _isBeingCarried);
        if (_isWandering && !PetMenu.IsOpen)
        {
            _destinationTimer.Start();
            BeginDrift();
        }
    }

    private static Point Interpolate(Point from, Point to, double progress) =>
        new(
            from.X + (to.X - from.X) * progress,
            from.Y + (to.Y - from.Y) * progress);

    private void GooseMode_Click(object sender, RoutedEventArgs e)
    {
        if (((MenuItem)sender).IsChecked)
            _funMode.Start();
        else
            _funMode.Stop();
    }

    private void GooseDoodles_Click(object sender, RoutedEventArgs e) =>
        _funMode.DoodlesEnabled = ((MenuItem)sender).IsChecked;

    private void GooseCursorGrabs_Click(object sender, RoutedEventArgs e) =>
        _funMode.CursorGrabsEnabled = ((MenuItem)sender).IsChecked;

    private void GooseCheckInterval_Click(object sender, RoutedEventArgs e)
    {
        var selected = (MenuItem)sender;
        _funMode.CheckIntervalSeconds = ParseSeconds(selected.Header);
        SetCheckedGooseOption("check every", selected);
    }

    private void GooseIdleThreshold_Click(object sender, RoutedEventArgs e)
    {
        var selected = (MenuItem)sender;
        _funMode.IdleThresholdSeconds = ParseSeconds(selected.Header);
        SetCheckedGooseOption("require inactivity", selected);
    }

    private void SetCheckedGooseOption(string parentHeader, MenuItem selected)
    {
        var parent = FindMenuItem(PetMenu, parentHeader);
        if (parent is null)
            return;

        foreach (var item in parent.Items.OfType<MenuItem>())
            item.IsChecked = ReferenceEquals(item, selected);
    }

    private static MenuItem? FindMenuItem(ItemsControl parent, string header)
    {
        foreach (var item in parent.Items.OfType<MenuItem>())
        {
            if (Equals(item.Header, header))
                return item;

            var nested = FindMenuItem(item, header);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static int ParseSeconds(object? header)
    {
        var value = header?.ToString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(value, out var seconds)
            ? seconds
            : throw new InvalidOperationException("the selected timer option does not contain a valid duration");
    }

    private void PetMenu_Opened(object sender, RoutedEventArgs e)
    {
        _destinationTimer.Stop();
    }

    private void PetMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (_isWandering && !_isSleeping && !_isGoingToSleep && !_isWakingAngry)
        {
            _destinationTimer.Start();
            BeginDrift();
        }
    }

    private void PetWindow_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        PetMenu.PlacementTarget = PetCanvas;
        PetMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        PetMenu.IsOpen = true;
        e.Handled = true;
    }

    private void Wander_Click(object sender, RoutedEventArgs e)
    {
        _isWandering = ((MenuItem)sender).IsChecked;
        _destinationTimer.IsEnabled = _isWandering;
        if (_isWandering && !_isSleeping && !_isGoingToSleep)
            BeginDrift();
        else
            _wanderTimer.Stop();
    }

    private void NoHat_Click(object sender, RoutedEventArgs e) => SetHat("none");

    private void LittleHat_Click(object sender, RoutedEventArgs e) => SetHat("blue");

    private void GoldHat_Click(object sender, RoutedEventArgs e) => SetHat("gold");

    private void SetHat(string hat)
    {
        _hat = hat;
        foreach (var item in PetMenu.Items.OfType<MenuItem>())
        {
            if (item.Header is string header && header is "no hat" or "little hat" or "gold hat")
                item.IsChecked = header switch
                {
                    "no hat" => hat == "none",
                    "little hat" => hat == "blue",
                    "gold hat" => hat == "gold",
                    _ => false
                };
        }
        Hat.Visibility = hat == "none" ? Visibility.Collapsed : Visibility.Visible;
        Hat.Margin = new Thickness(0, _largo ? -3 : 2, 0, 0);
        SetAngry(_isAngry);
    }

    private void Largo_Click(object sender, RoutedEventArgs e)
    {
        _largo = ((MenuItem)sender).IsChecked;
        Slime.Width = _largo ? 126 : 104;
        Slime.Height = _largo ? 106 : 88;
        Slime.SetValue(Canvas.LeftProperty, _largo ? 12d : 23d);
        Slime.SetValue(Canvas.TopProperty, _largo ? 8d : 18d);
        Body.Width = _largo ? 106 : 86;
        Body.Height = _largo ? 86 : 68;
        Glow.Width = _largo ? 120 : 98;
        Glow.Height = _largo ? 96 : 76;
        Face.FontSize = _largo ? 22 : (_hat == "none" ? 18 : 15);
        Hat.Margin = new Thickness(0, _largo ? -3 : 2, 0, 0);
        Width = _largo ? 172 : 150;
        Height = _largo ? 148 : 130;
        if (_isWandering && !_isSleeping && !_isGoingToSleep)
            BeginDrift();
    }

}
