using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace DesktopPet.Localization;

/// <summary>
/// UI strings from Localization/Strings.resx (English, default) and Strings.id.resx (Indonesian).
/// XAML binds through <see cref="TrExtension"/>; switching language raises an indexer change so
/// every bound text updates without a restart.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string AutoLanguage = "auto";
    public const string English = "en";
    public const string Indonesian = "id";

    /// <summary>Selectable languages with their native names (not translated).</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> Languages =
    [
        (English, "English"),
        (Indonesian, "Bahasa Indonesia"),
    ];

    private static readonly ResourceManager Resources = new("DesktopPet.Localization.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    private Loc()
    {
        Culture = ResolveCulture(AutoLanguage, CultureInfo.CurrentUICulture);
    }

    /// <summary>"auto", "en" or "id" as stored in settings.json.</summary>
    public string LanguageSetting { get; private set; } = AutoLanguage;

    public CultureInfo Culture { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed, for UI that is built in code (menus, tray).</summary>
    public event Action? LanguageChanged;

    public string this[string key] => Get(key);

    public string Get(string key) =>
        Resources.GetString(key, Culture) ?? $"[{key}]";

    public static string T(string key) => Instance.Get(key);

    public static string F(string key, params object?[] args) =>
        string.Format(Instance.Culture, Instance.Get(key), args);

    /// <summary>
    /// "auto" follows the Windows display language: Indonesian when it is Indonesian, otherwise English.
    /// </summary>
    public static CultureInfo ResolveCulture(string? setting, CultureInfo systemUiCulture)
    {
        string code = setting?.Trim().ToLowerInvariant() switch
        {
            English => English,
            Indonesian => Indonesian,
            _ => systemUiCulture.TwoLetterISOLanguageName.Equals(Indonesian, StringComparison.OrdinalIgnoreCase)
                ? Indonesian
                : English,
        };
        return CultureInfo.GetCultureInfo(code);
    }

    public void SetLanguage(string? setting)
    {
        string normalized = setting?.Trim().ToLowerInvariant() switch
        {
            English => English,
            Indonesian => Indonesian,
            _ => AutoLanguage,
        };

        var culture = ResolveCulture(normalized, CultureInfo.CurrentUICulture);
        bool changed = !culture.Equals(Culture);

        LanguageSetting = normalized;
        Culture = culture;

        if (changed)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            LanguageChanged?.Invoke();
        }
    }
}
