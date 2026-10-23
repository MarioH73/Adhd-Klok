using System.Globalization;

namespace TopKlok;

internal static class AppLanguage
{
    public static bool IsEnglish =>
        !string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "nl",
            StringComparison.OrdinalIgnoreCase);

    public static string Text(string dutch, string english) => IsEnglish ? english : dutch;

    public static CultureInfo DisplayCulture =>
        CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "nl-NL");

    public static string CountryCode => RegionInfo.CurrentRegion.TwoLetterISORegionName;
}
