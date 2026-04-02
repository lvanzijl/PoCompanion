namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class TestCategoryEnforcementTests
{
    [TestMethod]
    public void GovernanceFolders_AllTestClassesDeclareGovernanceCategory()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var relativeFolder in new[] { "PoTool.Tests.Unit/Audits", "PoTool.Tests.Unit/Architecture" })
        {
            var folder = Path.Combine(repositoryRoot, relativeFolder);
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                if (!content.Contains("[TestClass]", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!content.Contains("[TestCategory(\"Governance\")]", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'));
                }
            }
        }

        Assert.IsFalse(
            violations.Any(),
            "Test classes under Audits/** and Architecture/** must declare [TestCategory(\"Governance\")] so they cannot leak into the core gate." +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void NswagGovernanceTests_DeclareApiContractCategory()
    {
        var repositoryRoot = GetRepositoryRoot();
        var path = Path.Combine(repositoryRoot, "PoTool.Tests.Unit", "Audits", "NswagGovernanceTests.cs");
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "[TestCategory(\"Governance\")]");
        StringAssert.Contains(content, "[TestCategory(\"ApiContract\")]");
    }

    [TestMethod]
    public void GovernanceFolders_DoNotRelyOnMissingCategories()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var relativeFolder in new[] { "PoTool.Tests.Unit/Audits", "PoTool.Tests.Unit/Architecture" })
        {
            var folder = Path.Combine(repositoryRoot, relativeFolder);
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                if (!content.Contains("[TestMethod]", StringComparison.Ordinal))
                {
                    continue;
                }

                var hasGovernanceCategory = content.Contains("[TestCategory(\"Governance\")]", StringComparison.Ordinal);
                var hasTestClass = content.Contains("[TestClass]", StringComparison.Ordinal);
                if (!hasGovernanceCategory || !hasTestClass)
                {
                    violations.Add(Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'));
                }
            }
        }

        Assert.IsFalse(
            violations.Any(),
            "Governance-folder tests must be explicitly categorized and must not rely on folder placement alone." +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
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
