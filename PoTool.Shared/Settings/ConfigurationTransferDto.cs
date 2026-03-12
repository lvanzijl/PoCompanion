using PoTool.Shared.BugTriage;

namespace PoTool.Shared.Settings;

public sealed record ConfigurationExportDto(
    string Version,
    DateTimeOffset ExportedAt,
    TfsConfigEntity? TfsConfiguration,
    SettingsDto? Settings,
    EffortEstimationSettingsDto? EffortEstimationSettings,
    IReadOnlyList<ConfigurationStateClassificationDto> StateClassifications,
    IReadOnlyList<TriageTagDto> TriageTags,
    IReadOnlyList<ProfileDto> Profiles,
    IReadOnlyList<TeamDto> Teams,
    IReadOnlyList<ProductDto> Products
);

public sealed record ConfigurationStateClassificationDto(
    string ProjectName,
    string WorkItemType,
    string StateName,
    StateClassification Classification
);

public sealed record ConfigurationImportRequest(
    string JsonContent,
    bool ValidateOnly = false,
    bool WipeExistingConfiguration = false
);

public enum ConfigurationImportEntityStatus
{
    Success,
    Skipped,
    Warning,
    Error
}

public sealed record ConfigurationImportEntityResultDto(
    string EntityType,
    string Name,
    ConfigurationImportEntityStatus Status,
    string? Message
);

public sealed record ConfigurationImportResultDto(
    bool CanImport,
    bool ImportExecuted,
    bool ExistingConfigurationDetected,
    bool RequiresDestructiveConfirmation,
    IReadOnlyList<string> ProfilesValidated,
    IReadOnlyList<string> ProfilesImported,
    IReadOnlyList<string> ExistingConfigurationSummary,
    IReadOnlyList<string> RemovedItems,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ConfigurationImportEntityResultDto> StructuredProfilesImported,
    IReadOnlyList<ConfigurationImportEntityResultDto> ProductsImported,
    IReadOnlyList<ConfigurationImportEntityResultDto> TeamsImported,
    IReadOnlyList<ConfigurationImportEntityResultDto> RepositoriesLinked,
    IReadOnlyList<ConfigurationImportEntityResultDto> GlobalSettingsApplied
);
