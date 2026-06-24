using System.Text.RegularExpressions;

namespace CareConnect.Application.Helpers;

public static partial class PostalCodeHelper
{
    public static bool IsValidOptionalUsZip(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return true;

        return UsZipRegex().IsMatch(postalCode.Trim());
    }

    [GeneratedRegex(@"^\d{5}(-\d{4})?$")]
    private static partial Regex UsZipRegex();
}
