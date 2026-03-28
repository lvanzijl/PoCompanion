using PoTool.Shared.Metrics;

namespace PoTool.Core.Filters;

public enum FilterSelectionMode
{
    All = 0,
    Selected = 1
}

public enum FilterTimeSelectionMode
{
    None = 0,
    CurrentSprint = 1,
    Sprint = 2,
    MultiSprint = 3,
    DateRange = 4
}

public sealed record FilterSelection<T>(FilterSelectionMode Mode, IReadOnlyList<T> Values)
{
    public bool IsAll => Mode == FilterSelectionMode.All;

    public static FilterSelection<T> All() => new(FilterSelectionMode.All, Array.Empty<T>());

    public static FilterSelection<T> Selected(IEnumerable<T> values)
        => new(FilterSelectionMode.Selected, values?.ToArray() ?? Array.Empty<T>());
}

public sealed record FilterTimeSelection(
    FilterTimeSelectionMode Mode,
    int? SprintId,
    IReadOnlyList<int> SprintIds,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc)
{
    public static FilterTimeSelection None() => new(FilterTimeSelectionMode.None, null, Array.Empty<int>(), null, null);

    public static FilterTimeSelection CurrentSprint() => new(FilterTimeSelectionMode.CurrentSprint, null, Array.Empty<int>(), null, null);

    public static FilterTimeSelection Sprint(int sprintId) => new(FilterTimeSelectionMode.Sprint, sprintId, Array.Empty<int>(), null, null);

    public static FilterTimeSelection MultiSprint(IEnumerable<int> sprintIds)
        => new(FilterTimeSelectionMode.MultiSprint, null, sprintIds?.ToArray() ?? Array.Empty<int>(), null, null);

    public static FilterTimeSelection DateRange(DateTimeOffset? rangeStartUtc, DateTimeOffset? rangeEndUtc)
        => new(FilterTimeSelectionMode.DateRange, null, Array.Empty<int>(), rangeStartUtc, rangeEndUtc);
}

public sealed record FilterContext(
    FilterSelection<int> ProductIds,
    FilterSelection<string> ProjectNumbers,
    FilterSelection<string> WorkPackages,
    FilterSelection<PortfolioLifecycleState> LifecycleStates,
    FilterSelection<int> TeamIds,
    FilterTimeSelection Time)
{
    public static FilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterSelection<string>.All(),
        FilterSelection<string>.All(),
        FilterSelection<PortfolioLifecycleState>.All(),
        FilterSelection<int>.All(),
        FilterTimeSelection.None());
}

public sealed record FilterValidationIssue(string Field, string Message);

public sealed record FilterValidationResult(
    bool IsValid,
    IReadOnlyList<string> InvalidFields,
    IReadOnlyList<FilterValidationIssue> Messages)
{
    public static FilterValidationResult Valid { get; } = new(true, Array.Empty<string>(), Array.Empty<FilterValidationIssue>());

    public static FilterValidationResult FromIssues(IEnumerable<FilterValidationIssue> issues)
    {
        var issueList = issues.ToArray();
        return new FilterValidationResult(
            issueList.Length == 0,
            issueList.Select(issue => issue.Field).Distinct(StringComparer.Ordinal).ToArray(),
            issueList);
    }
}

public sealed class FilterContextValidator
{
    public FilterValidationResult Validate(FilterContext filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var issues = new List<FilterValidationIssue>();

        ValidateSelection(filter.ProductIds, nameof(FilterContext.ProductIds), issues, static value => value > 0, "Product selection must contain at least one valid product identifier when not using ALL.");
        ValidateSelection(filter.ProjectNumbers, nameof(FilterContext.ProjectNumbers), issues, static value => !string.IsNullOrWhiteSpace(value), "Project selection must contain at least one project value when not using ALL.");
        ValidateSelection(filter.WorkPackages, nameof(FilterContext.WorkPackages), issues, static value => !string.IsNullOrWhiteSpace(value), "Work-package selection must contain at least one work-package value when not using ALL.");
        ValidateSelection(filter.LifecycleStates, nameof(FilterContext.LifecycleStates), issues, static _ => true, "Lifecycle-state selection must contain at least one lifecycle state when not using ALL.");
        ValidateSelection(filter.TeamIds, nameof(FilterContext.TeamIds), issues, static value => value > 0, "Team selection must contain at least one valid team identifier when not using ALL.");

        switch (filter.Time.Mode)
        {
            case FilterTimeSelectionMode.None:
            case FilterTimeSelectionMode.CurrentSprint:
                break;

            case FilterTimeSelectionMode.Sprint:
                if (!filter.Time.SprintId.HasValue || filter.Time.SprintId.Value <= 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
                }
                break;

            case FilterTimeSelectionMode.MultiSprint:
                if (filter.Time.SprintIds.Count == 0 || filter.Time.SprintIds.Any(id => id <= 0))
                {
                    issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
                }
                break;

            case FilterTimeSelectionMode.DateRange:
                if (filter.Time.RangeStartUtc.HasValue && filter.Time.RangeEndUtc.HasValue
                    && filter.Time.RangeStartUtc.Value > filter.Time.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Date-range time selection requires the start to be earlier than or equal to the end."));
                }
                break;

            default:
                issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Unsupported time selection mode."));
                break;
        }

        return FilterValidationResult.FromIssues(issues);
    }

    private static void ValidateSelection<T>(
        FilterSelection<T> selection,
        string field,
        ICollection<FilterValidationIssue> issues,
        Func<T, bool> valueValidator,
        string emptyMessage)
    {
        if (selection.IsAll)
        {
            return;
        }

        if (selection.Values.Count == 0)
        {
            issues.Add(new FilterValidationIssue(field, emptyMessage));
            return;
        }

        if (selection.Values.Any(value => !valueValidator(value)))
        {
            issues.Add(new FilterValidationIssue(field, $"{field} contains one or more invalid values."));
        }
    }
}
