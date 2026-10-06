using System.Windows;
using WinForms = System.Windows.Forms;

namespace KodiakCleaner.App;

/// <summary>"Minimize to System Tray" and "Close to System Tray": hide the window and show an icon by the clock.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Window _window;
    private WinForms.NotifyIcon? _icon;

    public TrayIcon(Window window)
    {
        _window = window;
        _window.StateChanged += OnStateChanged;
        _window.Closing += OnClosing;
    }

    private bool _exiting;
    private bool _toldAboutTray;

    // Close to tray: the close button hides the window; Exit in the tray menu really quits.
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting || !Settings.Current.CloseToTray || Application.Current.Dispatcher.HasShutdownStarted) return;
        e.Cancel = true;
        Show();
        _window.Hide();
        if (!_toldAboutTray && _icon is not null)
        {
            _toldAboutTray = true;
            _icon.ShowBalloonTip(4000, AppInfo.Name, $"{AppInfo.Name} is still running here. Right-click the icon and choose Exit to quit.", WinForms.ToolTipIcon.None);
        }
    }

    private WindowState _restoreState = WindowState.Normal;

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState != WindowState.Minimized) _restoreState = _window.WindowState; // comes back maximized if it was
        if (_window.WindowState != WindowState.Minimized || !Settings.Current.MinimizeToTray) return;
        Show();
        _window.Hide();
    }

    private void Show()
    {
        if (_icon is not null) { _icon.Visible = true; return; }
        _icon = new WinForms.NotifyIcon
        {
            Text = AppInfo.Name,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Integration.ExePath),
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Items.Add($"Open {AppInfo.Name}", null, (_, _) => Restore());
        _icon.ContextMenuStrip.Items.Add("Run Cleaner Now", null, (_, _) => Task.Run(AutoClean.Run));
        _icon.ContextMenuStrip.Items.Add(new WinForms.ToolStripSeparator());
        _icon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => { _exiting = true; Application.Current.Shutdown(); });
        _icon.DoubleClick += (_, _) => Restore();
    }

    public void Restore()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = _restoreState;
        _window.Activate();
        if (_icon is not null) _icon.Visible = false;
    }

    public void Dispose()
    {
        _window.StateChanged -= OnStateChanged;
        _window.Closing -= OnClosing;
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
