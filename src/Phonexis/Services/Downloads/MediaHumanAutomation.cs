using System.Diagnostics;
using System.Windows.Automation;

namespace Phonexis.Services.Downloads;

/// <summary>
/// Presses MediaHuman YouTube Downloader's "Paste link" button through UI Automation, so the links Phonexis put
/// on the clipboard are added in one go. The button is the leftmost one of the top toolbar, which keeps this
/// independent of MediaHuman's UI language. Never touches anything else (trial notice, start buttons).
/// </summary>
public static class MediaHumanAutomation
{
    private const string ProcessName = "YouTubeDownloader";

    public static async Task<bool> TryPasteLinksAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var window = FindMainWindow();
            var button = window != null ? FindPasteButton(window) : null;
            if (button != null && button.Current.IsEnabled &&
                button.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
            {
                try
                {
                    ((InvokePattern)pattern).Invoke();
                    return true;
                }
                catch (ElementNotEnabledException)
                {
                    // A modal notice is in front; keep waiting until it is gone or we time out.
                }
            }
            await Task.Delay(700, ct);
        }
        return false;
    }

    private static AutomationElement? FindMainWindow()
    {
        var process = Process.GetProcessesByName(ProcessName)
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero &&
                                 p.MainWindowTitle.StartsWith("MediaHuman", StringComparison.OrdinalIgnoreCase));
        return process == null ? null : AutomationElement.FromHandle(process.MainWindowHandle);
    }

    private static AutomationElement? FindPasteButton(AutomationElement window)
    {
        var toolbars = window.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar));

        // The main toolbar sits at the top; the status bar toolbar lives inside a StatusBar, not as a direct child.
        var top = toolbars.Cast<AutomationElement>()
            .OrderBy(t => t.Current.BoundingRectangle.Top)
            .FirstOrDefault();
        if (top == null) return null;

        return top.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>()
            .Where(b => !string.IsNullOrEmpty(b.Current.Name))
            .OrderBy(b => b.Current.BoundingRectangle.Left)
            .FirstOrDefault();
    }
}
