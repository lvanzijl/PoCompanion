using System.Web;

namespace PoTool.Client.Helpers;

public sealed record WorkspaceQueryContext(
    string? ProjectAlias = null,
    int? ProductId = null,
    int? TeamId = null,
    int? SprintId = null,
    int? FromSprintId = null,
    int? ToSprintId = null);

public static class WorkspaceQueryContextHelper
{
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
            ProductId: ParseNullableInt(queryParams["productId"]),
            TeamId: ParseNullableInt(queryParams["teamId"]),
            SprintId: ParseNullableInt(queryParams["sprintId"]),
            FromSprintId: ParseNullableInt(queryParams["fromSprintId"]),
            ToSprintId: ParseNullableInt(queryParams["toSprintId"]));
    }

    public static string BuildQueryString(WorkspaceQueryContext context, string? additionalParams = null)
    {
        var parameters = new List<string>();

        AppendString(parameters, "projectAlias", context.ProjectAlias);
        AppendInt(parameters, "productId", context.ProductId);
        AppendInt(parameters, "teamId", context.TeamId);
        AppendInt(parameters, "sprintId", context.SprintId);
        AppendInt(parameters, "fromSprintId", context.FromSprintId);
        AppendInt(parameters, "toSprintId", context.ToSprintId);

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
}
