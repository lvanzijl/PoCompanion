using System.Text.Json;
using System.Xml.Linq;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestCategory("ApiContract")]
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
        CollectionAssert.Contains(
            generator.GetProperty("additionalNamespaceUsages").EnumerateArray().Select(value => value.GetString()).ToList(),
            "PoTool.Shared.DataState");

        var excludedTypeNames = generator.GetProperty("excludedTypeNames")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredType in GetSharedPublicTypeNames(repositoryRoot))
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
                "ApiClient.PipelineFilters.cs",
                "ApiClient.PortfolioConsumption.cs",
                "ApiClient.PullRequestFilters.cs",
                "ApiClient.SprintFilters.cs",
                "BugTriageClientServiceCollectionExtensions.cs"
            },
            rootFiles);
    }

    [TestMethod]
    public void GeneratedClient_DoesNotRecreateSharedPublicTypes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var sharedTypeNames = GetSharedPublicTypeNames(repositoryRoot);
        var generatedPath = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "Generated", "ApiClient.g.cs");
        var generatedTypeNames = GetPublicTypeNames(File.ReadAllText(generatedPath));

        var overlap = sharedTypeNames.Intersect(generatedTypeNames, StringComparer.Ordinal).OrderBy(value => value).ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), overlap, "Generated client types must not overlap with shared contract ownership.");
    }

    [TestMethod]
    public void ApiProject_HostedClientReferenceIsExplicit()
    {
        var repositoryRoot = GetRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "PoTool.Api", "PoTool.Api.csproj");
        var project = XDocument.Load(projectPath);

        var projectReferences = project.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        CollectionAssert.Contains(projectReferences, @"..\PoTool.Client\PoTool.Client.csproj");
        Assert.AreEqual(1, projectReferences.Count(reference => string.Equals(reference, @"..\PoTool.Client\PoTool.Client.csproj", StringComparison.Ordinal)));
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

    private static HashSet<string> GetSharedPublicTypeNames(string repositoryRoot)
    {
        var sharedRoot = Path.Combine(repositoryRoot, "PoTool.Shared");
        var typeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(sharedRoot, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var typeName in GetPublicTypeNames(File.ReadAllText(path)))
            {
                typeNames.Add(typeName);
            }
        }

        return typeNames;
    }

    private static IEnumerable<string> GetPublicTypeNames(string source)
    {
        return System.Text.RegularExpressions.Regex.Matches(
                source,
                @"public\s+(?:sealed\s+|partial\s+)?(?:record|class|enum|interface)\s+([A-Za-z0-9_]+)")
            .Select(match => match.Groups[1].Value);
    }
}
