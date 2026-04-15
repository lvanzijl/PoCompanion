using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Delivery.Filters;
using PoTool.Core.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed record DeliveryFilterResolution(
    DeliveryFilterContext RequestedFilter,
    DeliveryEffectiveFilter EffectiveFilter,
    FilterValidationResult Validation,
    IReadOnlyDictionary<int, string> TeamLabels,
    IReadOnlyDictionary<int, string> SprintLabels);

public sealed record DeliveryFilterBoundaryRequest(
    int? ProductOwnerId = null,
    IReadOnlyList<int>? ProductIds = null,
    int? SprintId = null,
    IReadOnlyList<int>? SprintIds = null,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null);

public sealed class DeliveryFilterResolutionService
{
    private readonly PoToolDbContext _context;
    private readonly ContextResolver _contextResolver;
    private readonly ILogger<DeliveryFilterResolutionService> _logger;

    public DeliveryFilterResolutionService(
        PoToolDbContext context,
        ContextResolver contextResolver,
        ILogger<DeliveryFilterResolutionService> logger)
    {
        _context = context;
        _contextResolver = contextResolver;
        _logger = logger;
    }

    public async Task<DeliveryFilterResolution> ResolveAsync(
        DeliveryFilterBoundaryRequest request,
        string boundaryName,
        CancellationToken cancellationToken)
    {
        var requestedFilter = MapRequestedFilter(request);
        var issues = new List<FilterValidationIssue>();
        ValidateBoundaryRequest(request, issues);
        ValidateRequestedFilter(requestedFilter, issues);

        var ownerProductIds = await LoadOwnerProductIdsAsync(request.ProductOwnerId, cancellationToken);
        var effectiveProductIds = ResolveProductIds(
            requestedFilter.ProductIds,
            ownerProductIds,
            request.ProductOwnerId.HasValue,
            issues);

        var (effectiveTime, rangeStartUtc, rangeEndUtc, sprintId, sprintIds) = await ResolveTimeAsync(
            requestedFilter.Time,
            cancellationToken,
            issues);
        var contextResolution = await _contextResolver.ResolveAsync(
            new ContextResolutionRequest(
                effectiveProductIds,
                FilterSelection<int>.All(),
                sprintIds.Count > 0 ? sprintIds : sprintId.HasValue ? [sprintId.Value] : Array.Empty<int>()),
            cancellationToken);
        issues.AddRange(contextResolution.Validation.Messages);

        var effectiveFilter = new DeliveryEffectiveFilter(
            new DeliveryFilterContext(
                contextResolution.ProductIds,
                effectiveTime),
            rangeStartUtc,
            rangeEndUtc,
            sprintId,
            sprintIds);

        var validation = FilterValidationResult.FromIssues(issues);
        var sprintLabels = await CanonicalFilterDisplayLabelLoader.LoadSprintLabelsAsync(
            _context,
            CollectSprintIds(requestedFilter.Time, effectiveFilter.Context.Time, effectiveFilter),
            cancellationToken);

        LogResolution(boundaryName, request, requestedFilter, effectiveFilter, validation, ownerProductIds);

        return new DeliveryFilterResolution(
            requestedFilter,
            effectiveFilter,
            validation,
            new Dictionary<int, string>(),
            sprintLabels);
    }

