using System.Web;
using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public sealed record WorkspaceQueryContext(
    string? ProjectAlias = null,
    string? ProjectId = null,
    int? ProductId = null,
    int? TeamId = null,
    int? SprintId = null,
    int? FromSprintId = null,
    int? ToSprintId = null,
    FilterTimeMode? TimeMode = null,
    int? RollingWindow = null,
    FilterTimeUnit? RollingUnit = null);

public static class WorkspaceQueryContextHelper
{
    private static readonly HashSet<string> FilterQueryKeys =
    [
        "projectAlias",
        "projectId",
        "productId",
        "teamId",
        "sprintId",
        "fromSprintId",
        "toSprintId",
        "timeMode",
        "rollingWindow",
        "rollingUnit"
    ];

    public static WorkspaceQueryContext Parse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return new WorkspaceQueryContext();
        }

        var absoluteUri = Uri.TryCreate(uri, UriKind.Absolute, out var parsedAbsolute)
            ? parsedAbsolute
            : new Uri(new Uri("http://localhost"), uri.StartsWith('/') ? uri : $"/{uri}");

        var queryParams = HttpUtility.ParseQueryString(absoluteUri.Query);
        return new WorkspaceQueryContext(
            ProjectAlias: queryParams["projectAlias"],
            ProjectId: queryParams["projectId"],
            ProductId: ParseNullableInt(queryParams["productId"]),
            TeamId: ParseNullableInt(queryParams["teamId"]),
            SprintId: ParseNullableInt(queryParams["sprintId"]),
            FromSprintId: ParseNullableInt(queryParams["fromSprintId"]),
            ToSprintId: ParseNullableInt(queryParams["toSprintId"]),
            TimeMode: ParseEnum<FilterTimeMode>(queryParams["timeMode"]),
            RollingWindow: ParseNullableInt(queryParams["rollingWindow"]),
            RollingUnit: ParseEnum<FilterTimeUnit>(queryParams["rollingUnit"]));
    }

    public static string CreateRouteSignature(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return string.Empty;
        }

        var absoluteUri = Uri.TryCreate(uri, UriKind.Absolute, out var parsedAbsolute)
            ? parsedAbsolute
            : new Uri(new Uri("http://localhost"), uri.StartsWith('/') ? uri : $"/{uri}");

        var queryParams = HttpUtility.ParseQueryString(absoluteUri.Query);
        var normalizedQuery = queryParams.AllKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(key => (queryParams.GetValues(key!) ?? [string.Empty])
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => $"{key}={value}"))
            .ToArray();

        var normalizedPath = absoluteUri.AbsolutePath.Trim('/').ToLowerInvariant();
        return normalizedQuery.Length == 0
            ? normalizedPath
            : $"{normalizedPath}?{string.Join("&", normalizedQuery)}";
    }

    public static bool AreEquivalentRoutes(string? left, string? right)
        => string.Equals(CreateRouteSignature(left), CreateRouteSignature(right), StringComparison.Ordinal);

    public static string? ExtractAdditionalParameters(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var absoluteUri = Uri.TryCreate(uri, UriKind.Absolute, out var parsedAbsolute)
            ? parsedAbsolute
            : new Uri(new Uri("http://localhost"), uri.StartsWith('/') ? uri : $"/{uri}");

        var queryParams = HttpUtility.ParseQueryString(absoluteUri.Query);
        var additionalParameters = queryParams.AllKeys
            .Where(key => !string.IsNullOrWhiteSpace(key) && !FilterQueryKeys.Contains(key!))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(key => (queryParams.GetValues(key!) ?? [string.Empty])
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => $"{key}={HttpUtility.UrlEncode(value)}"))
            .ToArray();

        return additionalParameters.Length == 0
            ? null
            : string.Join("&", additionalParameters);
    }

    public static string BuildQueryString(WorkspaceQueryContext context, string? additionalParams = null)
    {
        var parameters = new List<string>();

        AppendString(parameters, "projectAlias", context.ProjectAlias);
        AppendString(parameters, "projectId", context.ProjectId);
        AppendInt(parameters, "productId", context.ProductId);
        AppendInt(parameters, "teamId", context.TeamId);
        AppendInt(parameters, "sprintId", context.SprintId);
        AppendInt(parameters, "fromSprintId", context.FromSprintId);
        AppendInt(parameters, "toSprintId", context.ToSprintId);
        AppendEnum(parameters, "timeMode", context.TimeMode);
        AppendInt(parameters, "rollingWindow", context.RollingWindow);
        AppendEnum(parameters, "rollingUnit", context.RollingUnit);

        if (!string.IsNullOrWhiteSpace(additionalParams))
        {
            parameters.Add(additionalParams.TrimStart('?'));
        }

        return parameters.Count > 0
            ? $"?{string.Join("&", parameters)}"
            : string.Empty;
    }

    public static string BuildRoute(string route, WorkspaceQueryContext context, string? additionalParams = null)
        => $"{route}{BuildQueryString(context, additionalParams)}";

    private static int? ParseNullableInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;

    private static void AppendInt(ICollection<string> parameters, string key, int? value)
    {
        if (value.HasValue)
        {
            parameters.Add($"{key}={value.Value}");
        }
    }

    private static void AppendString(ICollection<string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{key}={HttpUtility.UrlEncode(value)}");
        }
    }

    private static void AppendEnum<TEnum>(ICollection<string> parameters, string key, TEnum? value)
        where TEnum : struct
    {
        if (value.HasValue)
        {
            parameters.Add($"{key}={value.Value}");
        }
    }
}
