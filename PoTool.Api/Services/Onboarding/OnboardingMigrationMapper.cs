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
    MappedOnboardingEntity<TfsConnection> MapConnection(TfsConfigEntity connection, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<ProjectSource> MapProject(LegacyProjectReference reference, ProjectLookupResultDto project, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<TeamSource> MapTeam(LegacyTeamMigrationRecord team, string projectExternalId, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<PipelineSource> MapPipeline(LegacyPipelineMigrationRecord pipeline, string projectExternalId, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<ProductRoot> MapProductRoot(LegacyProductRootMigrationRecord productRoot, string projectExternalId, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<ProductSourceBinding> MapProjectBinding(LegacyProductRootMigrationRecord productRoot, ProductRoot targetRoot, ProjectSource projectSource, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<ProductSourceBinding> MapTeamBinding(LegacyTeamBindingMigrationRecord binding, ProductRoot targetRoot, ProjectSource projectSource, TeamSource teamSource, DateTime migrationTimestampUtc);
    MappedOnboardingEntity<ProductSourceBinding> MapPipelineBinding(LegacyPipelineBindingMigrationRecord binding, ProductRoot targetRoot, ProjectSource projectSource, PipelineSource pipelineSource, DateTime migrationTimestampUtc);
}

public sealed class OnboardingMigrationMapper : IOnboardingMigrationMapper
{
    public MappedOnboardingEntity<TfsConnection> MapConnection(TfsConfigEntity connection, DateTime migrationTimestampUtc)
    {
        var entity = new TfsConnection
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
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<TfsConnection>(
            entity,
            new OnboardingMigrationMappingContext("TfsConfigEntity", nameof(TfsConnection), "connection"));
    }

    public MappedOnboardingEntity<ProjectSource> MapProject(LegacyProjectReference reference, ProjectLookupResultDto project, DateTime migrationTimestampUtc)
    {
        var entity = new ProjectSource
        {
            ProjectExternalId = project.ProjectExternalId,
            Enabled = true,
            Snapshot = new ProjectSnapshot
            {
                ProjectExternalId = project.ProjectExternalId,
                Name = project.Name,
                Description = project.Description,
                Metadata = CreateMetadata(migrationTimestampUtc)
            }
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<ProjectSource>(
            entity,
            new OnboardingMigrationMappingContext(reference.SourceLegacyReference, nameof(ProjectSource), project.ProjectExternalId));
    }

    public MappedOnboardingEntity<TeamSource> MapTeam(LegacyTeamMigrationRecord team, string projectExternalId, DateTime migrationTimestampUtc)
    {
        var entity = new TeamSource
        {
            TeamExternalId = team.TeamExternalId?.Trim() ?? string.Empty,
            Enabled = true,
            Snapshot = new TeamSnapshot
            {
                TeamExternalId = team.TeamExternalId?.Trim() ?? string.Empty,
                ProjectExternalId = projectExternalId,
                Name = team.TeamName.Trim(),
                DefaultAreaPath = team.TeamAreaPath.Trim(),
                Metadata = CreateMetadata(migrationTimestampUtc)
            }
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<TeamSource>(
            entity,
            new OnboardingMigrationMappingContext(team.SourceLegacyReference, nameof(TeamSource), team.TeamExternalId?.Trim()));
    }

    public MappedOnboardingEntity<PipelineSource> MapPipeline(LegacyPipelineMigrationRecord pipeline, string projectExternalId, DateTime migrationTimestampUtc)
    {
        var entity = new PipelineSource
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
                Metadata = CreateMetadata(migrationTimestampUtc)
            }
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<PipelineSource>(
            entity,
            new OnboardingMigrationMappingContext(pipeline.SourceLegacyReference, nameof(PipelineSource), pipeline.PipelineExternalId));
    }

    public MappedOnboardingEntity<ProductRoot> MapProductRoot(LegacyProductRootMigrationRecord productRoot, string projectExternalId, DateTime migrationTimestampUtc)
    {
        var entity = new ProductRoot
        {
            WorkItemExternalId = productRoot.WorkItemExternalId,
            Enabled = true,
            Snapshot = new ProductRootSnapshot
            {
                WorkItemExternalId = productRoot.WorkItemExternalId,
                ProjectExternalId = projectExternalId,
                Metadata = CreateMetadata(migrationTimestampUtc)
            }
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<ProductRoot>(
            entity,
            new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProductRoot), productRoot.WorkItemExternalId));
    }

    public MappedOnboardingEntity<ProductSourceBinding> MapProjectBinding(
        LegacyProductRootMigrationRecord productRoot,
        ProductRoot targetRoot,
        ProjectSource projectSource,
        DateTime migrationTimestampUtc)
    {
        var entity = new ProductSourceBinding
        {
            ProductRootId = targetRoot.Id,
            ProjectSourceId = projectSource.Id,
            ProductRoot = targetRoot,
            ProjectSource = projectSource,
            SourceType = ProductSourceType.Project,
            SourceExternalId = projectSource.ProjectExternalId,
            Enabled = true
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<ProductSourceBinding>(
            entity,
            new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProductSourceBinding), projectSource.ProjectExternalId));
    }

    public MappedOnboardingEntity<ProductSourceBinding> MapTeamBinding(
        LegacyTeamBindingMigrationRecord binding,
        ProductRoot targetRoot,
        ProjectSource projectSource,
        TeamSource teamSource,
        DateTime migrationTimestampUtc)
    {
        var entity = new ProductSourceBinding
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
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<ProductSourceBinding>(
            entity,
            new OnboardingMigrationMappingContext(binding.SourceLegacyReference, nameof(ProductSourceBinding), teamSource.TeamExternalId));
    }

    public MappedOnboardingEntity<ProductSourceBinding> MapPipelineBinding(
        LegacyPipelineBindingMigrationRecord binding,
        ProductRoot targetRoot,
        ProjectSource projectSource,
        PipelineSource pipelineSource,
        DateTime migrationTimestampUtc)
    {
        var entity = new ProductSourceBinding
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
        };

        StampEntity(entity, migrationTimestampUtc);

        return new MappedOnboardingEntity<ProductSourceBinding>(
            entity,
            new OnboardingMigrationMappingContext(binding.SourceLegacyReference, nameof(ProductSourceBinding), pipelineSource.PipelineExternalId));
    }

    private static OnboardingSnapshotMetadata CreateMetadata(DateTime migrationTimestampUtc)
        => new()
        {
            ConfirmedAtUtc = migrationTimestampUtc,
            LastSeenAtUtc = migrationTimestampUtc,
            IsCurrent = true,
            RenameDetected = false,
            StaleReason = null
        };

    private static void StampEntity(OnboardingEntityBase entity, DateTime migrationTimestampUtc)
    {
        entity.CreatedAtUtc = migrationTimestampUtc;
        entity.UpdatedAtUtc = migrationTimestampUtc;
    }
}
