using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed partial class TestCategoryEnforcementTests
{
    private static readonly string[] GovernedFolders =
    [
        "PoTool.Tests.Unit/Audits",
        "PoTool.Tests.Unit/Architecture"
    ];

    private static readonly Regex TestClassRegex = TestClassDeclarationRegex();
    private static readonly Regex TestMethodRegex = TestMethodDeclarationRegex();
    private static readonly Regex TestCategoryRegex = TestCategoryDeclarationRegex();

    [TestMethod]
    public void GovernanceFolders_AllTestClassesDeclareGovernanceCategory()
    {
        var violations = LoadTestClasses()
            .Where(static testClass => testClass.IsInGovernedFolder)
            .Where(static testClass => !testClass.Categories.Contains("Governance", StringComparer.Ordinal))
            .Select(static testClass => testClass.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        AssertViolations(
            violations,
            "Test classes under Audits/** and Architecture/** must declare [TestCategory(\"Governance\")] so they cannot leak into the core gate.");
    }

    [TestMethod]
    public void GovernanceNamingPatterns_DeclareGovernanceCategory_EvenOutsideGovernedFolders()
    {
        var violations = LoadTestClasses()
            .Where(static testClass => testClass.IsGovernanceStyleClass)
            .Where(static testClass => !testClass.Categories.Contains("Governance", StringComparer.Ordinal))
            .Select(static testClass => $"{testClass.RelativePath} ({testClass.ClassName})")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        AssertViolations(
            violations,
            "Governance-style test classes (*AuditTests, *GovernanceTests, *ArchitectureGuardTests) must declare [TestCategory(\"Governance\")] even when they are added outside the governed folders.");
    }

    [TestMethod]
    public void ApiContractTests_DeclareApiContractCategory()
    {
        var violations = LoadTestClasses()
            .Where(static testClass => testClass.IsApiContractClass)
            .Where(static testClass => !testClass.Categories.Contains("ApiContract", StringComparer.Ordinal))
            .Select(static testClass => $"{testClass.RelativePath} ({testClass.ClassName})")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        AssertViolations(
            violations,
            "API contract test classes must declare [TestCategory(\"ApiContract\")] so contract drift remains intentionally enforceable.");
    }

    [TestMethod]
    public void GovernedFolders_DoNotRelyOnMissingCategories()
    {
        var violations = LoadTestClasses()
            .Where(static testClass => testClass.IsInGovernedFolder)
            .Where(static testClass => !testClass.Categories.Contains("Governance", StringComparer.Ordinal))
            .Select(static testClass => $"{testClass.RelativePath} ({testClass.ClassName})")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        AssertViolations(
            violations,
            "Governance-folder tests must be explicitly categorized and must not rely on folder placement alone.");
    }

    private static IReadOnlyList<TestClassMetadata> LoadTestClasses()
    {
        var repositoryRoot = GetRepositoryRoot();
        var testsRoot = Path.Combine(repositoryRoot, "PoTool.Tests.Unit");
        var results = new List<TestClassMetadata>();

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            if (!content.Contains("[TestClass]", StringComparison.Ordinal) ||
                !TestMethodRegex.IsMatch(content))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            var isInGovernedFolder = GovernedFolders.Any(folder => relativePath.StartsWith(folder, StringComparison.Ordinal));
            var className = TestClassRegex.Match(content).Groups["name"].Value;
            var categories = TestCategoryRegex.Matches(content)
                .Select(static match => match.Groups["category"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            results.Add(new TestClassMetadata(
                relativePath,
                className,
                categories,
                isInGovernedFolder,
                IsGovernanceStyleClassName(className),
                IsApiContractClassName(className)));
        }

        return results;
    }

    private static void AssertViolations(IReadOnlyCollection<string> violations, string message)
    {
        Assert.IsEmpty(
            violations,
            message + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsGovernanceStyleClassName(string className) =>
        className.EndsWith("AuditTests", StringComparison.Ordinal) ||
        className.EndsWith("GovernanceTests", StringComparison.Ordinal) ||
        className.EndsWith("ArchitectureGuardTests", StringComparison.Ordinal);

    private static bool IsApiContractClassName(string className) =>
        className.Contains("ApiContract", StringComparison.Ordinal) ||
        (className.Contains("Nswag", StringComparison.Ordinal) &&
         className.EndsWith("GovernanceTests", StringComparison.Ordinal));

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

    [GeneratedRegex(@"class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TestClassDeclarationRegex();

    [GeneratedRegex(@"\[(?:Data)?TestMethod\]", RegexOptions.CultureInvariant)]
    private static partial Regex TestMethodDeclarationRegex();

    [GeneratedRegex(@"\[TestCategory\(""(?<category>[^""]+)""\)\]", RegexOptions.CultureInvariant)]
    private static partial Regex TestCategoryDeclarationRegex();

    private sealed record TestClassMetadata(
        string RelativePath,
        string ClassName,
        IReadOnlyCollection<string> Categories,
        bool IsInGovernedFolder,
        bool IsGovernanceStyleClass,
        bool IsApiContractClass);
}
