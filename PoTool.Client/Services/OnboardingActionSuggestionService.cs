using System.Globalization;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class OnboardingActionSuggestionService
{
    public string GetSuggestedAction(
        OnboardingProblemScope scope,
        string title,
        string reason,
        string affectedEntity)
    {
        var normalized = Normalize($"{title} {reason} {affectedEntity}");

        if (ContainsAny(normalized, "connection required", "missing connection", "no connection"))
        {
            return "Create or select a connection";
        }

        if (ContainsAny(normalized, "permission denied", "permissions are insufficient", "insufficient permission"))
        {
            return scope == OnboardingProblemScope.Global
                ? "Grant the required connection read permissions"
                : "Resolve the missing read permissions";
        }

        if (ContainsAny(normalized, "capability denied", "required capabilities", "does not expose all required capabilities"))
        {
            return "Enable the required connection capabilities";
        }

        if (ContainsAny(normalized, "project mapping", "project not linked", "project onboarding is blocked", "project source"))
        {
            return "Link project to connection";
        }

        if (ContainsAny(normalized, "pipeline not associated", "missing pipeline", "pipeline requires attention", "pipeline source"))
        {
            return "Assign pipeline to project";
        }

        if (ContainsAny(normalized, "team requires attention", "missing team", "team source"))
        {
            return "Assign team to project";
        }

        if (ContainsAny(normalized, "missing binding", "binding requires attention", "binding"))
        {
            return "Create binding for product root";
        }

        if (ContainsAny(normalized, "root metadata", "product root", "root requires attention"))
        {
            return "Resolve product root validation issue";
        }

        if (ContainsAny(normalized, "invalid", "validation failed", "requires attention"))
        {
            return "Resolve validation issue";
        }

        return "Resolve validation issue";
    }

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));

    private static string Normalize(string value)
        => value
            .Trim()
            .ToLower(CultureInfo.InvariantCulture)
            .Replace('—', ' ')
            .Replace('-', ' ');
}
