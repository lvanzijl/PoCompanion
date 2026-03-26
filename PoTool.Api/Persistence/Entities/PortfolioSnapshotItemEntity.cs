using System.ComponentModel.DataAnnotations;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Durable CDC portfolio snapshot row for one exact business key.
/// </summary>
public class PortfolioSnapshotItemEntity
{
    /// <summary>
    /// Internal row identifier.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to the owning snapshot header.
    /// </summary>
    [Required]
    public long SnapshotId { get; set; }

    /// <summary>
    /// Required project-number business key.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional work-package business key.
    /// </summary>
    [MaxLength(200)]
    public string? WorkPackage { get; set; }

    /// <summary>
    /// Canonical progress ratio in the unit interval [0, 1].
    /// </summary>
    [Required]
    public double Progress { get; set; }

    /// <summary>
    /// Canonical total weight.
    /// </summary>
    [Required]
    public double TotalWeight { get; set; }

    /// <summary>
    /// Lifecycle state captured for the persisted row.
    /// </summary>
    [Required]
    public WorkPackageLifecycleState LifecycleState { get; set; }

    /// <summary>
    /// Navigation to the owning snapshot header.
    /// </summary>
    public PortfolioSnapshotEntity Snapshot { get; set; } = null!;
}
