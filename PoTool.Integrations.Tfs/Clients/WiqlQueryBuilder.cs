using System.Text;
using System.Text.RegularExpressions;

namespace PoTool.Integrations.Tfs.Clients;

internal static partial class WiqlQueryBuilder
{
    private static readonly Regex SelectFromRegex = CreateSelectFromRegex();
    private static readonly Regex UnsupportedTopRegex = CreateUnsupportedTopRegex();
    private static readonly Regex EmptyWhereRegex = CreateEmptyWhereRegex();
    private static readonly Regex BrokenBooleanRegex = CreateBrokenBooleanRegex();
    private static readonly Regex EmptyInRegex = CreateEmptyInRegex();
    private static readonly Regex WhitespaceRegex = CreateWhitespaceRegex();

    public static string BuildWorkItemsQuery(
        IEnumerable<string> selectFields,
        IEnumerable<string>? whereClauses = null,
        IEnumerable<string>? orderByClauses = null)
    {
        return BuildQuery("WorkItems", selectFields, whereClauses, orderByClauses, modeClause: null, requireWhereClause: false);
    }

    public static string BuildWorkItemLinksQuery(
        IEnumerable<string> selectFields,
        IEnumerable<string> whereClauses,
        string modeClause)
    {
        return BuildQuery("WorkItemLinks", selectFields, whereClauses, orderByClauses: null, modeClause, requireWhereClause: true);
    }

    public static void Validate(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("WIQL query cannot be null or empty.");
        }

        var normalized = ToDiagnosticString(query);
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("WIQL query cannot be empty after normalization.");
        }

        if (UnsupportedTopRegex.IsMatch(normalized))
        {
            throw new InvalidOperationException("WIQL SELECT TOP is not supported by the hardened query path. Fetch IDs first and limit client-side.");
        }

        var selectMatch = SelectFromRegex.Match(normalized);
        if (!selectMatch.Success)
        {
            throw new InvalidOperationException("WIQL must start with SELECT and include FROM WorkItems or FROM WorkItemLinks.");
        }

        var selectSegment = selectMatch.Groups["select"].Value.Trim();
        if (selectSegment.Length == 0)
        {
            throw new InvalidOperationException("WIQL SELECT must include at least one field.");
        }

        if (EmptyWhereRegex.IsMatch(normalized))
        {
            throw new InvalidOperationException("WIQL WHERE clause is incomplete.");
        }

        if (BrokenBooleanRegex.IsMatch(normalized))
        {
            throw new InvalidOperationException("WIQL contains a malformed boolean clause composition.");
        }

        if (EmptyInRegex.IsMatch(normalized))
        {
            throw new InvalidOperationException("WIQL cannot contain an empty IN () filter.");
        }
    }

    public static string ToDiagnosticString(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : WhitespaceRegex.Replace(query, " ").Trim();
    }

    private static string BuildQuery(
        string source,
        IEnumerable<string> selectFields,
        IEnumerable<string>? whereClauses,
        IEnumerable<string>? orderByClauses,
        string? modeClause,
        bool requireWhereClause)
    {
        var normalizedFields = NormalizeSegments(selectFields, "select field");
        if (normalizedFields.Count == 0)
        {
            throw new InvalidOperationException("WIQL SELECT must include at least one field.");
        }

        var normalizedWhereClauses = NormalizeSegments(whereClauses, "WHERE clause");
        if (requireWhereClause && normalizedWhereClauses.Count == 0)
        {
            throw new InvalidOperationException($"WIQL FROM {source} must include at least one WHERE clause.");
        }

        var normalizedOrderByClauses = NormalizeSegments(orderByClauses, "ORDER BY clause");
        var normalizedModeClause = NormalizeOptionalSegment(modeClause, "MODE clause");

        var builder = new StringBuilder()
            .Append("SELECT ")
            .Append(string.Join(", ", normalizedFields))
            .Append(" FROM ")
            .Append(source);

        if (normalizedWhereClauses.Count > 0)
        {
            builder.Append(" WHERE ")
                .Append(string.Join(" AND ", normalizedWhereClauses));
        }

        if (normalizedOrderByClauses.Count > 0)
        {
            builder.Append(" ORDER BY ")
                .Append(string.Join(", ", normalizedOrderByClauses));
        }

        if (normalizedModeClause is not null)
        {
            builder.Append(' ')
                .Append(normalizedModeClause);
        }

        var query = builder.ToString();
        Validate(query);
        return query;
    }

    private static List<string> NormalizeSegments(IEnumerable<string>? segments, string segmentName)
    {
        if (segments is null)
        {
            return [];
        }

        var normalized = new List<string>();
        foreach (var segment in segments)
        {
            normalized.Add(NormalizeRequiredSegment(segment, segmentName));
        }

        return normalized;
    }

    private static string NormalizeRequiredSegment(string value, string segmentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"WIQL {segmentName} cannot be null or empty.");
        }

        return ToDiagnosticString(value);
    }

    private static string? NormalizeOptionalSegment(string? value, string segmentName)
    {
        if (value is null)
        {
            return null;
        }

        return NormalizeRequiredSegment(value, segmentName);
    }

    [GeneratedRegex(@"^\s*SELECT\s+(?<select>.+?)\s+FROM\s+(?<source>WorkItems|WorkItemLinks)\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateSelectFromRegex();

    [GeneratedRegex(@"^\s*SELECT\s+TOP\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateUnsupportedTopRegex();

    [GeneratedRegex(@"\bWHERE\s*(ORDER\s+BY|MODE\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateEmptyWhereRegex();

    [GeneratedRegex(@"\bWHERE\s+(AND|OR)\b|\b(AND|OR)\s*(ORDER\s+BY|MODE\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateBrokenBooleanRegex();

    [GeneratedRegex(@"\bIN\s*\(\s*\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateEmptyInRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Singleline)]
    private static partial Regex CreateWhitespaceRegex();
}
