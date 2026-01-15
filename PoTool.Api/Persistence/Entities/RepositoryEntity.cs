using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for Repository persistence.
/// A Repository represents a Git repository in Azure DevOps/TFS associated with a Product.
/// </summary>
public class RepositoryEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Repository name as used in Azure DevOps/TFS.
    /// This is the primary identifier used in TFS API calls.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this repository configuration was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to the Product.
    /// </summary>
    public virtual ProductEntity Product { get; set; } = null!;
}
