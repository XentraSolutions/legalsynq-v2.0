using System.Text.RegularExpressions;

namespace CareConnect.Application.Helpers;

public static partial class PhoneNumberHelper
{
    public static bool TryNormalizeOptionalUsPhone(string? phone, out string? normalizedPhone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            normalizedPhone = null;
            return true;
        }

        var digits = NonDigitRegex().Replace(phone, string.Empty);
        if (digits.Length != 10)
        {
            normalizedPhone = null;
            return false;
        }

        normalizedPhone = digits;
        return true;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
