using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

public class ActivityEventLedgerEntryEntity
{
    [Key]
    public int Id { get; set; }

    public int ProductOwnerId { get; set; }
    public int WorkItemId { get; set; }
    public int UpdateId { get; set; }

    [MaxLength(256)]
    public string FieldRefName { get; set; } = string.Empty;

    public DateTimeOffset EventTimestamp { get; set; }
    public DateTime EventTimestampUtc { get; set; }

    [MaxLength(500)]
    public string? IterationPath { get; set; }

    public int? ParentId { get; set; }
    public int? FeatureId { get; set; }
    public int? EpicId { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
