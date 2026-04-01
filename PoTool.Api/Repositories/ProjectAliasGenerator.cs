using System.Globalization;
using System.Text;

namespace PoTool.Api.Repositories;

internal static class ProjectAliasGenerator
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "project";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasDash = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasDash = false;
                continue;
            }

            if (previousWasDash)
            {
                continue;
            }

            builder.Append('-');
            previousWasDash = true;
        }

        var alias = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(alias) ? "project" : alias;
    }

    public static async Task<string> GenerateUniqueAliasAsync(
        string? name,
        Func<string, Task<bool>> aliasExistsAsync)
    {
        var baseAlias = Normalize(name);
        if (!await aliasExistsAsync(baseAlias))
        {
            return baseAlias;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseAlias}-{suffix}";
            if (!await aliasExistsAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique project alias.");
    }
}
