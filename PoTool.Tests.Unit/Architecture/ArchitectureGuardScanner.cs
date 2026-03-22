using System.Text;
using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Architecture;

internal static class ArchitectureGuardScanner
{
    private static readonly string[] IncludedExtensions =
    [
        ".cs",
        ".razor"
    ];

    private static readonly string[] ExcludedPathTokens =
    [
        "/ApiClient/",
        "/bin/",
        "/docs/",
        "/obj/",
        "/test/",
        "/tests/",
        "/wwwroot/"
    ];

    public static IReadOnlyList<ArchitectureGuardViolation> FindViolations(string relativeRoot, ArchitectureGuardRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeRoot);
        ArgumentNullException.ThrowIfNull(rule);

        var repositoryRoot = GetRepositoryRoot();
        var scanRoot = Path.Combine(repositoryRoot, relativeRoot);

        if (!Directory.Exists(scanRoot))
        {
            throw new DirectoryNotFoundException($"Could not locate scan root '{relativeRoot}' under the repository root.");
        }

        var violations = new List<ArchitectureGuardViolation>();

        foreach (var filePath in EnumerateSourceFiles(scanRoot))
        {
            var content = File.ReadAllText(filePath);
            var relativePath = NormalizeRelativePath(repositoryRoot, filePath);

            foreach (var pattern in rule.Patterns)
            {
                foreach (Match match in pattern.Regex.Matches(content))
                {
                    if (!match.Success)
                    {
                        continue;
                    }

                    violations.Add(CreateViolation(relativePath, content, pattern.Description, match));
                }
            }
        }

        return violations
            .OrderBy(violation => violation.RelativePath, StringComparer.Ordinal)
            .ThenBy(violation => violation.LineNumber)
            .ThenBy(violation => violation.PatternDescription, StringComparer.Ordinal)
            .ToArray();
    }

    public static string FormatFailureMessage(ArchitectureGuardRule rule, IReadOnlyList<ArchitectureGuardViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(violations);

        var builder = new StringBuilder();
        builder.AppendLine(rule.Why);
        builder.AppendLine("The following PoTool.Client source matches must be removed:");

        foreach (var fileGroup in violations.GroupBy(violation => violation.RelativePath, StringComparer.Ordinal))
        {
            builder.Append("- ");
            builder.AppendLine(fileGroup.Key);

            foreach (var violation in fileGroup)
            {
                builder.Append("  line ");
                builder.Append(violation.LineNumber);
                builder.Append(" — ");
                builder.AppendLine(violation.PatternDescription);
                builder.AppendLine(IndentSnippet(violation.Snippet, "    "));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(filePath);
            if (!IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedPath = filePath.Replace('\\', '/');
            if (normalizedPath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                || ExcludedPathTokens.Any(token => normalizedPath.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return filePath;
        }
    }

    private static ArchitectureGuardViolation CreateViolation(
        string relativePath,
        string content,
        string patternDescription,
        Match match)
    {
        var lineNumber = GetLineNumber(content, match.Index);
        var snippet = ExtractSnippet(content, match.Index, match.Length);

        return new ArchitectureGuardViolation(relativePath, lineNumber, snippet, patternDescription);
    }

    private static int GetLineNumber(string content, int index)
    {
        var lineNumber = 1;

        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static string ExtractSnippet(string content, int startIndex, int length)
    {
        var snippetStart = FindLineStart(content, startIndex);
        var snippetEnd = FindLineEnd(content, startIndex + Math.Max(length, 1));

        if (snippetStart > 0)
        {
            snippetStart = FindLineStart(content, snippetStart - 1);
        }

        if (snippetEnd < content.Length)
        {
            snippetEnd = FindLineEnd(content, snippetEnd + 1);
        }

        return content[snippetStart..snippetEnd]
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .TrimEnd();
    }

    private static int FindLineStart(string content, int index)
    {
        var cursor = Math.Clamp(index, 0, content.Length);

        while (cursor > 0 && content[cursor - 1] != '\n')
        {
            cursor--;
        }

        return cursor;
    }

    private static int FindLineEnd(string content, int index)
    {
        var cursor = Math.Clamp(index, 0, content.Length);

        while (cursor < content.Length && content[cursor] != '\n')
        {
            cursor++;
        }

        return cursor;
    }

    private static string IndentSnippet(string snippet, string indent)
    {
        return string.Join(
            Environment.NewLine,
            snippet.Split('\n').Select(line => string.Concat(indent, line)));
    }

    private static string NormalizeRelativePath(string repositoryRoot, string filePath)
    {
        return Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/');
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

internal sealed record ArchitectureGuardRule(
    string Name,
    string Why,
    IReadOnlyList<ArchitectureGuardPattern> Patterns);

internal sealed class ArchitectureGuardPattern
{
    public ArchitectureGuardPattern(string description, string expression, RegexOptions options = RegexOptions.None)
    {
        Description = description;
        Regex = new Regex(
            expression,
            RegexOptions.CultureInvariant | RegexOptions.Multiline | options);
    }

    public string Description { get; }

    public Regex Regex { get; }

    public static ArchitectureGuardPattern Literal(string token)
    {
        return new(token, Regex.Escape(token));
    }
}

internal sealed record ArchitectureGuardViolation(
    string RelativePath,
    int LineNumber,
    string Snippet,
    string PatternDescription);
