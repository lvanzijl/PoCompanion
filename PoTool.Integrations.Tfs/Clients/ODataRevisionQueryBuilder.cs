using System.Globalization;
using System.Text;
using PoTool.Core;
using PoTool.Core.Configuration;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

internal sealed class ODataRevisionQueryBuilder
{
    private const string ChangedDateField = "ChangedDate";
    private const string WorkItemIdField = "WorkItemId";
    private const string RevisionField = "Revision";
    private static readonly RevisionFieldWhitelist.ODataRevisionSelectionSpec ODataSelectionSpec =
        RevisionFieldWhitelist.BuildODataRevisionSelectionSpec(includeRevision: true);
    private static readonly HashSet<string> ContinuationParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$skiptoken",
        "$skip",
        "$deltatoken",
        "continuationToken"
    };

    public string BuildInitialPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        DateTimeOffset? endDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        WorkItemIdRangeSegment? scopeSegment = null,
        bool? quoteDateStrings = null)
    {
        return BuildPageUrl(
            config,
            startDateTime,
            endDateTime,
            scopedWorkItemIds,
            options,
            top,
            seekCursor: null,
            scopeSegment,
            quoteDateStrings);
    }

    public string BuildSeekPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        DateTimeOffset? endDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        DateTimeOffset seekChangedDate,
        int seekWorkItemId,
        int seekRevision,
        WorkItemIdRangeSegment? scopeSegment = null,
        bool? quoteDateStrings = null)
    {
        return BuildPageUrl(
            config,
            startDateTime,
            endDateTime,
            scopedWorkItemIds,
            options,
            top,
            (seekChangedDate, seekWorkItemId, seekRevision),
            scopeSegment,
            quoteDateStrings);
    }

    public string BuildContinuationPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        DateTimeOffset? endDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        string nextLinkUrl,
        WorkItemIdRangeSegment? scopeSegment = null,
        bool? quoteDateStrings = null)
    {
        var canonicalUrl = BuildInitialPageUrl(
            config,
            startDateTime,
            endDateTime,
            scopedWorkItemIds,
            options,
            top,
            scopeSegment,
            quoteDateStrings);

        if (!Uri.TryCreate(nextLinkUrl, UriKind.Absolute, out var nextLinkUri))
        {
            return canonicalUrl;
        }

        var continuationSegments = nextLinkUri.Query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.TrimStart('?'))
            .Where(segment =>
            {
                var separator = segment.IndexOf('=');
                var key = separator >= 0 ? segment[..separator] : segment;
                return ContinuationParameterNames.Contains(key);
            })
            .ToArray();

        if (continuationSegments.Length == 0)
        {
            return canonicalUrl;
        }

        var separator = canonicalUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{canonicalUrl}{separator}{string.Join("&", continuationSegments)}";
    }

    private string BuildPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        DateTimeOffset? endDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        (DateTimeOffset ChangedDate, int WorkItemId, int Revision)? seekCursor,
        WorkItemIdRangeSegment? scopeSegment,
        bool? quoteDateStrings)
    {
        var baseUrl = config.AnalyticsODataBaseUrl.TrimEnd('/');
        var entitySet = config.AnalyticsODataEntitySetPath.Trim().TrimStart('/');
        var query = new List<string> { $"$top={Math.Max(1, top).ToString(CultureInfo.InvariantCulture)}" };
        var filters = new List<string>();

        if (startDateTime.HasValue)
        {
            filters.Add($"{ChangedDateField} ge {FormatDateLiteral(startDateTime.Value, options, quoteDateStrings)}");
        }
        if (endDateTime.HasValue)
        {
            filters.Add($"{ChangedDateField} lt {FormatDateLiteral(endDateTime.Value, options, quoteDateStrings)}");
        }

        var scopeFilter = BuildScopeFilter(scopedWorkItemIds, options, scopeSegment);
        if (!string.IsNullOrWhiteSpace(scopeFilter))
        {
            filters.Add(scopeFilter);
        }

        if (seekCursor is not null)
        {
            var cursor = seekCursor.Value;
            var changed = FormatDateLiteral(cursor.ChangedDate, options, quoteDateStrings);
            filters.Add(
                $"({ChangedDateField} gt {changed} or ({ChangedDateField} eq {changed} and {WorkItemIdField} gt {cursor.WorkItemId.ToString(CultureInfo.InvariantCulture)}) or ({ChangedDateField} eq {changed} and {WorkItemIdField} eq {cursor.WorkItemId.ToString(CultureInfo.InvariantCulture)} and {RevisionField} gt {cursor.Revision.ToString(CultureInfo.InvariantCulture)}))");
        }

        if (filters.Count > 0)
        {
            query.Add($"$filter={string.Join(" and ", filters)}");
        }

        if (options.ODataSelectMode == ODataRevisionSelectMode.Minimal)
        {
            query.Add($"$select={string.Join(",", ODataSelectionSpec.TopLevelSelect)}");

            if (ODataSelectionSpec.Expands.Count > 0)
            {
                var expandClauses = ODataSelectionSpec.Expands
                    .Select(expand => $"{expand.Key}($select={string.Join(",", expand.Value)})");
                query.Add($"$expand={string.Join(",", expandClauses)}");
            }
        }

        if (options.ODataOrderByEnabled)
        {
            query.Add($"$orderby={ChangedDateField} asc,{WorkItemIdField} asc,{RevisionField} asc");
        }

        return $"{baseUrl}/{entitySet}?{string.Join("&", query)}";
    }

    private static string? BuildScopeFilter(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        WorkItemIdRangeSegment? scopeSegment)
    {
        if (scopeSegment is not null)
        {
            var segment = scopeSegment.Value;
            return segment.Start == segment.End
                ? $"{WorkItemIdField} eq {segment.Start.ToString(CultureInfo.InvariantCulture)}"
                : $"{WorkItemIdField} ge {segment.Start.ToString(CultureInfo.InvariantCulture)} and {WorkItemIdField} le {segment.End.ToString(CultureInfo.InvariantCulture)}";
        }

        if (options.ODataScopeMode == ODataRevisionScopeMode.None ||
            scopedWorkItemIds == null ||
            scopedWorkItemIds.Count == 0)
        {
            return null;
        }

        var orderedIds = scopedWorkItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (orderedIds.Length == 0)
        {
            return null;
        }

        if (orderedIds.Length == 1)
        {
            return $"{WorkItemIdField} eq {orderedIds[0].ToString(CultureInfo.InvariantCulture)}";
        }

        if (options.ODataScopeMode == ODataRevisionScopeMode.IdList)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < orderedIds.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(" or ");
                }

                sb.Append(WorkItemIdField)
                    .Append(" eq ")
                    .Append(orderedIds[i].ToString(CultureInfo.InvariantCulture));
            }

            var idListFilter = $"({sb})";
            if (idListFilter.Length <= Math.Max(128, options.ODataMaxUrlLength))
            {
                return idListFilter;
            }
        }

        return $"{WorkItemIdField} ge {orderedIds[0].ToString(CultureInfo.InvariantCulture)} and {WorkItemIdField} le {orderedIds[^1].ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDateLiteral(DateTimeOffset value, RevisionIngestionPaginationOptions options, bool? quoteDateStrings)
    {
        var literal = value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        return (quoteDateStrings ?? options.ODataQuoteDateStrings) ? $"'{literal}'" : literal;
    }
}
