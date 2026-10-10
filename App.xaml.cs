using System.Windows;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;

namespace PhosphorPet;

public partial class App : System.Windows.Application
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "PhosphorPet";
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private Forms.ToolStripMenuItem? _launchOnStartupItem;
    private Icon? _trayImage;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _launchOnStartupItem = new Forms.ToolStripMenuItem("launch on startup")
        {
            CheckOnClick = true,
            Checked = IsLaunchOnStartupEnabled()
        };
        _launchOnStartupItem.Click += LaunchOnStartup_Click;

        var exitItem = new Forms.ToolStripMenuItem("exit phosphor pet");
        exitItem.Click += (_, _) => Shutdown();

        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add(_launchOnStartupItem);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(exitItem);
        _trayImage = CreateTrayImage();
        _trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _trayMenu,
            Icon = _trayImage,
            Text = "phosphor pet",
            Visible = true
        };

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _trayMenu?.Dispose();
        _trayMenu = null;
        _trayImage?.Dispose();
        _trayImage = null;
        base.OnExit(e);
    }

    private void LaunchOnStartup_Click(object? sender, EventArgs e)
    {
        if (_launchOnStartupItem is null)
            return;

        try
        {
            SetLaunchOnStartup(_launchOnStartupItem.Checked);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
                                          SecurityException or IOException or InvalidOperationException)
        {
            _launchOnStartupItem.Checked = !_launchOnStartupItem.Checked;
            WpfMessageBox.Show(
                $"could not update the startup setting\n\n{exception.Message}",
                "phosphor pet",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private static bool IsLaunchOnStartupEnabled()
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath);
        var registeredCommand = startupKey?.GetValue(
            StartupValueName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return string.Equals(registeredCommand, GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SetLaunchOnStartup(bool enabled)
    {
        if (enabled)
        {
            using var startupKey = Registry.CurrentUser.CreateSubKey(StartupRegistryPath, true)
                ?? throw new InvalidOperationException("could not open the current user's startup settings");
            startupKey.SetValue(StartupValueName, GetStartupCommand(), RegistryValueKind.String);
            return;
        }

        using var existingKey = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true);
        existingKey?.DeleteValue(StartupValueName, false);
    }

    private static string GetStartupCommand()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("could not determine the phosphor pet executable path");
        return $"\"{executablePath}\"";
    }

    private static Icon CreateTrayImage()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var glow = new SolidBrush(Color.FromArgb(90, 73, 143, 255));
            graphics.FillEllipse(glow, 1, 3, 30, 27);

            var bodyBounds = new Rectangle(5, 6, 22, 20);
            using var body = new LinearGradientBrush(
                bodyBounds,
                Color.FromArgb(239, 255, 255, 255),
                Color.FromArgb(65, 66, 117, 255),
                45f);
            graphics.FillEllipse(body, bodyBounds);

            using var outline = new Pen(Color.FromArgb(220, 154, 216, 255), 1f);
            graphics.DrawEllipse(outline, bodyBounds);

            using var eyes = new SolidBrush(Color.FromArgb(245, 232, 255, 255));
            graphics.FillEllipse(eyes, 11, 14, 2, 3);
            graphics.FillEllipse(eyes, 19, 14, 2, 3);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporaryIcon = Icon.FromHandle(handle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
