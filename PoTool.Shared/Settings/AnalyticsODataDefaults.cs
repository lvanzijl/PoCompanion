namespace PoTool.Shared.Settings;

public static class AnalyticsODataDefaults
{
    public const string EntitySetPath = "WorkItemRevisions";
    public const string VersionPath = "_odata/v3.0-preview";

    public static string BuildBaseUrl(string? url, string? project)
    {
        var trimmedUrl = (url ?? string.Empty).Trim().TrimEnd('/');
        var trimmedProject = (project ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl) || string.IsNullOrWhiteSpace(trimmedProject))
        {
            return string.Empty;
        }

        return $"{trimmedUrl}/{Uri.EscapeDataString(trimmedProject)}/{VersionPath}";
    }
}
