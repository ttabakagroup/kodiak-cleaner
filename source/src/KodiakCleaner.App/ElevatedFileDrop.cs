using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace KodiakCleaner.App;

/// <summary>A page that takes files dropped on Kodiak Cleaner's window (Hash Checker).</summary>
public interface IFileDropTarget
{
    Task DropAsync(IReadOnlyList<string> paths);
}

/// <summary>
/// Windows blocks drag and drop from Explorer into a program running as administrator (Explorer runs as the user, and the
/// usual drop route doesn't cross that line). Run as administrator, the main window takes the older WM_DROPFILES message
/// instead, lets it through from Explorer, and hands the files to the open page (Ted, 2026-10-23: dropping a file into Hash
/// Checker must just work). Without administrator rights nothing changes and pages use their normal drop handlers.
/// </summary>
public static class ElevatedFileDrop
{
    private const int WM_DROPFILES = 0x0233, WM_COPYDATA = 0x004A, WM_COPYGLOBALDATA = 0x0049, MSGFLT_ALLOW = 1;

    public static void Attach(Window window, Func<IFileDropTarget?> target)
    {
        if (!Elevation.IsAdministrator) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || HwndSource.FromHwnd(hwnd) is not { } source) return;
        try
        {
            // WPF's own drop target would take the drop first and then refuse it, so it is taken off; Explorer then falls back
            // to WM_DROPFILES. Kodiak Cleaner's own dragging (the sidebar) tracks the mouse itself, so it doesn't need it.
            RevokeDragDrop(hwnd);
            DragAcceptFiles(hwnd, true);
            foreach (var msg in new[] { WM_DROPFILES, WM_COPYDATA, WM_COPYGLOBALDATA })
                ChangeWindowMessageFilterEx(hwnd, (uint)msg, MSGFLT_ALLOW, IntPtr.Zero);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return; // Wine and very old systems: drops just don't arrive, as before
        }
        source.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg != WM_DROPFILES) return IntPtr.Zero;
            handled = true;
            var paths = Read(wParam);
            if (paths.Count > 0 && target() is { } page)
                window.Dispatcher.BeginInvoke(async () =>
                {
                    try { await page.DropAsync(paths); }
                    catch (Exception e) { Dialogs.Error(e.Message); }
                });
            return IntPtr.Zero;
        });
    }

    private static List<string> Read(IntPtr drop)
    {
        var paths = new List<string>();
        try
        {
            uint count = DragQueryFile(drop, 0xFFFFFFFF, null, 0);
            for (uint i = 0; i < count; i++)
            {
                uint length = DragQueryFile(drop, i, null, 0);
                var buffer = new StringBuilder((int)length + 1);
                if (DragQueryFile(drop, i, buffer, (uint)buffer.Capacity) > 0) paths.Add(buffer.ToString());
            }
        }
        finally { DragFinish(drop); }
        return paths;
    }

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr hwnd);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder? file, uint size);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr drop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr filterStatus);
}
