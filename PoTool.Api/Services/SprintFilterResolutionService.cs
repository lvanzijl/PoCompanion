using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;
using PoTool.Core.Metrics.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed record SprintFilterResolution(
    SprintFilterContext RequestedFilter,
    SprintEffectiveFilter EffectiveFilter,
    FilterValidationResult Validation);

public sealed record SprintFilterBoundaryRequest(
    int? ProductOwnerId = null,
    IReadOnlyList<int>? ProductIds = null,
    int? TeamId = null,
    string? AreaPath = null,
    IReadOnlyList<string>? AreaPaths = null,
    string? IterationPath = null,
    IReadOnlyList<string>? IterationPaths = null,
    int? SprintId = null,
    IReadOnlyList<int>? SprintIds = null,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null);

public sealed class SprintFilterResolutionService
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<SprintFilterResolutionService> _logger;

    public SprintFilterResolutionService(
        PoToolDbContext context,
        ILogger<SprintFilterResolutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SprintFilterResolution> ResolveAsync(
        SprintFilterBoundaryRequest request,
        string boundaryName,
        CancellationToken cancellationToken)
    {
        var requestedFilter = MapRequestedFilter(request);
        var issues = new List<FilterValidationIssue>();
        ValidateRequestedFilter(requestedFilter, issues);

        var ownerProductIds = await LoadOwnerProductIdsAsync(request.ProductOwnerId, cancellationToken);
        var effectiveProductIds = ResolveProductIds(
            requestedFilter.ProductIds,
            ownerProductIds,
            request.ProductOwnerId.HasValue,
            issues);
        var effectiveTeamIds = ResolveTeamIds(requestedFilter.TeamIds, issues);
        var effectiveAreaPaths = ResolveStringSelection(requestedFilter.AreaPaths, nameof(SprintFilterContext.AreaPaths), issues);
        var requestedIterationPaths = ResolveStringSelection(requestedFilter.IterationPaths, nameof(SprintFilterContext.IterationPaths), issues);

        var resolvedTime = await ResolveTimeAsync(
            requestedFilter.Time,
            requestedIterationPaths.Values,
            cancellationToken,
            issues);

        var effectiveFilter = new SprintEffectiveFilter(
            new SprintFilterContext(
                effectiveProductIds,
                effectiveTeamIds,
                effectiveAreaPaths,
                resolvedTime.IterationPaths.Count == 0
                    ? requestedIterationPaths
                    : FilterSelection<string>.Selected(resolvedTime.IterationPaths),
                resolvedTime.Time),
            resolvedTime.RangeStartUtc,
            resolvedTime.RangeEndUtc,
            resolvedTime.SprintId,
            resolvedTime.SprintIds,
            resolvedTime.IterationPaths,
            resolvedTime.CurrentSprintId,
            resolvedTime.PreviousSprintId);

        var validation = FilterValidationResult.FromIssues(issues);

        _logger.LogInformation(
            "Resolved sprint filter for {Boundary}. RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}; OwnerDerivedProductIds: {@OwnerDerivedProductIds}; FinalProductScope: {@FinalProductScope}; FinalIterationScope: {@FinalIterationScope}; FinalAreaScope: {@FinalAreaScope}; TimeRange: {RangeStartUtc} - {RangeEndUtc}; SprintScope: {SprintId}; SprintRange: {@SprintIds}; CurrentSprint: {CurrentSprintId}; PreviousSprint: {PreviousSprintId}",
            boundaryName,
            requestedFilter,
            effectiveFilter,
            validation.InvalidFields,
            ownerProductIds,
            effectiveFilter.Context.ProductIds.IsAll ? "ALL" : effectiveFilter.Context.ProductIds.Values,
            effectiveFilter.IterationPaths,
            effectiveFilter.Context.AreaPaths.IsAll ? "ALL" : effectiveFilter.Context.AreaPaths.Values,
            effectiveFilter.RangeStartUtc,
            effectiveFilter.RangeEndUtc,
            effectiveFilter.SprintId,
            effectiveFilter.SprintIds,
            effectiveFilter.CurrentSprintId,
            effectiveFilter.PreviousSprintId);

        return new SprintFilterResolution(requestedFilter, effectiveFilter, validation);
    }

    public static SprintQueryResponseDto<T> ToResponse<T>(T data, SprintFilterResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new SprintQueryResponseDto<T>
        {
            Data = data,
            RequestedFilter = ToDto(resolution.RequestedFilter),
            EffectiveFilter = ToDto(resolution.EffectiveFilter.Context),
            InvalidFields = resolution.Validation.InvalidFields,
            ValidationMessages = resolution.Validation.Messages
                .Select(issue => new FilterValidationIssueDto
                {
                    Field = issue.Field,
                    Message = issue.Message
                })
                .ToArray()
        };
    }

    private static SprintFilterContext MapRequestedFilter(SprintFilterBoundaryRequest request)
        => new(
            ToIntSelection(request.ProductIds),
            request.TeamId.HasValue
                ? FilterSelection<int>.Selected([request.TeamId.Value])
                : FilterSelection<int>.All(),
            ToStringSelection(request.AreaPaths, request.AreaPath),
            ToStringSelection(request.IterationPaths, request.IterationPath),
            MapTime(request));

    private static FilterTimeSelection MapTime(SprintFilterBoundaryRequest request)
    {
        if (request.SprintIds is { Count: > 0 })
        {
            return FilterTimeSelection.MultiSprint(request.SprintIds);
        }

        if (request.SprintId.HasValue)
        {
            return FilterTimeSelection.Sprint(request.SprintId.Value);
        }

        if (request.RangeStartUtc.HasValue || request.RangeEndUtc.HasValue)
        {
            return FilterTimeSelection.DateRange(request.RangeStartUtc, request.RangeEndUtc);
        }

        return FilterTimeSelection.None();
    }

    private static void ValidateRequestedFilter(
        SprintFilterContext filter,
        ICollection<FilterValidationIssue> issues)
    {
        ValidateIntSelection(filter.ProductIds, nameof(SprintFilterContext.ProductIds), issues);
        ValidateIntSelection(filter.TeamIds, nameof(SprintFilterContext.TeamIds), issues);
        ValidateStringSelection(filter.AreaPaths, nameof(SprintFilterContext.AreaPaths), issues);
        ValidateStringSelection(filter.IterationPaths, nameof(SprintFilterContext.IterationPaths), issues);

        switch (filter.Time.Mode)
        {
            case FilterTimeSelectionMode.None:
                break;
            case FilterTimeSelectionMode.Sprint:
                if (!filter.Time.SprintId.HasValue || filter.Time.SprintId.Value <= 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
                }

                break;
            case FilterTimeSelectionMode.MultiSprint:
                if (filter.Time.SprintIds.Count == 0 || filter.Time.SprintIds.Any(id => id <= 0))
                {
                    issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
                }

                break;
            case FilterTimeSelectionMode.DateRange:
                if (filter.Time.RangeStartUtc.HasValue
                    && filter.Time.RangeEndUtc.HasValue
                    && filter.Time.RangeStartUtc.Value > filter.Time.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Date-range time selection requires the start to be earlier than or equal to the end."));
                }

                break;
            default:
                issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Unsupported sprint time filter."));
                break;
        }
    }

    private async Task<IReadOnlyList<int>> LoadOwnerProductIdsAsync(int? productOwnerId, CancellationToken cancellationToken)
    {
        if (!productOwnerId.HasValue)
        {
            return Array.Empty<int>();
        }

        return await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId.Value)
            .Select(product => product.Id)
            .OrderBy(id => id)
            .ToArrayAsync(cancellationToken);
    }

    private static FilterSelection<int> ResolveProductIds(
        FilterSelection<int> requestedProductIds,
        IReadOnlyList<int> ownerProductIds,
        bool hasOwnerScope,
        ICollection<FilterValidationIssue> issues)
    {
        if (hasOwnerScope && ownerProductIds.Count == 0)
        {
            return FilterSelection<int>.Selected(Array.Empty<int>());
        }

        if (requestedProductIds.IsAll)
        {
            return hasOwnerScope
                ? FilterSelection<int>.Selected(ownerProductIds)
                : FilterSelection<int>.All();
        }

        var normalizedRequestedIds = requestedProductIds.Values
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (normalizedRequestedIds.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.ProductIds), "Product selection cannot be empty when not using ALL."));
            return hasOwnerScope
                ? FilterSelection<int>.Selected(ownerProductIds)
                : FilterSelection<int>.All();
        }

        if (!hasOwnerScope)
        {
            return FilterSelection<int>.Selected(normalizedRequestedIds);
        }

        var ownerProductSet = ownerProductIds.ToHashSet();
        if (normalizedRequestedIds.Any(id => !ownerProductSet.Contains(id)))
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.ProductIds), "One or more selected products are outside the product owner's scope and were replaced with all owner products."));
            return FilterSelection<int>.Selected(ownerProductIds);
        }

        return FilterSelection<int>.Selected(normalizedRequestedIds);
    }

    private static FilterSelection<int> ResolveTeamIds(
        FilterSelection<int> requestedTeamIds,
        ICollection<FilterValidationIssue> issues)
    {
        if (requestedTeamIds.IsAll)
        {
            return FilterSelection<int>.All();
        }

        var normalizedTeamIds = requestedTeamIds.Values
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (normalizedTeamIds.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.TeamIds), "Team selection cannot be empty when not using ALL."));
            return FilterSelection<int>.All();
        }

        return FilterSelection<int>.Selected(normalizedTeamIds);
    }

    private static FilterSelection<string> ResolveStringSelection(
        FilterSelection<string> requestedValues,
        string fieldName,
        ICollection<FilterValidationIssue> issues)
    {
        if (requestedValues.IsAll)
        {
            return FilterSelection<string>.All();
        }

        var normalizedValues = requestedValues.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedValues.Length == 0)
        {
            issues.Add(new FilterValidationIssue(fieldName, $"{fieldName} selection cannot be empty when not using ALL."));
            return FilterSelection<string>.All();
        }

        return FilterSelection<string>.Selected(normalizedValues);
    }

    private async Task<ResolvedSprintTime> ResolveTimeAsync(
        FilterTimeSelection requestedTime,
        IReadOnlyList<string> requestedIterationPaths,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        return requestedTime.Mode switch
        {
            FilterTimeSelectionMode.None => await ResolveIterationPathsAsync(requestedIterationPaths, cancellationToken, issues),
            FilterTimeSelectionMode.DateRange => ResolveDateRange(requestedTime, issues),
            FilterTimeSelectionMode.Sprint => await ResolveSingleSprintAsync(requestedTime.SprintId, cancellationToken, issues),
            FilterTimeSelectionMode.MultiSprint => await ResolveMultiSprintAsync(requestedTime.SprintIds, cancellationToken, issues),
            _ => new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), requestedIterationPaths, null, null)
        };
    }

    private static ResolvedSprintTime ResolveDateRange(
        FilterTimeSelection requestedTime,
        ICollection<FilterValidationIssue> issues)
    {
        if (requestedTime.RangeStartUtc.HasValue
            && requestedTime.RangeEndUtc.HasValue
            && requestedTime.RangeStartUtc.Value > requestedTime.RangeEndUtc.Value)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Invalid date range was replaced with no time constraint."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        return new ResolvedSprintTime(
            FilterTimeSelection.DateRange(requestedTime.RangeStartUtc, requestedTime.RangeEndUtc),
            requestedTime.RangeStartUtc,
            requestedTime.RangeEndUtc,
            null,
            Array.Empty<int>(),
            Array.Empty<string>(),
            null,
            null);
    }

    private async Task<ResolvedSprintTime> ResolveSingleSprintAsync(
        int? requestedSprintId,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        if (!requestedSprintId.HasValue || requestedSprintId.Value <= 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        var sprint = await _context.Sprints
            .AsNoTracking()
            .Where(candidate => candidate.Id == requestedSprintId.Value)
            .Select(candidate => new SprintResolutionCandidate(
                candidate.Id,
                candidate.Path,
                candidate.StartDateUtc,
                candidate.EndDateUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (sprint is null)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Selected sprint was not found and the time filter was cleared."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        return new ResolvedSprintTime(
            FilterTimeSelection.Sprint(sprint.Id),
            ToOffset(sprint.StartDateUtc),
            ToOffset(sprint.EndDateUtc),
            sprint.Id,
            Array.Empty<int>(),
            string.IsNullOrWhiteSpace(sprint.Path) ? Array.Empty<string>() : [sprint.Path],
            sprint.Id,
            null);
    }

    private async Task<ResolvedSprintTime> ResolveMultiSprintAsync(
        IReadOnlyList<int> requestedSprintIds,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        var normalizedSprintIds = requestedSprintIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (normalizedSprintIds.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => normalizedSprintIds.Contains(sprint.Id))
            .Select(sprint => new SprintResolutionCandidate(
                sprint.Id,
                sprint.Path,
                sprint.StartDateUtc,
                sprint.EndDateUtc))
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "Selected sprints were not found and the time filter was cleared."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        if (sprints.Count != normalizedSprintIds.Length)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.Time), "One or more selected sprints were not found and were removed from the effective sprint scope."));
        }

        var orderedSprints = sprints
            .OrderBy(sprint => sprint.StartDateUtc ?? DateTime.MaxValue)
            .ThenBy(sprint => sprint.Id)
            .ToArray();

        var effectiveSprintIds = orderedSprints
            .Select(sprint => sprint.Id)
            .ToArray();
        var effectiveIterationPaths = orderedSprints
            .Select(sprint => sprint.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var datedSprints = orderedSprints
            .Where(sprint => sprint.StartDateUtc.HasValue && sprint.EndDateUtc.HasValue)
            .ToArray();

        return new ResolvedSprintTime(
            FilterTimeSelection.MultiSprint(effectiveSprintIds),
            datedSprints.Length == 0 ? null : ToOffset(datedSprints.Min(sprint => sprint.StartDateUtc)),
            datedSprints.Length == 0 ? null : ToOffset(datedSprints.Max(sprint => sprint.EndDateUtc)),
            null,
            effectiveSprintIds,
            effectiveIterationPaths,
            effectiveSprintIds.LastOrDefault(),
            effectiveSprintIds.Length > 1 ? effectiveSprintIds[^2] : null);
    }

    private async Task<ResolvedSprintTime> ResolveIterationPathsAsync(
        IReadOnlyList<string> requestedIterationPaths,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        if (requestedIterationPaths.Count == 0)
        {
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        var normalizedPaths = requestedIterationPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(SprintFilterContext.IterationPaths), "Iteration-path selection cannot be empty when not using ALL."));
            return new ResolvedSprintTime(FilterTimeSelection.None(), null, null, null, Array.Empty<int>(), Array.Empty<string>(), null, null);
        }

        var matchingSprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => normalizedPaths.Contains(sprint.Path))
            .Select(sprint => new SprintResolutionCandidate(
                sprint.Id,
                sprint.Path,
                sprint.StartDateUtc,
                sprint.EndDateUtc))
            .ToListAsync(cancellationToken);

        if (matchingSprints.Count == 1)
        {
            var sprint = matchingSprints[0];
            return new ResolvedSprintTime(
                FilterTimeSelection.Sprint(sprint.Id),
                ToOffset(sprint.StartDateUtc),
                ToOffset(sprint.EndDateUtc),
                sprint.Id,
                Array.Empty<int>(),
                [sprint.Path],
                sprint.Id,
                null);
        }

        return new ResolvedSprintTime(
            FilterTimeSelection.None(),
            null,
            null,
            null,
            Array.Empty<int>(),
            normalizedPaths,
            null,
            null);
    }

    private static SprintFilterContextDto ToDto(SprintFilterContext filter)
        => new()
        {
            ProductIds = ToDto(filter.ProductIds),
            TeamIds = ToDto(filter.TeamIds),
            AreaPaths = ToDto(filter.AreaPaths),
            IterationPaths = ToDto(filter.IterationPaths),
            Time = new FilterTimeSelectionDto
            {
                Mode = (FilterTimeSelectionModeDto)filter.Time.Mode,
                SprintId = filter.Time.SprintId,
                SprintIds = filter.Time.SprintIds.ToArray(),
                RangeStartUtc = filter.Time.RangeStartUtc,
                RangeEndUtc = filter.Time.RangeEndUtc
            }
        };

    private static FilterSelectionDto<T> ToDto<T>(FilterSelection<T> selection)
        => new()
        {
            IsAll = selection.IsAll,
            Values = selection.Values.ToArray()
        };

    private static FilterSelection<int> ToIntSelection(IReadOnlyList<int>? values)
        => values is { Count: > 0 }
            ? FilterSelection<int>.Selected(values)
            : FilterSelection<int>.All();

    private static FilterSelection<string> ToStringSelection(IReadOnlyList<string>? values, string? singleValue)
    {
        if (values is { Count: > 0 })
        {
            return FilterSelection<string>.Selected(values);
        }

        return string.IsNullOrWhiteSpace(singleValue)
            ? FilterSelection<string>.All()
            : FilterSelection<string>.Selected([singleValue]);
    }

    private static void ValidateIntSelection(
        FilterSelection<int> selection,
        string fieldName,
        ICollection<FilterValidationIssue> issues)
    {
        if (selection.IsAll)
        {
            return;
        }

        if (selection.Values.Count == 0 || selection.Values.Any(value => value <= 0))
        {
            issues.Add(new FilterValidationIssue(fieldName, $"{fieldName} selection must contain one or more valid identifiers when not using ALL."));
        }
    }

    private static void ValidateStringSelection(
        FilterSelection<string> selection,
        string fieldName,
        ICollection<FilterValidationIssue> issues)
    {
        if (selection.IsAll)
        {
            return;
        }

        if (selection.Values.Count == 0 || selection.Values.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(new FilterValidationIssue(fieldName, $"{fieldName} selection must contain one or more values when not using ALL."));
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;

    private sealed record SprintResolutionCandidate(
        int Id,
        string Path,
        DateTime? StartDateUtc,
        DateTime? EndDateUtc);

    private sealed record ResolvedSprintTime(
        FilterTimeSelection Time,
        DateTimeOffset? RangeStartUtc,
        DateTimeOffset? RangeEndUtc,
        int? SprintId,
        IReadOnlyList<int> SprintIds,
        IReadOnlyList<string> IterationPaths,
        int? CurrentSprintId,
        int? PreviousSprintId);
}
