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
    bool ValidateOnly = false
);

public sealed record ConfigurationImportResultDto(
    bool CanImport,
    bool ImportExecuted,
    IReadOnlyList<string> ProfilesValidated,
    IReadOnlyList<string> ProfilesImported,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors
);
