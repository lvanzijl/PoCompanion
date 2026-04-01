using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for project persistence.
/// A project groups one or more products for routing and planning context.
/// </summary>
public class ProjectEntity
{
    /// <summary>
    /// Internal project identifier.
    /// </summary>
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique alias used in routing.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable project name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Products that belong to the project.
    /// </summary>
    public virtual ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();
}
