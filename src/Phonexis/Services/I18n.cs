using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Phonexis.Services;

/// <summary>
/// Runtime localization with live language switching.
/// Dictionaries are JSON files embedded under Resources/i18n.
/// XAML usage: Text="{Binding [KeyName], Source={x:Static services:I18n.Instance}}"
/// </summary>
public sealed class I18n : INotifyPropertyChanged
{
    public static I18n Instance { get; } = new();

    public static readonly IReadOnlyList<(string Code, string Name)> Languages = new[]
    {
        ("en", "English"),
        ("it", "Italiano"),
        ("fr", "Français"),
        ("es", "Español"),
        ("de", "Deutsch")
    };

    private Dictionary<string, string> _fallback = new();
    private Dictionary<string, string> _current = new();
    private string _language = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    private I18n()
    {
        _fallback = LoadDictionary("en") ?? new Dictionary<string, string>();
        _current = _fallback;
    }

    public string Language => _language;

    public string this[string key]
    {
        get
        {
            if (_current.TryGetValue(key, out var value)) return value;
            if (_fallback.TryGetValue(key, out var fallback)) return fallback;
            return key; // visible marker for missing translations
        }
    }

    public string Format(string key, params object[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, this[key], args); }
        catch (FormatException) { return this[key]; }
    }

    public void SetLanguage(string code)
    {
        if (code == _language && _current.Count > 0) return;
        var dict = LoadDictionary(code);
        if (dict == null) return;
        _language = code;
        _current = dict;
        // "Item[]" invalidates every indexer binding at once.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    private static Dictionary<string, string>? LoadDictionary(string code)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($"i18n.{code}.json", StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }
}
