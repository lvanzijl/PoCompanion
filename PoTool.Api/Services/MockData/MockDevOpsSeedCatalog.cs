using System.Security.Cryptography;
using System.Text;

using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services.MockData;

internal static class MockDevOpsSeedCatalog
{
    private static readonly MockRepositorySeed[] RepositorySeeds =
    [
        new("Incident Response Control", "Battleship-Incident-Backend"),
        new("Crew Safety Operations", "Battleship-CrewSafety-UI"),
        new("Damage Control Platform", "Battleship-DamageControl-Backend"),
        new("Predictive Maintenance Insights", "Battleship-Maintenance-Analytics"),
        new("Communication & Coordination", "Battleship-Coordination-UI"),
        new("Portfolio Reporting", "Battleship-Portfolio-Reporting")
    ];

    public static IReadOnlyList<string> RepositoryNames => RepositorySeeds
        .Select(seed => seed.Name)
        .ToArray();

    public static IReadOnlyList<string> GetRepositoryNamesForProduct(string productName)
    {
        return RepositorySeeds
            .Where(seed => string.Equals(seed.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            .Select(seed => seed.Name)
            .ToArray();
    }

    public static string CreateRepositoryId(string repositoryName)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(repositoryName));
        return new Guid(hash).ToString();
    }

    public static IReadOnlyList<PipelineDefinitionDto> GetPipelineDefinitionsForRepository(
        string repositoryName,
        IReadOnlyList<PipelineDto> pipelines,
        DateTimeOffset syncedAt)
    {
        var repositoryIndex = RepositoryNames
            .Select((name, index) => new { name, index })
            .FirstOrDefault(item => string.Equals(item.name, repositoryName, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (!repositoryIndex.HasValue)
        {
            return [];
        }

        var repositoryCount = RepositoryNames.Count;
        var repositoryId = CreateRepositoryId(repositoryName);

        return pipelines
            .Select((pipeline, index) => new { pipeline, index })
            .Where(item => item.index % repositoryCount == repositoryIndex.Value)
            .Select(item => new PipelineDefinitionDto
            {
                PipelineDefinitionId = item.pipeline.Id,
                RepoId = repositoryId,
                RepoName = repositoryName,
                Name = item.pipeline.Name,
                YamlPath = $"/pipelines/{item.pipeline.Name.ToLowerInvariant().Replace('.', '-').Replace(' ', '-')}.yml",
                Folder = item.pipeline.Path,
                Url = $"https://dev.azure.com/mock/{repositoryName}/_build?definitionId={item.pipeline.Id}",
                DefaultBranch = "refs/heads/main",
                LastSyncedUtc = syncedAt
            })
            .ToArray();
    }

    private sealed record MockRepositorySeed(string ProductName, string Name);
}
