namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for Product-Team many-to-many relationship.
/// </summary>
public class ProductTeamLinkEntity
{
    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Foreign key to the Team.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// Navigation property to the Product.
    /// </summary>
    public virtual ProductEntity Product { get; set; } = null!;

    /// <summary>
    /// Navigation property to the Team.
    /// </summary>
    public virtual TeamEntity Team { get; set; } = null!;
}
