using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

public class TfsConfigEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Project { get; set; } = string.Empty;

    [Required]
    public string ProtectedPat { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
