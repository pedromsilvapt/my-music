namespace MyMusic.Common;

/// <summary>
/// The set of UI languages supported by the application.
/// Used to validate the <see cref="Entities.User.Language"/> value on patch requests.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    /// The default language applied to new users and used before the user's
    /// stored preference loads.
    /// </summary>
    public const string Default = "en";

    /// <summary>
    /// All supported language codes. Keep in sync with the client's
    /// <c>SUPPORTED_LANGUAGES</c> list in <c>src/locales/index.ts</c>.
    /// </summary>
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "pt",
    };

    /// <summary>
    /// Returns <see langword="true"/> when the given code is a supported language.
    /// Comparison is ordinal-ignore-case.
    /// </summary>
    public static bool Contains(string? code) =>
        !string.IsNullOrEmpty(code) && Codes.Contains(code);
}