using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.Onboarding;

public sealed record LegacyProjectReference(
    string SourceLegacyReference,
    string ProjectName);

public sealed record LegacyTeamMigrationRecord(
    string SourceLegacyReference,
    int TeamId,
    string? TeamExternalId,
    string? ProjectName,
    string TeamName,
    string TeamAreaPath);

public sealed record LegacyPipelineMigrationRecord(
    string SourceLegacyReference,
    int PipelineId,
    string PipelineExternalId,
    int ProductId,
    int RepositoryId,
    string RepositoryExternalId,
    string RepositoryName,
    string PipelineName,
    string? Folder,
    string? YamlPath);

public sealed record LegacyProductRootMigrationRecord(
    string SourceLegacyReference,
    int ProductId,
    string WorkItemExternalId);

public sealed record LegacyTeamBindingMigrationRecord(
    string SourceLegacyReference,
    int ProductId,
    int TeamId);

public sealed record LegacyPipelineBindingMigrationRecord(
    string SourceLegacyReference,
    int ProductId,
    string PipelineExternalId);

public sealed record LegacyOnboardingMigrationSnapshot(
    TfsConfigEntity? Connection,
    IReadOnlyList<LegacyProjectReference> ProjectReferences,
    IReadOnlyList<LegacyTeamMigrationRecord> Teams,
    IReadOnlyList<LegacyPipelineMigrationRecord> Pipelines,
    IReadOnlyList<LegacyProductRootMigrationRecord> ProductRoots,
    IReadOnlyList<LegacyTeamBindingMigrationRecord> TeamBindings,
    IReadOnlyList<LegacyPipelineBindingMigrationRecord> PipelineBindings,
    string SourceFingerprint);

