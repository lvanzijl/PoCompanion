namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class CanonicalFilterMetadataNoticeAuditTests
{
    [TestMethod]
    public void CanonicalFilterMetadataNotice_UsesClosestMatchingContextMicrocopy()
    {
        var content = File.ReadAllText(GetComponentPath());

        StringAssert.Contains(content, "Applied filter scope differs from the request");
        StringAssert.Contains(content, "Closest matching sprint context shown");
        StringAssert.Contains(content, "Closest matching context shown");
        Assert.IsFalse(
            content.Contains("Requested ≠ applied", StringComparison.Ordinal),
            "The material-difference chip should use explanatory microcopy instead of symbolic requested/applied wording.");
    }

    private static string GetComponentPath()
        => Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Components", "Common", "CanonicalFilterMetadataNotice.razor");

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
