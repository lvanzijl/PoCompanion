using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class DateTimeOffsetOrderingAuditTests
{
    private static readonly HashSet<string> IgnoredPropertyNames = new(StringComparer.Ordinal)
    {
        "EventTimestampUtc"
    };

    [TestMethod]
    public void PoToolApi_OrderClauses_DoNotSortDateTimeOffsetWithoutUtcDateTimeConversion()
    {
        var repositoryRoot = GetRepositoryRoot();
        var dateTimeOffsetPropertyNames = GetDateTimeOffsetPropertyNames(repositoryRoot);
        var violations = new List<string>();

        foreach (var path in Directory.GetFiles(Path.Combine(repositoryRoot, "PoTool.Api"), "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!ContainsOrderingMethod(line))
                {
                    continue;
                }

                if (line.Contains("UtcDateTime", StringComparison.Ordinal))
                {
                    continue;
                }

                if (dateTimeOffsetPropertyNames.Any(propertyName => Regex.IsMatch(line, $@"\b{Regex.Escape(propertyName)}\b")))
                {
                    violations.Add($"{path}:{index + 1}: {line.Trim()}");
                }
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                "DateTimeOffset ordering in PoTool.Api must convert to UtcDateTime before sorting. Violations:\n"
                + string.Join("\n", violations));
        }
    }

    private static bool ContainsOrderingMethod(string line)
        => line.Contains("OrderBy(", StringComparison.Ordinal)
           || line.Contains("OrderByDescending(", StringComparison.Ordinal)
           || line.Contains("ThenBy(", StringComparison.Ordinal)
           || line.Contains("ThenByDescending(", StringComparison.Ordinal);

    private static HashSet<string> GetDateTimeOffsetPropertyNames(string repositoryRoot)
    {
        var sourceRoots = new[]
        {
            Path.Combine(repositoryRoot, "PoTool.Api"),
            Path.Combine(repositoryRoot, "PoTool.Shared"),
            Path.Combine(repositoryRoot, "PoTool.Core")
        };

        var propertyNamePattern = new Regex(
            @"\b(?:public|internal|protected|private)\s+(?:required\s+)?(?:virtual\s+)?DateTimeOffset\??\s+([A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled);

        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in sourceRoots)
        {
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in propertyNamePattern.Matches(File.ReadAllText(path)))
                {
                    var propertyName = match.Groups[1].Value;
                    if (!IgnoredPropertyNames.Contains(propertyName))
                    {
                        result.Add(propertyName);
                    }
                }
            }
        }

        return result;
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