public interface IOnboardingLegacyMigrationReader
{
    Task<LegacyOnboardingMigrationSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class OnboardingLegacyMigrationReader : IOnboardingLegacyMigrationReader
{
    private readonly PoToolDbContext _dbContext;

    public OnboardingLegacyMigrationReader(PoToolDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LegacyOnboardingMigrationSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var connection = await _dbContext.TfsConfigs
            .AsNoTracking()
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var teams = await _dbContext.Teams
            .AsNoTracking()
            .OrderBy(entity => entity.Id)
            .Select(entity => new LegacyTeamMigrationRecord(
                $"TeamEntity:{entity.Id}",
                entity.Id,
                entity.TfsTeamId,
                entity.ProjectName,
                entity.TfsTeamName ?? entity.Name,
                entity.TeamAreaPath))
            .ToListAsync(cancellationToken);

        var pipelines = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .Include(entity => entity.Repository)
            .OrderBy(entity => entity.Id)
            .Select(entity => new LegacyPipelineMigrationRecord(
                $"PipelineDefinitionEntity:{entity.Id}",
                entity.Id,
                entity.PipelineDefinitionId.ToString(),
                entity.ProductId,
                entity.RepositoryId,
                entity.RepoId,
                entity.RepoName,
                entity.Name,
                entity.Folder,
                entity.YamlPath))
            .ToListAsync(cancellationToken);

        var productRoots = await _dbContext.ProductBacklogRoots
            .AsNoTracking()
            .OrderBy(entity => entity.ProductId)
            .ThenBy(entity => entity.WorkItemTfsId)
            .Select(entity => new LegacyProductRootMigrationRecord(
                $"ProductBacklogRootEntity:{entity.ProductId}:{entity.WorkItemTfsId}",
                entity.ProductId,
                entity.WorkItemTfsId.ToString()))
            .ToListAsync(cancellationToken);

        var teamBindings = await _dbContext.ProductTeamLinks
            .AsNoTracking()
            .OrderBy(entity => entity.ProductId)
            .ThenBy(entity => entity.TeamId)
            .Select(entity => new LegacyTeamBindingMigrationRecord(
                $"ProductTeamLinkEntity:{entity.ProductId}:{entity.TeamId}",
                entity.ProductId,
                entity.TeamId))
            .ToListAsync(cancellationToken);

        var pipelineBindings = pipelines
            .OrderBy(entity => entity.ProductId)
            .ThenBy(entity => entity.PipelineExternalId, StringComparer.Ordinal)
            .Select(entity => new LegacyPipelineBindingMigrationRecord(
                $"{entity.SourceLegacyReference}:binding",
                entity.ProductId,
                entity.PipelineExternalId))
            .ToArray();

        var projectReferences = BuildProjectReferences(connection, teams);
        var sourceFingerprint = ComputeFingerprint(connection, projectReferences, teams, pipelines, productRoots, teamBindings, pipelineBindings);

        return new LegacyOnboardingMigrationSnapshot(
            connection,
            projectReferences,
            teams,
            pipelines,
            productRoots,
            teamBindings,
            pipelineBindings,
            sourceFingerprint);
    }

    private static IReadOnlyList<LegacyProjectReference> BuildProjectReferences(
        TfsConfigEntity? connection,
        IReadOnlyList<LegacyTeamMigrationRecord> teams)
    {
        var references = new List<LegacyProjectReference>();

        if (!string.IsNullOrWhiteSpace(connection?.Project))
        {
            references.Add(new LegacyProjectReference("TfsConfigEntity:Project", connection.Project.Trim()));
        }

        references.AddRange(teams
            .Where(team => !string.IsNullOrWhiteSpace(team.ProjectName))
            .Select(team => new LegacyProjectReference($"{team.SourceLegacyReference}:ProjectName", team.ProjectName!.Trim())));

        return references
            .OrderBy(reference => reference.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.SourceLegacyReference, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeFingerprint(
        TfsConfigEntity? connection,
        IReadOnlyList<LegacyProjectReference> projectReferences,
        IReadOnlyList<LegacyTeamMigrationRecord> teams,
        IReadOnlyList<LegacyPipelineMigrationRecord> pipelines,
        IReadOnlyList<LegacyProductRootMigrationRecord> productRoots,
        IReadOnlyList<LegacyTeamBindingMigrationRecord> teamBindings,
        IReadOnlyList<LegacyPipelineBindingMigrationRecord> pipelineBindings)
    {
        var builder = new StringBuilder();

        if (connection is null)
        {
            builder.AppendLine("connection:none");
        }
        else
        {
            builder.Append("connection:")
                .Append(connection.Url)
                .Append('|').Append(connection.Project)
                .Append('|').Append(connection.TimeoutSeconds)
                .Append('|').Append(connection.ApiVersion)
                .Append('|').Append(connection.DefaultAreaPath)
                .Append('|').Append(connection.LastValidated?.UtcDateTime.Ticks ?? 0)
                .Append('|').Append(connection.HasTestedConnectionSuccessfully)
                .Append('|').Append(connection.HasVerifiedTfsApiSuccessfully)
                .AppendLine();
        }

        foreach (var reference in projectReferences)
        {
            builder.Append("project:")
                .Append(reference.SourceLegacyReference)
                .Append('|').Append(reference.ProjectName)
                .AppendLine();
        }

        foreach (var team in teams.OrderBy(item => item.TeamId))
        {
            builder.Append("team:")
                .Append(team.TeamId)
                .Append('|').Append(team.TeamExternalId)
                .Append('|').Append(team.ProjectName)
                .Append('|').Append(team.TeamName)
                .Append('|').Append(team.TeamAreaPath)
                .AppendLine();
        }

        foreach (var pipeline in pipelines.OrderBy(item => item.PipelineId))
        {
            builder.Append("pipeline:")
                .Append(pipeline.PipelineId)
                .Append('|').Append(pipeline.PipelineExternalId)
                .Append('|').Append(pipeline.ProductId)
                .Append('|').Append(pipeline.RepositoryId)
                .Append('|').Append(pipeline.RepositoryExternalId)
                .Append('|').Append(pipeline.RepositoryName)
                .Append('|').Append(pipeline.PipelineName)
                .Append('|').Append(pipeline.Folder)
                .Append('|').Append(pipeline.YamlPath)
                .AppendLine();
        }

        foreach (var productRoot in productRoots.OrderBy(item => item.ProductId).ThenBy(item => item.WorkItemExternalId, StringComparer.Ordinal))
        {
            builder.Append("root:")
                .Append(productRoot.ProductId)
                .Append('|').Append(productRoot.WorkItemExternalId)
                .AppendLine();
        }

        foreach (var binding in teamBindings.OrderBy(item => item.ProductId).ThenBy(item => item.TeamId))
        {
            builder.Append("team-binding:")
                .Append(binding.ProductId)
                .Append('|').Append(binding.TeamId)
                .AppendLine();
        }

        foreach (var binding in pipelineBindings.OrderBy(item => item.ProductId).ThenBy(item => item.PipelineExternalId, StringComparer.Ordinal))
        {
            builder.Append("pipeline-binding:")
                .Append(binding.ProductId)
                .Append('|').Append(binding.PipelineExternalId)
                .AppendLine();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }
}
