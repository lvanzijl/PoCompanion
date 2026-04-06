using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Onboarding;

public sealed record OnboardingMigrationMappingContext(
    string SourceLegacyReference,
    string TargetEntityType,
    string? TargetExternalIdentity);

public sealed record MappedOnboardingEntity<T>(
    T Entity,
    OnboardingMigrationMappingContext Context);

public interface IOnboardingMigrationMapper
{
    MappedOnboardingEntity<TfsConnection> MapConnection(TfsConfigEntity connection);
    MappedOnboardingEntity<ProjectSource> MapProject(LegacyProjectReference reference, ProjectLookupResultDto project);
    MappedOnboardingEntity<TeamSource> MapTeam(LegacyTeamMigrationRecord team, string projectExternalId);
    MappedOnboardingEntity<PipelineSource> MapPipeline(LegacyPipelineMigrationRecord pipeline, string projectExternalId);
    MappedOnboardingEntity<ProductRoot> MapProductRoot(LegacyProductRootMigrationRecord productRoot, string projectExternalId);
    MappedOnboardingEntity<ProductSourceBinding> MapProjectBinding(LegacyProductRootMigrationRecord productRoot, ProductRoot targetRoot, ProjectSource projectSource);
    MappedOnboardingEntity<ProductSourceBinding> MapTeamBinding(LegacyTeamBindingMigrationRecord binding, ProductRoot targetRoot, ProjectSource projectSource, TeamSource teamSource);
    MappedOnboardingEntity<ProductSourceBinding> MapPipelineBinding(LegacyPipelineBindingMigrationRecord binding, ProductRoot targetRoot, ProjectSource projectSource, PipelineSource pipelineSource);
}

public sealed class OnboardingMigrationMapper : IOnboardingMigrationMapper
{
    public MappedOnboardingEntity<TfsConnection> MapConnection(TfsConfigEntity connection)
        => new(
            new TfsConnection
            {
                ConnectionKey = "connection",
                OrganizationUrl = connection.Url.Trim(),
                AuthenticationMode = connection.UseDefaultCredentials ? "Ntlm" : "Pat",
                TimeoutSeconds = connection.TimeoutSeconds,
                ApiVersion = connection.ApiVersion.Trim(),
                LastSuccessfulValidationAtUtc = connection.LastValidated?.UtcDateTime,
                LastAttemptedValidationAtUtc = connection.LastValidated?.UtcDateTime ?? connection.UpdatedAtUtc,
                LastVerifiedCapabilitiesSummary = connection.HasVerifiedTfsApiSuccessfully
                    ? "Legacy configuration indicated TFS API verification success."
                    : null
            },
            new OnboardingMigrationMappingContext("TfsConfigEntity", nameof(TfsConnection), "connection"));

    public MappedOnboardingEntity<ProjectSource> MapProject(LegacyProjectReference reference, ProjectLookupResultDto project)
        => new(
            new ProjectSource
            {
                ProjectExternalId = project.ProjectExternalId,
                Enabled = true,
                Snapshot = new ProjectSnapshot
                {
                    ProjectExternalId = project.ProjectExternalId,
                    Name = project.Name,
                    Description = project.Description,
                    Metadata = CreateMetadata()
                }
            },
            new OnboardingMigrationMappingContext(reference.SourceLegacyReference, nameof(ProjectSource), project.ProjectExternalId));

    public MappedOnboardingEntity<TeamSource> MapTeam(LegacyTeamMigrationRecord team, string projectExternalId)
        => new(
            new TeamSource
            {
                TeamExternalId = team.TeamExternalId?.Trim() ?? string.Empty,
                Enabled = true,
                Snapshot = new TeamSnapshot
                {
                    TeamExternalId = team.TeamExternalId?.Trim() ?? string.Empty,
                    ProjectExternalId = projectExternalId,
                    Name = team.TeamName.Trim(),
                    DefaultAreaPath = team.TeamAreaPath.Trim(),
                    Metadata = CreateMetadata()
                }
            },
            new OnboardingMigrationMappingContext(team.SourceLegacyReference, nameof(TeamSource), team.TeamExternalId?.Trim()));

