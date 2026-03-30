using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class DocumentationVerificationBatch6Tests
{
    private static readonly Regex DeprecatedAnalysisTerms = new(
        @"\b(OData|validator|deprecated ingestion|legacy ingestion|ingestion v1|ingestion v2|revision ingestor)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ActiveSemanticLeakageTerms = new(
        @"\b(OData|validator|legacy ingestion|revision ingestor|ingestion v1|ingestion v2)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HistoricalNote = new(
        @"historical state prior to Batch 3 cleanup|historical note|historical reference|historical context|historical artifact|legacy reference|superseded|archived",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InlineMarkdownLink = new(
        @"(?<!!)\[[^\]]+\]\(([^)]+)\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex ReferenceDefinition = new(
        @"^\s*\[([^\]]+)\]:\s*(\S+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex ReferenceUse = new(
        @"(?<!!)\[[^\]]+\]\[([^\]]+)\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownAutolink = new(
        @"<([^>\s]+)>",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void DocumentationVerification_AllMarkdownLinksAndAnchorsResolve()
    {
        var repositoryRoot = GetRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "docs");
        var brokenLinks = new List<string>();
        var activeArchiveReferences = new List<string>();
        var validatedLinks = 0;

        foreach (var path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var relativeSource = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            var content = File.ReadAllText(path);
            var referenceTargets = ReferenceDefinition
                .Matches(content)
                .Select(static match => (Key: match.Groups[1].Value, Value: match.Groups[2].Value))
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase);

            foreach (var rawTarget in EnumerateTargets(content, referenceTargets))
            {
                validatedLinks++;
                ValidateTarget(repositoryRoot, path, relativeSource, rawTarget, brokenLinks, activeArchiveReferences);
            }
        }

        Assert.IsGreaterThan(2, validatedLinks, $"Expected to validate more than 2 markdown links, but only validated {validatedLinks}.");
        Assert.IsEmpty(brokenLinks, $"Broken markdown links found:{Environment.NewLine}{string.Join(Environment.NewLine, brokenLinks)}");
        Assert.IsEmpty(activeArchiveReferences, $"Active docs must not depend on archive content:{Environment.NewLine}{string.Join(Environment.NewLine, activeArchiveReferences)}");
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

    [TestMethod]
    public void DocumentationVerification_ActiveDocumentationContainsNoSemanticLeakage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var matches = new List<string>();

        foreach (var folder in new[] { "architecture", "implementation", "rules" })
        {
            var root = Path.Combine(repositoryRoot, "docs", folder);
            foreach (var path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                var found = ActiveSemanticLeakageTerms.Matches(content).Select(static match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (found.Length == 0)
                {
                    continue;
                }

                matches.Add($"{Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/')} -> {string.Join(", ", found)}");
            }
        }

        Assert.IsEmpty(matches, $"Active documentation contains semantic leakage:{Environment.NewLine}{string.Join(Environment.NewLine, matches)}");
    }

    [TestMethod]
    public void DocumentationVerification_ArchiveDomainsAreStrictlyScoped()
    {
        var repositoryRoot = GetRepositoryRoot();
        var archiveRoot = Path.Combine(repositoryRoot, "docs", "archive");

        var subfolders = Directory
            .EnumerateDirectories(archiveRoot)
            .Select(Path.GetFileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "code-quality",
                "revision-ingestion",
                "validation"
            },
            subfolders);

        var revisionIngestionFiles = Directory
            .EnumerateFiles(Path.Combine(archiveRoot, "revision-ingestion"), "*.md", SearchOption.TopDirectoryOnly)
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
            revisionIngestionFiles);
    }

    [TestMethod]
    public void DocumentationVerification_RuleMirrorsExposeTrustClosureLanguage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var rulesRoot = Path.Combine(repositoryRoot, "docs", "rules");

        foreach (var path in Directory.EnumerateFiles(rulesRoot, "*.md", SearchOption.TopDirectoryOnly))
        {
            var mirror = File.ReadAllText(path);
            StringAssert.Contains(mirror, "No semantic interpretation is allowed.");
            StringAssert.Contains(mirror, "If a rule requires zero occurrences, zero is absolute.");
            StringAssert.Contains(mirror, "If a violation is found, it must be fixed, not justified.");
            StringAssert.Contains(mirror, "Historical leakage");
            StringAssert.Contains(mirror, "Active documentation");
            StringAssert.Contains(mirror, "Archive-only content");
            StringAssert.Contains(mirror, "../../.github/copilot-instructions.md");
        }
    }

    private static IEnumerable<string> EnumerateTargets(string content, IReadOnlyDictionary<string, string> referenceTargets)
    {
        foreach (Match match in InlineMarkdownLink.Matches(content))
        {
            yield return match.Groups[1].Value.Trim();
        }

        foreach (Match match in ReferenceUse.Matches(content))
        {
            var key = match.Groups[1].Value;
            if (!referenceTargets.TryGetValue(key, out var target))
            {
                yield return $"[missing-ref:{key}]";
                continue;
            }

            yield return target.Trim();
        }

        foreach (Match match in MarkdownAutolink.Matches(content))
        {
            var candidate = match.Groups[1].Value.Trim();
            if (candidate.StartsWith('#') || candidate.Contains(".md", StringComparison.OrdinalIgnoreCase))
            {
                yield return candidate;
            }
        }
    }

    private static void ValidateTarget(
        string repositoryRoot,
        string sourcePath,
        string relativeSource,
        string rawTarget,
        ICollection<string> brokenLinks,
        ICollection<string> activeArchiveReferences)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return;
        }

        if (rawTarget.StartsWith("[missing-ref:", StringComparison.Ordinal))
        {
            brokenLinks.Add($"{relativeSource} -> {rawTarget}");
            return;
        }

        if (rawTarget.Contains("://", StringComparison.Ordinal) || rawTarget.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = rawTarget.Split('#', 2);
        var targetPath = parts[0];
        var anchor = parts.Length == 2 ? parts[1] : string.Empty;
        var resolvedPath = string.IsNullOrEmpty(targetPath)
            ? sourcePath
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, targetPath));

        if (!File.Exists(resolvedPath))
        {
            brokenLinks.Add($"{relativeSource} -> {rawTarget}");
            return;
        }

        if (!string.IsNullOrEmpty(anchor))
        {
            var anchors = GetAnchors(resolvedPath);
            if (!anchors.Contains(NormalizeAnchor(anchor)))
            {
                brokenLinks.Add($"{relativeSource} -> {rawTarget}");
                return;
            }
        }

        var relativeTarget = Path.GetRelativePath(repositoryRoot, resolvedPath).Replace('\\', '/');
        if (!relativeSource.StartsWith("docs/archive/", StringComparison.Ordinal) &&
            relativeTarget.StartsWith("docs/archive/", StringComparison.Ordinal))
        {
            activeArchiveReferences.Add($"{relativeSource} -> {relativeTarget}");
        }
    }

    private static HashSet<string> GetAnchors(string path)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith('#'))
            {
                continue;
            }

            var heading = line.TrimStart('#', ' ').Trim();
            if (heading.Length == 0)
            {
                continue;
            }

            anchors.Add(NormalizeAnchor(heading));
        }

        return anchors;
    }

    private static string NormalizeAnchor(string value)
    {
        value = value.Trim().ToLowerInvariant();
        value = Regex.Replace(value, "[`*_]+", string.Empty, RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"[^\p{L}\p{Nd}\- ]+", string.Empty, RegexOptions.CultureInvariant);
        value = value.Replace(' ', '-');
        value = Regex.Replace(value, "-+", "-", RegexOptions.CultureInvariant);
        return value;
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
