using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Tests.Unit.TestSupport;

internal static class PersistenceTestGraph
{
    public const string DefaultProjectId = "test-project";

    public static ProjectEntity EnsureProject(
        PoToolDbContext context,
        string projectId = DefaultProjectId,
        string? alias = null,
        string? name = null)
    {
        var tracked = context.Projects.Local.FirstOrDefault(project => string.Equals(project.Id, projectId, StringComparison.Ordinal));
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = context.Projects.FirstOrDefault(project => project.Id == projectId);
        if (existing is not null)
        {
            return existing;
        }

        var project = new ProjectEntity
        {
            Id = projectId,
            Alias = alias ?? projectId,
            Name = name ?? projectId
        };

        context.Projects.Add(project);
        return project;
    }

    public static ProfileEntity EnsureProfile(
        PoToolDbContext context,
        int profileId,
        string? name = null)
    {
        var tracked = context.Profiles.Local.FirstOrDefault(profile => profile.Id == profileId);
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = context.Profiles.FirstOrDefault(profile => profile.Id == profileId);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var profile = new ProfileEntity
        {
            Id = profileId,
            Name = name ?? $"Profile {profileId}",
            CreatedAt = now,
            LastModified = now
        };

        context.Profiles.Add(profile);
        return profile;
    }

    public static TeamEntity EnsureTeam(
        PoToolDbContext context,
        int teamId,
        string? name = null,
        string? areaPath = null)
    {
        var tracked = context.Teams.Local.FirstOrDefault(team => team.Id == teamId);
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = context.Teams.FirstOrDefault(team => team.Id == teamId);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var team = new TeamEntity
        {
            Id = teamId,
            Name = name ?? $"Team {teamId}",
            TeamAreaPath = areaPath ?? $"\\Test\\Team {teamId}",
            CreatedAt = now,
            LastModified = now
        };

        context.Teams.Add(team);
        return team;
    }

    public static ProductEntity CreateProduct(
        int productId,
        string name,
        int? productOwnerId = null,
        string projectId = DefaultProjectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProductEntity
        {
            Id = productId,
            Name = name,
            ProductOwnerId = productOwnerId,
            ProjectId = projectId,
            CreatedAt = now,
            LastModified = now
        };
    }

    public static RepositoryEntity CreateRepository(
        int repositoryId,
        int productId,
        string name)
    {
        return new RepositoryEntity
        {
            Id = repositoryId,
            ProductId = productId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static ProductOwnerCacheStateEntity CreateCacheState(int productOwnerId)
    {
        return new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId
        };
    }
}
