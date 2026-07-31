using System.Globalization;
using System.Text.RegularExpressions;

namespace Ocelot.Extensions;

public static class StringEx
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return textInfo.ToTitleCase(input.ToLowerInvariant());
    }


    public static string ToSnakeCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = Regex.Replace(input, @"[\s\-]+", "_");
        result = Regex.Replace(result, @"([a-z0-9])([A-Z])", "$1_$2");

        return result.ToLowerInvariant();
    }
}
