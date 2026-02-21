using System.ComponentModel.DataAnnotations;

namespace PoTool.Shared.Settings;

public class TfsConfigEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Use default Windows credentials (NTLM authentication).
    /// Always true - NTLM is the only supported authentication mode.
    /// </summary>
    public bool UseDefaultCredentials { get; set; } = true;

    /// <summary>
    /// HTTP timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// TFS API version (e.g., "7.0" for Azure DevOps Server 2022).
    /// </summary>
    [MaxLength(16)]
    public string ApiVersion { get; set; } = "7.0";

    /// <summary>
    /// Analytics OData base URL for revision ingestion.
    /// </summary>
    [MaxLength(1024)]
    public string AnalyticsODataBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Analytics OData entity-set path (for example: WorkItemRevisions).
    /// </summary>
    [MaxLength(256)]
    public string AnalyticsODataEntitySetPath { get; set; } = AnalyticsODataDefaults.EntitySetPath;

    /// <summary>
    /// Default Area Path for work item queries.
    /// This is the AreaPath used in WIQL queries (e.g., "ProjectName\Team A").
    /// Required field - must be configured before sync operations.
    /// </summary>
    [MaxLength(512)]
    public string DefaultAreaPath { get; set; } = string.Empty;

    /// <summary>
    /// Last time the connection was validated successfully.
    /// </summary>
    public DateTimeOffset? LastValidated { get; set; }

    /// <summary>
    /// Whether Test Connection has succeeded at least once.
    /// Set to true when ValidateConnection succeeds.
    /// </summary>
    public bool HasTestedConnectionSuccessfully { get; set; } = false;

    /// <summary>
    /// Whether Verify TFS API has passed (all checks).
    /// Set to true only when ALL verification checks pass.
    /// </summary>
    public bool HasVerifiedTfsApiSuccessfully { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