    public static DeliveryQueryResponseDto<T> ToResponse<T>(T data, DeliveryFilterResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new DeliveryQueryResponseDto<T>
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
                .ToArray(),
            TeamLabels = resolution.TeamLabels,
            SprintLabels = resolution.SprintLabels
        };
    }

    private static IReadOnlyList<int> CollectSprintIds(
        FilterTimeSelection requestedTime,
        FilterTimeSelection effectiveTime,
        DeliveryEffectiveFilter effectiveFilter)
    {
        var sprintIds = CanonicalFilterDisplayLabelLoader.CollectSprintIds(requestedTime, effectiveTime).ToList();
        if (effectiveFilter.SprintId.HasValue)
        {
            sprintIds.Add(effectiveFilter.SprintId.Value);
        }

        sprintIds.AddRange(effectiveFilter.SprintIds);
        return sprintIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    private static DeliveryFilterContext MapRequestedFilter(DeliveryFilterBoundaryRequest request)
        => new(
            ToIntSelection(request.ProductIds),
            MapTime(request));

    private static FilterTimeSelection MapTime(DeliveryFilterBoundaryRequest request)
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
        DeliveryFilterContext filter,
        ICollection<FilterValidationIssue> issues)
    {
        ValidateIntSelection(filter.ProductIds, nameof(DeliveryFilterContext.ProductIds), issues);

        switch (filter.Time.Mode)
        {
            case FilterTimeSelectionMode.None:
                break;
            case FilterTimeSelectionMode.Sprint:
                if (!filter.Time.SprintId.HasValue || filter.Time.SprintId.Value <= 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
                }

                break;
            case FilterTimeSelectionMode.MultiSprint:
                if (filter.Time.SprintIds.Count == 0 || filter.Time.SprintIds.Any(id => id <= 0))
                {
                    issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
                }

                break;
            case FilterTimeSelectionMode.DateRange:
                if (filter.Time.RangeStartUtc.HasValue
                    && filter.Time.RangeEndUtc.HasValue
                    && filter.Time.RangeStartUtc.Value > filter.Time.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Date-range time selection requires the start to be earlier than or equal to the end."));
                }

                break;
            default:
                issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Unsupported delivery time filter."));
                break;
        }
    }

    private static void ValidateBoundaryRequest(
        DeliveryFilterBoundaryRequest request,
        ICollection<FilterValidationIssue> issues)
    {
        if (request.SprintIds is { Count: 0 }
            && !request.SprintId.HasValue
            && !request.RangeStartUtc.HasValue
            && !request.RangeEndUtc.HasValue)
        {
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Multi-sprint delivery requests must include sprint identifiers or explicit range boundaries."));
        }
    }

    private void LogResolution(
        string boundaryName,
        DeliveryFilterBoundaryRequest request,
        DeliveryFilterContext requestedFilter,
        DeliveryEffectiveFilter effectiveFilter,
        FilterValidationResult validation,
        IReadOnlyList<int> ownerProductIds)
    {
        var hasTimeMismatch = requestedFilter.Time.Mode != effectiveFilter.Context.Time.Mode
            || (requestedFilter.Time.Mode == FilterTimeSelectionMode.MultiSprint && effectiveFilter.SprintIds.Count == 0);
        if (!validation.IsValid || hasTimeMismatch)
        {
            _logger.LogWarning(
                "Delivery filter mismatch for {Boundary}. RequestedTimeInput: SprintId={RequestedSprintId}, SprintIds={@RequestedSprintIds}, Range={RequestedRangeStartUtc}-{RequestedRangeEndUtc}; RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}",
                boundaryName,
                request.SprintId,
                request.SprintIds,
                request.RangeStartUtc,
                request.RangeEndUtc,
                requestedFilter,
                effectiveFilter,
                validation.InvalidFields);
        }

        _logger.LogInformation(
            "Resolved delivery filter for {Boundary}. RequestedTimeInput: SprintId={RequestedSprintId}, SprintIds={@RequestedSprintIds}, Range={RequestedRangeStartUtc}-{RequestedRangeEndUtc}; RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}; OwnerDerivedProductIds: {@OwnerDerivedProductIds}; FinalProductScope: {@FinalProductScope}; TimeRange: {RangeStartUtc} - {RangeEndUtc}; SprintScope: {SprintId}; SprintRange: {@SprintIds}",
            boundaryName,
            request.SprintId,
            request.SprintIds,
            request.RangeStartUtc,
            request.RangeEndUtc,
            requestedFilter,
            effectiveFilter,
            validation.InvalidFields,
            ownerProductIds,
            effectiveFilter.Context.ProductIds.IsAll ? "ALL" : effectiveFilter.Context.ProductIds.Values,
            effectiveFilter.RangeStartUtc,
            effectiveFilter.RangeEndUtc,
            effectiveFilter.SprintId,
            effectiveFilter.SprintIds);
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
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.ProductIds), "Product selection cannot be empty when not using ALL."));
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
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.ProductIds), "One or more selected products are outside the product owner's scope and were replaced with all owner products."));
            return FilterSelection<int>.Selected(ownerProductIds);
        }

        return FilterSelection<int>.Selected(normalizedRequestedIds);
    }

    private async Task<(FilterTimeSelection EffectiveTime, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, int? SprintId, IReadOnlyList<int> SprintIds)> ResolveTimeAsync(
        FilterTimeSelection requestedTime,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        switch (requestedTime.Mode)
        {
            case FilterTimeSelectionMode.None:
                return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());

            case FilterTimeSelectionMode.DateRange:
                return (requestedTime, requestedTime.RangeStartUtc, requestedTime.RangeEndUtc, null, Array.Empty<int>());

            case FilterTimeSelectionMode.Sprint:
                return await ResolveSingleSprintAsync(requestedTime.SprintId, cancellationToken, issues);

            case FilterTimeSelectionMode.MultiSprint:
                return await ResolveMultiSprintAsync(requestedTime.SprintIds, cancellationToken, issues);

            default:
                issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Unsupported delivery time filter."));
                return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }
    }

    private async Task<(FilterTimeSelection EffectiveTime, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, int? SprintId, IReadOnlyList<int> SprintIds)> ResolveSingleSprintAsync(
        int? requestedSprintId,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        if (!requestedSprintId.HasValue || requestedSprintId.Value <= 0)
        {
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
            return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }

        var sprint = await _context.Sprints
            .AsNoTracking()
            .Where(candidate => candidate.Id == requestedSprintId.Value)
            .Select(candidate => new SprintWindow(candidate.Id, candidate.StartDateUtc, candidate.EndDateUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (sprint is null)
        {
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Selected sprint was not found and the time filter was cleared."));
            return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }

        return (
            FilterTimeSelection.Sprint(sprint.Id),
            sprint.StartDateUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(sprint.StartDateUtc.Value, DateTimeKind.Utc)),
            sprint.EndDateUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDateUtc.Value, DateTimeKind.Utc)),
            sprint.Id,
            Array.Empty<int>());
    }

    private async Task<(FilterTimeSelection EffectiveTime, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, int? SprintId, IReadOnlyList<int> SprintIds)> ResolveMultiSprintAsync(
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
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
            return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }

        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => normalizedSprintIds.Contains(sprint.Id))
            .Select(sprint => new SprintWindow(sprint.Id, sprint.StartDateUtc, sprint.EndDateUtc))
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "Selected sprints were not found and the time filter was cleared."));
            return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }

        if (sprints.Count != normalizedSprintIds.Length)
        {
            issues.Add(new FilterValidationIssue(nameof(DeliveryFilterContext.Time), "One or more selected sprints were not found and were removed from the effective delivery scope."));
        }

        var orderedSprints = sprints
            .OrderBy(sprint => sprint.StartDateUtc ?? DateTime.MaxValue)
            .ThenBy(sprint => sprint.Id)
            .ToArray();

        var effectiveSprintIds = orderedSprints
            .Select(sprint => sprint.Id)
            .ToArray();

        var datedSprints = orderedSprints
            .Where(sprint => sprint.StartDateUtc.HasValue && sprint.EndDateUtc.HasValue)
            .ToArray();

        DateTimeOffset? rangeStartUtc = datedSprints.Length == 0
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(datedSprints.Min(sprint => sprint.StartDateUtc)!.Value, DateTimeKind.Utc));
        DateTimeOffset? rangeEndUtc = datedSprints.Length == 0
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(datedSprints.Max(sprint => sprint.EndDateUtc)!.Value, DateTimeKind.Utc));

        return (
            FilterTimeSelection.MultiSprint(effectiveSprintIds),
            rangeStartUtc,
            rangeEndUtc,
            null,
            effectiveSprintIds);
    }

    private static DeliveryFilterContextDto ToDto(DeliveryFilterContext filter)
        => new()
        {
            ProductIds = ToDto(filter.ProductIds),
            Time = ToDto(filter.Time)
        };

    private static FilterSelectionDto<int> ToDto(FilterSelection<int> selection)
        => new()
        {
            IsAll = selection.IsAll,
            Values = selection.Values.ToArray()
        };

    private static FilterTimeSelectionDto ToDto(FilterTimeSelection selection)
        => new()
        {
            Mode = (FilterTimeSelectionModeDto)selection.Mode,
            SprintId = selection.SprintId,
            SprintIds = selection.SprintIds.ToArray(),
            RangeStartUtc = selection.RangeStartUtc,
            RangeEndUtc = selection.RangeEndUtc
        };

    private static FilterSelection<int> ToIntSelection(IReadOnlyList<int>? values)
        => values is { Count: > 0 }
            ? FilterSelection<int>.Selected(values)
            : FilterSelection<int>.All();

    private static void ValidateIntSelection(
        FilterSelection<int> selection,
        string field,
        ICollection<FilterValidationIssue> issues)
    {
        if (selection.IsAll)
        {
            return;
        }

        if (selection.Values.Count == 0 || selection.Values.Any(value => value <= 0))
        {
            issues.Add(new FilterValidationIssue(field, $"{field} contains one or more invalid values."));
        }
    }

    private sealed record SprintWindow(int Id, DateTime? StartDateUtc, DateTime? EndDateUtc);
}
