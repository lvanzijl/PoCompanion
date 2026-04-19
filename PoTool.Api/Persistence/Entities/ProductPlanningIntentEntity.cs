using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PoTool.Core.Planning;

namespace PoTool.Api.Persistence.Entities;

public class ProductPlanningIntentEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int EpicId { get; set; }

    [Required]
    public DateTime StartSprintStartDateUtc { get; set; }

    [Required]
    public int DurationInSprints { get; set; }

    public ProductPlanningRecoveryStatus? RecoveryStatus { get; set; }

    [Required]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProductId))]
    public virtual ProductEntity Product { get; set; } = null!;
}
