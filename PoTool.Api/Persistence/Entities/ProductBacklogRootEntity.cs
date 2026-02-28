namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for the Product backlog root work item IDs.
/// A product can have one or more backlog root work items that define its product backlog.
/// </summary>
public class ProductBacklogRootEntity
{
    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// TFS work item ID that serves as a backlog root for this product.
    /// </summary>
    public int WorkItemTfsId { get; set; }

    /// <summary>
    /// Navigation property to the Product.
    /// </summary>
    public virtual ProductEntity Product { get; set; } = null!;
}
