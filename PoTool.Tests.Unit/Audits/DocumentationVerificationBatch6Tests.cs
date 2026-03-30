using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class DocumentationVerificationBatch6Tests
{
    private static readonly Regex DeprecatedAnalysisTerms = new(
        @"\b(OData|validator|deprecated ingestion|legacy ingestion|ingestion v1|ingestion v2|revision ingestor)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HistoricalNote = new(
        @"historical state prior to Batch 3 cleanup|historical note|historical reference|historical context|historical artifact|legacy reference|superseded|archived",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownLink = new(
        @"(?<!!)(?:\[[^\]]+\]\(([^)#]+(?:\.md)?)(#[^)]+)?\)|<([^>]+\.md(?:#[^>]+)?)>)",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void DocumentationVerification_AllRelativeMarkdownLinksResolve()
    {
        var repositoryRoot = GetRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "docs");
        var brokenLinks = new List<string>();

        foreach (var path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var sourceDirectory = Path.GetDirectoryName(path)!;
            var relativeSource = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            var content = File.ReadAllText(path);

            foreach (Match match in MarkdownLink.Matches(content))
            {
                var rawTarget = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                if (string.IsNullOrWhiteSpace(rawTarget) || rawTarget.StartsWith('#') || rawTarget.StartsWith('/'))
                {
                    continue;
                }

                if (rawTarget.Contains("://", StringComparison.Ordinal))
                {
                    continue;
                }

                var targetPath = rawTarget.Split('#', 2)[0];
                var resolved = Path.GetFullPath(Path.Combine(sourceDirectory, targetPath));
                if (!File.Exists(resolved))
                {
                    brokenLinks.Add($"{relativeSource} -> {rawTarget}");
                }
            }
        }

        Assert.IsEmpty(brokenLinks, $"Broken markdown links found:{Environment.NewLine}{string.Join(Environment.NewLine, brokenLinks)}");
    }

    [TestMethod]
    public void DocumentationVerification_AnalysisFilesWithLegacyTermsCarryHistoricalNote()
    {
        var repositoryRoot = GetRepositoryRoot();
        var analysisRoot = Path.Combine(repositoryRoot, "docs", "analysis");
        var missingNotes = new List<string>();

        foreach (var path in Directory.EnumerateFiles(analysisRoot, "*.md", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            if (!DeprecatedAnalysisTerms.IsMatch(content))
            {
                continue;
            }

            var leadingWindow = content.Length <= 1200 ? content : content[..1200];
            if (!HistoricalNote.IsMatch(leadingWindow))
            {
                missingNotes.Add(Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'));
            }
        }

        Assert.IsEmpty(missingNotes, $"Analysis files with legacy terms must carry a historical note:{Environment.NewLine}{string.Join(Environment.NewLine, missingNotes)}");
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
