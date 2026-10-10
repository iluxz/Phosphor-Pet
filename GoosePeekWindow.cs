using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace PhosphorPet;

internal sealed class GoosePeekWindow : Window
{
    private static readonly (string Title, string Caption)[] Sightings =
    [
        ("field notes: corner slime", "it thinks you did not see it"),
        ("unlicensed mouse inspector", "inspection complete. probably."),
        ("rare phosphor doodle", "drawn from memory. it is very proud."),
        ("tiny snack break", "the snack is a star. do not ask.")
    ];

    private readonly DispatcherTimer _closeTimer = new()
    {
        Interval = TimeSpan.FromSeconds(4.8)
    };

    public GoosePeekWindow(Random random)
    {
        var sighting = Sightings[random.Next(Sightings.Length)];
        Title = "phosphor field notes";
        Width = 252;
        Height = 164;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;

        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, workArea.Right - Width - 20);
        Top = Math.Max(workArea.Top + 8, workArea.Bottom - Height - 20);

        var slide = new TranslateTransform(0, 14);
        Content = BuildCard(sighting.Title, sighting.Caption, random, slide);
        _closeTimer.Tick += CloseTimer_Tick;
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));
            slide.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            _closeTimer.Start();
        };
        Closed += (_, _) => _closeTimer.Stop();
    }

    private void CloseTimer_Tick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private static UIElement BuildCard(string title, string caption, Random random, TranslateTransform slide)
    {
        var shell = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(246, 15, 25, 46)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(190, 120, 180, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(100, 160, 255),
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.48
            },
            RenderTransform = slide
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(94) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        text.Children.Add(new TextBlock
        {
            Text = "PHOSPHOR / 01",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(116, 188, 255)),
            Margin = new Thickness(0, 0, 0, 7)
        });
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(235, 246, 255)),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(text, 0);
        layout.Children.Add(text);

        var doodle = BuildDoodle(random);
        Grid.SetColumn(doodle, 1);
        layout.Children.Add(doodle);

        var captionBlock = new TextBlock
        {
            Text = caption,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(166, 183, 207)),
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(captionBlock, 1);
        Grid.SetColumnSpan(captionBlock, 2);
        layout.Children.Add(captionBlock);

        shell.Child = layout;
        return shell;
    }

    private static UIElement BuildDoodle(Random random)
    {
        var canvas = new Canvas
        {
            Width = 94,
            Height = 102,
            Background = new SolidColorBrush(Color.FromArgb(32, 125, 185, 255))
        };

        var glow = new Ellipse
        {
            Width = 82,
            Height = 72,
            Fill = new SolidColorBrush(Color.FromArgb(34, 100, 160, 255))
        };
        Canvas.SetLeft(glow, 6);
        Canvas.SetTop(glow, 20);
        canvas.Children.Add(glow);

        var slime = new Ellipse
        {
            Width = 62,
            Height = 50,
            Stroke = new SolidColorBrush(Color.FromRgb(154, 216, 255)),
            StrokeThickness = 1,
            Fill = new RadialGradientBrush(
                Color.FromRgb(237, 251, 255),
                Color.FromRgb(88, 126, 255))
        };
        Canvas.SetLeft(slime, 16);
        Canvas.SetTop(slime, 34);
        canvas.Children.Add(slime);

        var eyes = new TextBlock
        {
            Text = random.Next(2) == 0 ? ".." : "^.^",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        Canvas.SetLeft(eyes, random.Next(2) == 0 ? 39 : 33);
        Canvas.SetTop(eyes, 48);
        canvas.Children.Add(eyes);

        var doodleText = new TextBlock
        {
            Text = random.Next(3) switch { 0 => "*", 1 => "~", _ => "+" },
            FontFamily = new FontFamily("Consolas"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 218, 126))
        };
        Canvas.SetLeft(doodleText, random.Next(2) == 0 ? 9 : 70);
        Canvas.SetTop(doodleText, random.Next(8, 24));
        canvas.Children.Add(doodleText);

        var hatLine = new Line
        {
            X1 = 27,
            X2 = 64,
            Y1 = 83,
            Y2 = 83,
            Stroke = new SolidColorBrush(Color.FromArgb(110, 170, 211, 255)),
            StrokeThickness = 1
        };
        canvas.Children.Add(hatLine);
        return canvas;
    }
}
