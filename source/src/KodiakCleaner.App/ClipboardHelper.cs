using System.Runtime.InteropServices;
using System.Windows;

namespace KodiakCleaner.App;

/// <summary>
/// Copies text without crashing when another program is holding the clipboard open
/// (OpenClipboard Failed, CLIPBRD_E_CANT_OPEN). Retries for about a second, then says so plainly.
/// </summary>
public static class ClipboardHelper
{
    /// <returns>True when the text is on the clipboard.</returns>
    public static bool TryCopy(string text)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                // copy: true keeps the text after Kodiak Cleaner closes.
                Clipboard.SetDataObject(text, true);
                return true;
            }
            catch (Exception e) when (e is COMException or ExternalException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    /// <summary>Empties the clipboard (UI thread only), retrying while another program holds it. Clipboard History is untouched.</summary>
    public static bool Clear()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Clipboard.Clear();
                return true;
            }
            catch (Exception e) when (e is COMException or ExternalException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    /// <summary>Copies, and tells the person when another program kept the clipboard busy.</summary>
    public static bool CopyOrExplain(string text)
    {
        if (TryCopy(text)) return true;
        Dialogs.Error("Another program is using the clipboard right now, so nothing was copied. Close clipboard tools or remote desktop windows and try again.");
        return false;
    }
}
