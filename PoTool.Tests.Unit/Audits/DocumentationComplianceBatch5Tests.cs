using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class DocumentationComplianceBatch5Tests
{
    private static readonly string[] CanonicalFolders =
    [
        "analysis",
        "architecture",
        "archive",
        "implementation",
        "reports",
        "rules"
    ];

    [TestMethod]
    public void DocumentationCompliance_DocsRootContainsOnlyReadmeMarkdown()
    {
        var repositoryRoot = GetRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "docs");

        var markdownFiles = Directory
            .EnumerateFiles(docsRoot, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "README.md" }, markdownFiles);
    }

    [TestMethod]
    public void DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames()
    {
        var repositoryRoot = GetRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "docs");
        var kebabCase = new Regex("^[a-z0-9-]+\\.md$", RegexOptions.CultureInvariant);

        foreach (var path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

            if (relativePath == "docs/README.md")
            {
                continue;
            }

            var segments = relativePath.Split('/');
            Assert.IsGreaterThanOrEqualTo(3, segments.Length, $"Unexpected docs path depth: {relativePath}");
            Assert.IsTrue(CanonicalFolders.Contains(segments[1], StringComparer.Ordinal), $"Non-canonical docs folder: {relativePath}");
            Assert.IsTrue(kebabCase.IsMatch(Path.GetFileName(path)), $"Non-kebab-case markdown filename: {relativePath}");
        }
    }

    [TestMethod]
    public void DocumentationCompliance_ReportFilesUseDatedNaming()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportsRoot = Path.Combine(repositoryRoot, "docs", "reports");
        var datedReport = new Regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}-[a-z0-9-]+\\.md$", RegexOptions.CultureInvariant);

        foreach (var path in Directory.EnumerateFiles(reportsRoot, "*.md", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            Assert.IsTrue(datedReport.IsMatch(fileName), $"Non-dated report filename: {fileName}");
        }
    }

    [TestMethod]
    public void DocumentationCompliance_ActiveFoldersDoNotReferenceDeprecatedIngestionTerminology()
    {
        var repositoryRoot = GetRepositoryRoot();
        var deprecatedTerms = new[]
        {
            "RealOData",
            "TfsRetrievalValidator",
            "validator tooling",
            "deprecated ingestion",
            "legacy ingestion"
        };

        foreach (var folder in new[] { "architecture", "implementation", "rules" })
        {
            var root = Path.Combine(repositoryRoot, "docs", folder);
            foreach (var path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                foreach (var term in deprecatedTerms)
                {
                    Assert.IsFalse(
                        content.Contains(term, StringComparison.OrdinalIgnoreCase),
                        $"Deprecated term '{term}' found in active docs file {Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/')}");
                }
            }
        }
    }

    [TestMethod]
    public void DocumentationCompliance_LegacyRevisionIngestionArchiveContainsOnlyApprovedArtifacts()
    {
        var repositoryRoot = GetRepositoryRoot();
        var archiveRoot = Path.Combine(repositoryRoot, "docs", "archive", "revision-ingestion");

        var files = Directory
            .EnumerateFiles(archiveRoot, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "odata-ingestion-fix-plan.md",
                "odata-validator-vs-ingestion-report.md",
                "real-revision-tfsclient-pagination-review.md",
                "revision-ingestion-api-vs-validator-odata-divergence.md",
                "revision-ingestor-v2.md"
            },
            files);
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
