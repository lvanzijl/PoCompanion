using System.Globalization;
using System.Text;
using PoTool.Core.Configuration;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

internal sealed class ODataRevisionQueryBuilder
{
    private const string ChangedDateField = "ChangedDate";
    private const string WorkItemIdField = "WorkItemId";
    private const string RevisionField = "Revision";
    private static readonly string[] MinimalSelectFields =
    [
        "WorkItemId",
        "Revision",
        "ChangedDate",
        "WorkItemType",
        "Title",
        "State",
        "Reason",
        "IterationPath",
        "AreaPath",
        "CreatedDate",
        "ClosedDate",
        "Effort",
        "Tags",
        "Severity",
        "ChangedBy"
    ];

    public string BuildInitialPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top)
    {
        return BuildPageUrl(
            config,
            startDateTime,
            scopedWorkItemIds,
            options,
            top,
            seekCursor: null);
    }

    public string BuildSeekPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        DateTimeOffset seekChangedDate,
        int seekWorkItemId,
        int seekRevision)
    {
        return BuildPageUrl(
            config,
            startDateTime,
            scopedWorkItemIds,
            options,
            top,
            (seekChangedDate, seekWorkItemId, seekRevision));
    }

    private string BuildPageUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options,
        int top,
        (DateTimeOffset ChangedDate, int WorkItemId, int Revision)? seekCursor)
    {
        var baseUrl = config.AnalyticsODataBaseUrl.TrimEnd('/');
        var entitySet = config.AnalyticsODataEntitySetPath.Trim().TrimStart('/');
        var query = new List<string> { $"$top={Math.Max(1, top).ToString(CultureInfo.InvariantCulture)}" };
        var filters = new List<string>();

        if (startDateTime.HasValue)
        {
            filters.Add($"{ChangedDateField} ge {FormatDateLiteral(startDateTime.Value, options)}");
        }

        var scopeFilter = BuildScopeFilter(scopedWorkItemIds, options);
        if (!string.IsNullOrWhiteSpace(scopeFilter))
        {
            filters.Add(scopeFilter);
        }

        if (seekCursor is not null)
        {
            var cursor = seekCursor.Value;
            var changed = FormatDateLiteral(cursor.ChangedDate, options);
            filters.Add(
                $"({ChangedDateField} gt {changed} or ({ChangedDateField} eq {changed} and {WorkItemIdField} gt {cursor.WorkItemId.ToString(CultureInfo.InvariantCulture)}) or ({ChangedDateField} eq {changed} and {WorkItemIdField} eq {cursor.WorkItemId.ToString(CultureInfo.InvariantCulture)} and {RevisionField} gt {cursor.Revision.ToString(CultureInfo.InvariantCulture)}))");
        }

        if (filters.Count > 0)
        {
            query.Add($"$filter={string.Join(" and ", filters)}");
        }

        if (options.ODataSelectMode == ODataRevisionSelectMode.Minimal)
        {
            query.Add($"$select={string.Join(",", MinimalSelectFields)}");
        }

        if (options.ODataOrderByEnabled)
        {
            query.Add($"$orderby={ChangedDateField} asc,{WorkItemIdField} asc,{RevisionField} asc");
        }

        return $"{baseUrl}/{entitySet}?{string.Join("&", query)}";
    }

    private static string? BuildScopeFilter(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options)
    {
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

    private static string FormatDateLiteral(DateTimeOffset value, RevisionIngestionPaginationOptions options)
    {
        var literal = value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        return options.ODataUseQuotedDateLiterals ? $"datetimeoffset'{literal}'" : literal;
    }
}
