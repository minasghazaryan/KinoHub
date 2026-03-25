using System.Text;
using System.Text.RegularExpressions;

namespace KinoHub.Web.Services;

public static class SlugGenerator
{
    public static string GenerateSlug(string source, int? numericSuffix = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            source = "item";

        var normalized = source.Trim().ToLowerInvariant();

        var sb = new StringBuilder(normalized.Length + 16);
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (ch == ' ' || ch == '-' || ch == '_')
            {
                sb.Append('-');
            }
        }

        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "item";

        if (numericSuffix.HasValue)
            slug = $"{slug}-{numericSuffix.Value}";

        return slug;
    }
}

