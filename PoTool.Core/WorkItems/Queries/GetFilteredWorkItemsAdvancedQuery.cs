using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query for filtering work items using multiple dimensions.
/// Supports combining multiple filter criteria for advanced search.
/// </summary>
public sealed record GetFilteredWorkItemsAdvancedQuery(
    string? TypeFilter = null,
    string? StateFilter = null,
    string? IterationPathFilter = null,
    string? AreaPathFilter = null,
    int? MinEffort = null,
    int? MaxEffort = null,
    bool? HasValidationIssues = null,
    bool? IsBlocked = null,
    string? TitleSearch = null
) : IQuery<IEnumerable<WorkItemDto>>;
