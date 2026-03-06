using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity that stores the link between a Pull Request and a Work Item.
/// Used by PR Delivery Insights to classify PRs and trace them to Features and Epics.
/// </summary>
public class PullRequestWorkItemLinkEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// TFS/Azure DevOps pull request ID.
    /// </summary>
    [Required]
    public int PullRequestId { get; set; }

    /// <summary>
    /// TFS work item ID linked to the pull request.
    /// </summary>
    [Required]
    public int WorkItemId { get; set; }
}
