using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingSnapshotMapper
{
    ProjectSnapshotDto MapProjectSnapshot(ProjectLookupResultDto project, DateTime utcNow);
    TeamSnapshotDto MapTeamSnapshot(TeamLookupResultDto team, DateTime utcNow);
    PipelineSnapshotDto MapPipelineSnapshot(PipelineLookupResultDto pipeline, DateTime utcNow);
    ProductRootSnapshotDto MapProductRootSnapshot(WorkItemLookupResultDto workItem, DateTime utcNow);
}

public sealed class OnboardingSnapshotMapper : IOnboardingSnapshotMapper
{
    public ProjectSnapshotDto MapProjectSnapshot(ProjectLookupResultDto project, DateTime utcNow)
        => new(
            project.ProjectExternalId,
            project.Name,
            project.Description,
            CreateMetadata(utcNow));

    public TeamSnapshotDto MapTeamSnapshot(TeamLookupResultDto team, DateTime utcNow)
        => new(
            team.TeamExternalId,
            team.ProjectExternalId,
            team.Name,
            team.DefaultAreaPath,
            team.Description,
            CreateMetadata(utcNow));

    public PipelineSnapshotDto MapPipelineSnapshot(PipelineLookupResultDto pipeline, DateTime utcNow)
        => new(
            pipeline.PipelineExternalId,
            pipeline.ProjectExternalId,
            pipeline.Name,
            pipeline.Folder,
            pipeline.YamlPath,
            pipeline.RepositoryExternalId,
            pipeline.RepositoryName,
            CreateMetadata(utcNow));

    public ProductRootSnapshotDto MapProductRootSnapshot(WorkItemLookupResultDto workItem, DateTime utcNow)
        => new(
            workItem.WorkItemExternalId,
            workItem.Title,
            workItem.WorkItemType,
            workItem.State,
            workItem.ProjectExternalId,
            workItem.AreaPath,
            CreateMetadata(utcNow));

    private static SnapshotMetadataDto CreateMetadata(DateTime utcNow)
        => new(utcNow, utcNow, true, false, null);
}
