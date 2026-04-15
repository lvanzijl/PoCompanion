using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public static class GlobalFilterValidationMapper
{
    public const string ProductIds = "productids";
    public const string ProjectIds = "projectids";
    public const string TeamIds = "teamids";
    public const string Time = "time";
    public const string SprintId = "sprintid";
    public const string StartSprintId = "startsprintid";
    public const string EndSprintId = "endsprintid";
    public const string RollingWindow = "rollingwindow";
    public const string RollingUnit = "rollingunit";

    public static IReadOnlyList<string> NormalizeFields(IEnumerable<string>? fields)
        => fields is null
            ? Array.Empty<string>()
            : fields
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(NormalizeField)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    public static IReadOnlyList<string> InferFieldsFromMessages(IEnumerable<string>? messages)
    {
        if (messages is null)
        {
            return Array.Empty<string>();
        }

        var inferred = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            var normalizedMessage = message?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                continue;
            }

            if (normalizedMessage.Contains("product", StringComparison.OrdinalIgnoreCase))
            {
                inferred.Add(ProductIds);
            }

            if (normalizedMessage.Contains("project", StringComparison.OrdinalIgnoreCase))
            {
                inferred.Add(ProjectIds);
            }

            if (normalizedMessage.Contains("team", StringComparison.OrdinalIgnoreCase))
            {
                inferred.Add(TeamIds);
            }

            if (normalizedMessage.Contains("sprint", StringComparison.OrdinalIgnoreCase)
                || normalizedMessage.Contains("range", StringComparison.OrdinalIgnoreCase)
                || normalizedMessage.Contains("rolling", StringComparison.OrdinalIgnoreCase)
                || normalizedMessage.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                inferred.Add(Time);
            }
        }

        return inferred.ToArray();
    }

    public static bool MatchesControl(
        IReadOnlyList<string> invalidFields,
        string controlField,
        FilterTimeMode currentTimeMode)
    {
        if (invalidFields.Count == 0)
        {
            return false;
        }

        var normalizedControl = NormalizeField(controlField);
        if (invalidFields.Contains(normalizedControl, StringComparer.Ordinal))
        {
            return true;
        }

        if (!invalidFields.Contains(Time, StringComparer.Ordinal))
        {
            return false;
        }

        return normalizedControl switch
        {
            SprintId => currentTimeMode == FilterTimeMode.Sprint,
            StartSprintId or EndSprintId => currentTimeMode == FilterTimeMode.Range,
            RollingWindow or RollingUnit => currentTimeMode == FilterTimeMode.Rolling,
            _ => false
        };
    }

    private static string NormalizeField(string field)
    {
        var normalized = field.Trim().Replace(".", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return normalized.ToLowerInvariant() switch
        {
            "productid" or "products" or "productids" => ProductIds,
            "projectid" or "projectalias" or "projectids" => ProjectIds,
            "teamid" or "teamids" => TeamIds,
            "time" or "timemode" or "timerange" or "windowstartutc" or "windowendutc" or "iterationpaths" or "sprintids" => Time,
            "sprintid" => SprintId,
            "startsprintid" or "fromsprintid" => StartSprintId,
            "endsprintid" or "tosprintid" => EndSprintId,
            "rollingwindow" => RollingWindow,
            "rollingunit" => RollingUnit,
            _ => normalized.ToLowerInvariant()
        };
    }
}
