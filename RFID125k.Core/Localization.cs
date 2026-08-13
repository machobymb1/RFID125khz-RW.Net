using System.Globalization;
using System.Text.Json;

namespace RFID125k.Core;

/// <summary>
/// Nyelvi erőforrások (többnyelvű felület).
///
/// A fordítások JSON-szótárakban vannak a program mappájában:
/// lang.hu.json, lang.en.json ... (esetleg lang/ almappában).
/// A kulcsok a Python változattal közösek; a formátumhelyettesítők
/// ({0}, {1:X4} ...) .NET (string.Format) formátumúak.
///
/// Ismeretlen/fordítatlan kulcsra az angoltól eltérő nyelveken a magyar
/// szöveg esik vissza; ha az sincs, a kulcs neve jelenik meg.
/// A beállított nyelv a program mappájában lévő config.json-ba mentődik
/// (felvevőhelyre esik vissza, ha oda nem írható).
/// </summary>
public static class Localization
{
    public static readonly string[] SupportedLanguages = ["hu", "en"];
    public static readonly string[] SupportedLanguageNames = ["Magyar", "English"];

    private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Fallback = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    public static string CurrentLanguage { get; private set; } = "hu";

    /// <summary>Nyelvváltáskor tüzel (a felület frissítéséhez).</summary>
    public static event Action? LanguageChanged;

    private static string? FindResource(string language)
    {
        string dir = AppContext.BaseDirectory;
        foreach (string name in new[] { $"lang.{language}.json", Path.Combine("lang", $"lang.{language}.json") })
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static bool TryLoad(string? path, Dictionary<string, string> target)
    {
        if (path is null)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            lock (Sync)
            {
                foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                    target[prop.Name] = prop.Value.GetString() ?? prop.Name;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>A nyelv betöltése ("hu", "en"...). Ismeretlen nyelvnél magyarra esik vissza.</summary>
    public static bool LoadLanguage(string language)
    {
        language = string.IsNullOrWhiteSpace(language) ? "hu" : language.Trim().ToLowerInvariant();
        if (language.Length != 2)
            language = "hu";

        lock (Sync)
        {
            Strings.Clear();
            Fallback.Clear();
            if (language != "hu")
                TryLoad(FindResource("hu"), Fallback);
            if (!TryLoad(FindResource(language), Strings))
            {
                language = "hu";
                TryLoad(FindResource("hu"), Strings);
            }
            CurrentLanguage = language;
        }
        LanguageChanged?.Invoke();
        return true;
    }

    /// <summary>Szöveg az aktuális nyelven, opcionális {0}, {1} ... helyettesítőkkel.</summary>
    public static string T(string key, params object?[] args)
    {
        lock (Sync)
        {
            if (!Strings.TryGetValue(key, out string? text) && !Fallback.TryGetValue(key, out text))
                text = key;
            try
            {
                return args.Length == 0
                    ? text
                    : string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }
    }

    private const string ConfigFileName = "config.json";

    private static string[] ConfigPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            Path.Combine(AppContext.BaseDirectory, ConfigFileName),
            Path.Combine(appData, "RFID125k", ConfigFileName)
        ];
    }

    /// <summary>A config.json-ból a mentett nyelv (hiány esetén "hu").</summary>
    public static string LoadConfigLanguage()
    {
        foreach (string path in ConfigPaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("language", out JsonElement el) &&
                    el.ValueKind == JsonValueKind.String)
                    return el.GetString() ?? "hu";
            }
            catch
            {
                // további helyek próbálása
            }
        }
        return "hu";
    }

    /// <summary>A nyelv mentése (config.json a program mappájába, vagy appdata-ba).</summary>
    public static void SaveConfigLanguage(string language)
    {
        string json = "{\n  \"language\": \"" + language + "\"\n}\n";
        foreach (string path in ConfigPaths())
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
                return;
            }
            catch
            {
                // következő hely próbálása
            }
        }
    }
}