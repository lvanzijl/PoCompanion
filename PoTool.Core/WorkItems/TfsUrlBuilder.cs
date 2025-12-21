namespace PoTool.Core.WorkItems;

/// <summary>
/// Helper class for constructing Azure DevOps / TFS URLs for work items.
/// </summary>
public static class TfsUrlBuilder
{
    /// <summary>
    /// Builds a URL to view a work item in Azure DevOps / TFS.
    /// </summary>
    /// <param name="organizationUrl">The base Azure DevOps organization URL (e.g., "https://dev.azure.com/myorg").</param>
    /// <param name="project">The project name.</param>
    /// <param name="workItemId">The work item TFS ID.</param>
    /// <returns>A fully qualified URL to the work item in Azure DevOps.</returns>
    /// <example>
    /// BuildWorkItemUrl("https://dev.azure.com/myorg", "MyProject", 12345)
    /// returns "https://dev.azure.com/myorg/MyProject/_workitems/edit/12345"
    /// </example>
    public static string BuildWorkItemUrl(string organizationUrl, string project, int workItemId)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty", nameof(organizationUrl));
        
        if (string.IsNullOrWhiteSpace(project))
            throw new ArgumentException("Project name cannot be null or empty", nameof(project));
        
        if (workItemId <= 0)
            throw new ArgumentException("Work item ID must be greater than zero", nameof(workItemId));

        // Normalize URL - remove trailing slash if present
        var baseUrl = organizationUrl.TrimEnd('/');
        
        // Encode project name for URL
        var encodedProject = Uri.EscapeDataString(project);
        
        // Construct Azure DevOps work item URL
        // Format: {organizationUrl}/{project}/_workitems/edit/{id}
        return $"{baseUrl}/{encodedProject}/_workitems/edit/{workItemId}";
    }

    /// <summary>
    /// Builds URLs for multiple work items.
    /// </summary>
    /// <param name="organizationUrl">The base Azure DevOps organization URL.</param>
    /// <param name="project">The project name.</param>
    /// <param name="workItemIds">Collection of work item TFS IDs.</param>
    /// <returns>A list of fully qualified URLs to the work items in Azure DevOps.</returns>
    public static List<string> BuildWorkItemUrls(string organizationUrl, string project, IEnumerable<int> workItemIds)
    {
        if (workItemIds == null)
            throw new ArgumentNullException(nameof(workItemIds));

        return workItemIds
            .Select(id => BuildWorkItemUrl(organizationUrl, project, id))
            .ToList();
    }
}
