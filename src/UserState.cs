using System.Text.Json;
using System.Text.Json.Serialization;

namespace PresentationOverlay;

/// <summary>
/// Wat de gebruiker in de app zelf instelt: volgorde van de tegels, extra apps en een paar
/// instellingen. Staat bewust buiten config.json (die blijft met de hand bewerkbaar) en
/// buiten bin/ (overleeft een herbouw). Verborgen tegels worden niet bewaard: bij elke
/// start staan alle tegels er weer.
/// </summary>
public sealed class UserState
{
    /// <summary>Labels in de gewenste volgorde; onbekende labels komen achteraan.</summary>
    public List<string> Order { get; set; } = [];
    public List<Rule> Extras { get; set; } = [];

    // Null = de waarde uit config.json gebruiken.
    public string? Monitor { get; set; }
    public int? Width { get; set; }
    public bool? FitFullscreen { get; set; }
    public string? Accent { get; set; }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PresentationOverlay", "user.json");

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static UserState Load()
    {
        try
        {
            if (!File.Exists(DefaultPath)) return new UserState();
            using var stream = File.OpenRead(DefaultPath);
            return JsonSerializer.Deserialize<UserState>(stream, Options) ?? new UserState();
        }
        catch
        {
            return new UserState();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
            File.WriteAllText(DefaultPath, JsonSerializer.Serialize(this, Options));
        }
        catch { /* niet bewaren is vervelend, maar mag de presentatie niet storen */ }
    }
}
