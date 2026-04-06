using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Services.Onboarding;

internal static class OnboardingReadQueries
{
    internal static IQueryable<TfsConnection> ActiveConnections(PoToolDbContext dbContext)
        => dbContext.OnboardingTfsConnections.AsNoTracking();

    internal static IQueryable<ProjectSource> ActiveProjects(PoToolDbContext dbContext)
        => dbContext.OnboardingProjectSources
            .AsNoTracking()
            .Where(project => ActiveConnections(dbContext)
                .Any(connection => connection.Id == project.TfsConnectionId));

    internal static IQueryable<TeamSource> ActiveTeams(PoToolDbContext dbContext)
        => dbContext.OnboardingTeamSources
            .AsNoTracking()
            .Where(team => ActiveProjects(dbContext)
                .Any(project => project.Id == team.ProjectSourceId));

    internal static IQueryable<PipelineSource> ActivePipelines(PoToolDbContext dbContext)
        => dbContext.OnboardingPipelineSources
            .AsNoTracking()
            .Where(pipeline => ActiveProjects(dbContext)
                .Any(project => project.Id == pipeline.ProjectSourceId));

    internal static IQueryable<ProductRoot> ActiveRoots(PoToolDbContext dbContext)
        => dbContext.OnboardingProductRoots
            .AsNoTracking()
            .Where(root => ActiveProjects(dbContext)
                .Any(project => project.Id == root.ProjectSourceId));

    internal static IQueryable<ProductSourceBinding> ActiveBindings(PoToolDbContext dbContext)
    {
        var projects = ActiveProjects(dbContext);
        var teams = ActiveTeams(dbContext);
        var pipelines = ActivePipelines(dbContext);
        var roots = ActiveRoots(dbContext);

        var projectBindings = dbContext.OnboardingProductSourceBindings
            .AsNoTracking()
            .Where(binding => binding.SourceType == ProductSourceType.Project)
            .Where(binding => roots.Any(root => root.Id == binding.ProductRootId && root.ProjectSourceId == binding.ProjectSourceId))
            .Where(binding => projects.Any(project => project.Id == binding.ProjectSourceId && project.ProjectExternalId == binding.SourceExternalId));

        var teamBindings = dbContext.OnboardingProductSourceBindings
            .AsNoTracking()
            .Where(binding => binding.SourceType == ProductSourceType.Team)
            .Where(binding => binding.TeamSourceId.HasValue)
            .Where(binding => roots.Any(root => root.Id == binding.ProductRootId && root.ProjectSourceId == binding.ProjectSourceId))
            .Where(binding => projects.Any(project => project.Id == binding.ProjectSourceId))
            .Where(binding => projectBindings.Any(projectBinding => projectBinding.ProductRootId == binding.ProductRootId && projectBinding.ProjectSourceId == binding.ProjectSourceId))
            .Where(binding => teams.Any(team =>
                team.Id == binding.TeamSourceId &&
                team.ProjectSourceId == binding.ProjectSourceId &&
                team.TeamExternalId == binding.SourceExternalId));

        var pipelineBindings = dbContext.OnboardingProductSourceBindings
            .AsNoTracking()
            .Where(binding => binding.SourceType == ProductSourceType.Pipeline)
            .Where(binding => binding.PipelineSourceId.HasValue)
            .Where(binding => roots.Any(root => root.Id == binding.ProductRootId && root.ProjectSourceId == binding.ProjectSourceId))
            .Where(binding => projects.Any(project => project.Id == binding.ProjectSourceId))
            .Where(binding => projectBindings.Any(projectBinding => projectBinding.ProductRootId == binding.ProductRootId && projectBinding.ProjectSourceId == binding.ProjectSourceId))
            .Where(binding => pipelines.Any(pipeline =>
                pipeline.Id == binding.PipelineSourceId &&
                pipeline.ProjectSourceId == binding.ProjectSourceId &&
                pipeline.PipelineExternalId == binding.SourceExternalId));

        return projectBindings
            .Concat(teamBindings)
            .Concat(pipelineBindings);
    }
}
