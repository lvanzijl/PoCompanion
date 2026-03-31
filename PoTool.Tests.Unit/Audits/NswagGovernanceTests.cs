using System.Text.Json;
using System.Xml.Linq;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class NswagGovernanceTests
{
    [TestMethod]
    public void CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource()
    {
        var repositoryRoot = GetRepositoryRoot();
        var nswagFiles = Directory.GetFiles(repositoryRoot, "*.nswag", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(repositoryRoot, "nswag.json", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                Path.Combine(repositoryRoot, "PoTool.Client", "nswag.json")
            },
            nswagFiles);

        using var document = JsonDocument.Parse(File.ReadAllText(nswagFiles[0]));
        var root = document.RootElement;
        var documentSource = root.GetProperty("documentGenerator").GetProperty("fromDocument");
        Assert.AreEqual("$(InputSwaggerFile)", documentSource.GetProperty("json").GetString());

        var generator = root.GetProperty("codeGenerators").GetProperty("openApiToCSharpClient");
        Assert.AreEqual("ApiClient/Generated/ApiClient.g.cs", generator.GetProperty("output").GetString());

        var excludedTypeNames = generator.GetProperty("excludedTypeNames")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredType in new[]
                 {
                     "BuildQualityPageDto",
                     "DeliveryBuildQualityDto",
                     "PipelineBuildQualityDto",
                     "HomeProductBarMetricsDto",
                     "TfsProjectDto",
                     "ConfigurationImportResultDto",
                     "ValidationTriageSummaryDto",
                     "ValidationCategoryTriageDto",
                     "HealthWorkspaceProductSummaryDto",
                     "PortfolioSnapshotDto",
                     "PortfolioComparisonDto",
                     "PortfolioDecisionSignalDto"
                 })
        {
            Assert.Contains(requiredType, excludedTypeNames.ToList(), $"NSwag must exclude shared contract type '{requiredType}'.");
        }
    }

    [TestMethod]
    public void ClientProject_OnlyRunsNswagWhenExplicitlyRequested_AndKeepsGeneratedOutputSeparated()
    {
        var repositoryRoot = GetRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "PoTool.Client", "PoTool.Client.csproj");
        var project = XDocument.Load(projectPath);

        var nswagTarget = project.Descendants("Target")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "NSwag", StringComparison.Ordinal));
        Assert.AreEqual("'$(GenerateApiClient)' == 'true'", nswagTarget.Attribute("Condition")?.Value);

        var validateTarget = project.Descendants("Target")
            .Single(element => string.Equals(element.Attribute("Name")?.Value, "ValidateNswagGovernance", StringComparison.Ordinal));
        Assert.AreEqual("'$(GenerateApiClient)' == 'true'", validateTarget.Attribute("Condition")?.Value);

        var legacyGeneratedFile = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "ApiClient.g.cs");
        Assert.IsFalse(File.Exists(legacyGeneratedFile), "Legacy generated client location must remain empty.");

        var staleSnapshot = Path.Combine(repositoryRoot, "PoTool.Client", "swagger.json");
        Assert.IsFalse(File.Exists(staleSnapshot), "Checked-in client swagger snapshots are not allowed.");

        var governedSnapshot = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "OpenApi", "swagger.json");
        Assert.IsTrue(File.Exists(governedSnapshot), "The governed OpenAPI snapshot must live under ApiClient/OpenApi.");
    }

    [TestMethod]
    public void ManualApiClientExtensions_AreLimitedToGovernedEnvelopeWrappersAndJsonSettings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var apiClientDirectory = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient");

        var rootFiles = Directory.GetFiles(apiClientDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "ApiClient.DeliveryFilters.cs",
                "ApiClient.Extensions.cs",
                "ApiClient.LegacyCompatibility.cs",
                "ApiClient.PipelineFilters.cs",
                "ApiClient.PortfolioConsumption.cs",
                "ApiClient.PullRequestFilters.cs",
                "ApiClient.SprintFilters.cs"
            },
            rootFiles);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