    public MappedOnboardingEntity<PipelineSource> MapPipeline(LegacyPipelineMigrationRecord pipeline, string projectExternalId)
        => new(
            new PipelineSource
            {
                PipelineExternalId = pipeline.PipelineExternalId,
                Enabled = true,
                Snapshot = new PipelineSnapshot
                {
                    PipelineExternalId = pipeline.PipelineExternalId,
                    ProjectExternalId = projectExternalId,
                    Name = pipeline.PipelineName.Trim(),
                    Folder = pipeline.Folder?.Trim(),
                    YamlPath = pipeline.YamlPath?.Trim(),
                    RepositoryExternalId = pipeline.RepositoryExternalId.Trim(),
                    RepositoryName = pipeline.RepositoryName.Trim(),
                    Metadata = CreateMetadata()
                }
            },
            new OnboardingMigrationMappingContext(pipeline.SourceLegacyReference, nameof(PipelineSource), pipeline.PipelineExternalId));

    public MappedOnboardingEntity<ProductRoot> MapProductRoot(LegacyProductRootMigrationRecord productRoot, string projectExternalId)
        => new(
            new ProductRoot
            {
                WorkItemExternalId = productRoot.WorkItemExternalId,
                Enabled = true,
                Snapshot = new ProductRootSnapshot
                {
                    WorkItemExternalId = productRoot.WorkItemExternalId,
                    ProjectExternalId = projectExternalId,
                    Metadata = CreateMetadata()
                }
            },
            new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProductRoot), productRoot.WorkItemExternalId));

    public MappedOnboardingEntity<ProductSourceBinding> MapProjectBinding(
        LegacyProductRootMigrationRecord productRoot,
        ProductRoot targetRoot,
        ProjectSource projectSource)
        => new(
            new ProductSourceBinding
            {
                ProductRootId = targetRoot.Id,
                ProjectSourceId = projectSource.Id,
                ProductRoot = targetRoot,
                ProjectSource = projectSource,
                SourceType = ProductSourceType.Project,
                SourceExternalId = projectSource.ProjectExternalId,
                Enabled = true
            },
            new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProductSourceBinding), projectSource.ProjectExternalId));

    public MappedOnboardingEntity<ProductSourceBinding> MapTeamBinding(
        LegacyTeamBindingMigrationRecord binding,
        ProductRoot targetRoot,
        ProjectSource projectSource,
        TeamSource teamSource)
        => new(
            new ProductSourceBinding
            {
                ProductRootId = targetRoot.Id,
                ProjectSourceId = projectSource.Id,
                TeamSourceId = teamSource.Id,
                ProductRoot = targetRoot,
                ProjectSource = projectSource,
                TeamSource = teamSource,
                SourceType = ProductSourceType.Team,
                SourceExternalId = teamSource.TeamExternalId,
                Enabled = true
            },
            new OnboardingMigrationMappingContext(binding.SourceLegacyReference, nameof(ProductSourceBinding), teamSource.TeamExternalId));

    public MappedOnboardingEntity<ProductSourceBinding> MapPipelineBinding(
        LegacyPipelineBindingMigrationRecord binding,
        ProductRoot targetRoot,
        ProjectSource projectSource,
        PipelineSource pipelineSource)
        => new(
            new ProductSourceBinding
            {
                ProductRootId = targetRoot.Id,
                ProjectSourceId = projectSource.Id,
                PipelineSourceId = pipelineSource.Id,
                ProductRoot = targetRoot,
                ProjectSource = projectSource,
                PipelineSource = pipelineSource,
                SourceType = ProductSourceType.Pipeline,
                SourceExternalId = pipelineSource.PipelineExternalId,
                Enabled = true
            },
            new OnboardingMigrationMappingContext(binding.SourceLegacyReference, nameof(ProductSourceBinding), pipelineSource.PipelineExternalId));

    private static OnboardingSnapshotMetadata CreateMetadata()
    {
        var utcNow = DateTime.UtcNow;
        return new OnboardingSnapshotMetadata
        {
            ConfirmedAtUtc = utcNow,
            LastSeenAtUtc = utcNow,
            IsCurrent = true,
            RenameDetected = false,
            StaleReason = null
        };
    }
}
