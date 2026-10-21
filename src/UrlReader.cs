using System.Windows;
using System.Windows.Automation;

namespace PresentationOverlay;

/// <summary>
/// Leest de URL uit de adresbalk van een Chromium-browser via UI Automation.
/// Asynchroon, want een trage browser mag de sidebar niet laten haperen. De laatst
/// gelezen URL blijft gelden als de adresbalk even niet zichtbaar is (HTML-fullscreen).
/// </summary>
public sealed class UrlReader
{
    readonly Dictionary<IntPtr, (string? Url, string Title, DateTime At)> _cache = new();
    readonly HashSet<IntPtr> _busy = new();

    public event Action? Changed;

    public string? Get(IntPtr hwnd, string title)
    {
        _cache.TryGetValue(hwnd, out var entry);
        var stale = entry.At == default || entry.Title != title || DateTime.UtcNow - entry.At > TimeSpan.FromSeconds(2);
        if (stale && _busy.Add(hwnd))
        {
            var dispatcher = Application.Current.Dispatcher;
            Task.Run(() => Read(hwnd)).ContinueWith(t => dispatcher.BeginInvoke(() =>
            {
                _busy.Remove(hwnd);
                _cache.TryGetValue(hwnd, out var prev);
                var url = t.Result ?? prev.Url;
                _cache[hwnd] = (url, title, DateTime.UtcNow);
                if (url != prev.Url) Changed?.Invoke();
            }));
        }
        return entry.Url;
    }

    static string? Read(IntPtr hwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            // Chrome: "OmniboxViewViews", Brave: "BraveOmniboxViewViews", Edge heeft een eigen variant.
            var edits = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            foreach (AutomationElement edit in edits)
            {
                if (!edit.Current.ClassName.Contains("Omnibox", StringComparison.OrdinalIgnoreCase)) continue;
                if (!edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                var value = ((ValuePattern)pattern).Current.Value;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch
        {
            // venster verdwenen of browser reageert niet: probeer bij de volgende tick opnieuw
        }
        return null;
    }
}
