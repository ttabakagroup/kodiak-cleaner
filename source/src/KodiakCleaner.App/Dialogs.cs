using System.Windows;

namespace KodiakCleaner.App;

/// <summary>
/// Every message Kodiak Cleaner shows. They use the themed <see cref="MessageWindow"/>, and fall back to the plain Windows box
/// only when the theme isn't loaded yet (an error very early during start-up).
/// </summary>
public static class Dialogs
{
    // No on the left, Yes on the right as the default (Enter) button; Esc or closing the window still means No (Ted, 0.8.50).
    private static readonly string[] YesNo = { "No", "Yes" };

    private static int Show(MessageKind kind, string heading, string message, string? caution, string[] buttons, int defaultButton, int cancelButton)
    {
        var app = Application.Current;
        if (app is null || !app.Resources.Contains("AccentButton"))
            return Fallback(kind, heading, message + (caution is null ? "" : "\n\n" + caution), buttons, defaultButton, cancelButton);
        if (!app.Dispatcher.CheckAccess())
            return app.Dispatcher.Invoke(() => Show(kind, heading, message, caution, buttons, defaultButton, cancelButton));
        return MessageWindow.Show(kind, heading, message, caution, buttons, defaultButton, cancelButton);
    }

    private static int Fallback(MessageKind kind, string heading, string message, string[] buttons, int defaultButton, int cancelButton)
    {
        var icon = kind switch
        {
            MessageKind.Warning or MessageKind.Problem => MessageBoxImage.Warning,
            MessageKind.Info => MessageBoxImage.Information,
            _ => MessageBoxImage.Question,
        };
        var set = buttons.Length switch { 1 => MessageBoxButton.OK, 2 => MessageBoxButton.YesNo, _ => MessageBoxButton.YesNoCancel };
        // The Windows box has its own button order, so match ours by label; custom labels map to Yes, No, Cancel in order.
        MessageBoxResult[] byPosition = buttons.Length == 1 ? [MessageBoxResult.OK] : [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel];
        var map = buttons.Select((label, n) => label switch
        {
            "Yes" => MessageBoxResult.Yes,
            "No" => MessageBoxResult.No,
            "Cancel" => MessageBoxResult.Cancel,
            "OK" => MessageBoxResult.OK,
            _ => byPosition[n],
        }).ToArray();
        var r = MessageBox.Show(message, heading, set, icon, map[defaultButton]);
        var i = Array.IndexOf(map, r);
        return i < 0 ? cancelButton : i;
    }

    public static bool Confirm(string message, string? title = null) =>
        Show(MessageKind.Question, title ?? "Please Confirm", message, null, YesNo, 1, 0) == 1;

    /// <summary>
    /// A Yes/No question for steps that need care, with its <paramref name="caution"/> in a highlighted box. Yes is the default
    /// (Ted, 0.8.50) and shown as the risky choice; Esc or closing the window means No.
    /// </summary>
    public static bool Warn(string message, string? title = null, string? caution = null) =>
        Show(MessageKind.Warning, title ?? "Caution", message, caution, YesNo, 1, 0) == 1;

    /// <returns>true = Yes, false = No, null = Cancel.</returns>
    public static bool? YesNoCancel(string message, string? title = null) =>
        Show(MessageKind.Question, title ?? "Please Choose", message, null, new[] { "No", "Yes", "Cancel" }, 1, 2) switch
        {
            1 => true,
            0 => false,
            _ => null,
        };

    /// <summary>A question with its own button labels. Returns the index of the button clicked (<paramref name="cancelButton"/> if closed).</summary>
    public static int Choose(MessageKind kind, string heading, string message, string? caution, string[] buttons, int defaultButton, int cancelButton) =>
        Show(kind, heading, message, caution, buttons, defaultButton, cancelButton);

    public static void Info(string message, string? title = null) =>
        Show(MessageKind.Info, title ?? AppInfo.Name, message, null, new[] { "OK" }, 0, 0);

    public static void Error(string message, string? title = null) =>
        Show(MessageKind.Problem, title ?? "Something Needs Attention", message, null, new[] { "OK" }, 0, 0);
}
