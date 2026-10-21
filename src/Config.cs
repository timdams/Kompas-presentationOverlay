using System.Text.Json;

namespace PresentationOverlay;

public sealed class Rule
{
    public string Label { get; set; } = "";
    public string[] Process { get; set; } = [];
    public string? ClassName { get; set; }
    public string? TitleContains { get; set; }
    public string? UrlContains { get; set; }
    /// <summary>Pad naar een afbeelding; wint van het exe-icoon.</summary>
    public string? Icon { get; set; }
    /// <summary>Pad naar de exe waar het icoon uit komt, voor als de app nog niet draait.</summary>
    public string? Exe { get; set; }
    public string? Glyph { get; set; }

    public bool NeedsUrl => UrlContains != null;

    public bool Matches(string process, string className, string title, Func<string?> url)
    {
        if (!Process.Any(p => string.Equals(Strip(p), process, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (ClassName != null && !string.Equals(ClassName, className, StringComparison.Ordinal))
            return false;
        if (TitleContains != null && !title.Contains(TitleContains, StringComparison.OrdinalIgnoreCase))
            return false;
        if (UrlContains != null)
        {
            var u = url();
            if (u == null || !u.Contains(UrlContains, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public static string Strip(string exe) =>
        exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
}

public sealed class Config
{
    public string Monitor { get; set; } = "secondary";
    public int Width { get; set; } = 260;
    /// <summary>Fullscreen-vensters (F11) verkleinen tot de ruimte naast de sidebar.</summary>
    public bool FitFullscreen { get; set; } = true;
    public string Accent { get; set; } = "#E4002B";
    public List<Rule> Items { get; set; } = [];

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Config Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return JsonSerializer.Deserialize<Config>(stream, Options) ?? new Config();
    }
}
