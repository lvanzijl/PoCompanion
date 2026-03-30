using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;
using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

public sealed record PipelineFilterResolution(
    PipelineFilterContext RequestedFilter,
    PipelineEffectiveFilter EffectiveFilter,
    FilterValidationResult Validation);

public sealed record PipelineFilterBoundaryRequest(
    int? ProductOwnerId = null,
    IReadOnlyList<int>? ProductIds = null,
    IReadOnlyList<int>? RepositoryIds = null,
    int? SprintId = null,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null);

public sealed class PipelineFilterResolutionService
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<PipelineFilterResolutionService> _logger;

    public PipelineFilterResolutionService(
        PoToolDbContext context,
        ILogger<PipelineFilterResolutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PipelineFilterResolution> ResolveAsync(
        PipelineFilterBoundaryRequest request,
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

        var repositoryUniverse = await LoadRepositoryUniverseAsync(effectiveProductIds, cancellationToken);
        var repositoryScope = requestedFilter.RepositoryIds.IsAll
            ? repositoryUniverse
            : ResolveRepositoryScope(requestedFilter.RepositoryIds, repositoryUniverse, issues);

        var pipelineDefinitions = await LoadPipelineDefinitionsAsync(
            effectiveProductIds,
            repositoryScope,
            cancellationToken);

        var (effectiveTime, rangeStartUtc, rangeEndUtc, sprintId) = await ResolveTimeAsync(
            requestedFilter.Time,
            cancellationToken,
            issues);

        var effectiveFilter = new PipelineEffectiveFilter(
            new PipelineFilterContext(
                effectiveProductIds,
                requestedFilter.TeamIds,
                requestedFilter.RepositoryIds.IsAll
                    ? FilterSelection<int>.Selected(repositoryScope)
                    : FilterSelection<int>.Selected(requestedFilter.RepositoryIds.Values),
                effectiveTime),
            repositoryScope,
            pipelineDefinitions
                .Select(definition => definition.PipelineDefinitionId)
                .Distinct()
                .OrderBy(id => id)
                .ToArray(),
            pipelineDefinitions
                .Select(definition => new PipelineBranchScope(definition.PipelineDefinitionId, definition.DefaultBranch))
                .DistinctBy(scope => scope.PipelineId)
                .OrderBy(scope => scope.PipelineId)
                .ToArray(),
            rangeStartUtc,
            rangeEndUtc,
            sprintId);

        var validation = FilterValidationResult.FromIssues(issues);

        _logger.LogInformation(
            "Resolved pipeline filter for {Boundary}. RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}; ProductScope: {@ProductScope}; RepositoryScope: {@RepositoryScope}; TimeRange: {RangeStartUtc} - {RangeEndUtc}; BranchScope: {@BranchScope}",
            boundaryName,
            requestedFilter,
            effectiveFilter,
            validation.InvalidFields,
            effectiveFilter.Context.ProductIds.IsAll ? "ALL" : effectiveFilter.Context.ProductIds.Values,
            effectiveFilter.RepositoryScope,
            effectiveFilter.RangeStartUtc,
            effectiveFilter.RangeEndUtc,
            effectiveFilter.BranchScope);

        return new PipelineFilterResolution(requestedFilter, effectiveFilter, validation);
    }

    public static PipelineQueryResponseDto<T> ToResponse<T>(T data, PipelineFilterResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new PipelineQueryResponseDto<T>
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

    private static PipelineFilterContext MapRequestedFilter(PipelineFilterBoundaryRequest request)
        => new(
            ToIntSelection(request.ProductIds),
            FilterSelection<int>.All(),
            ToIntSelection(request.RepositoryIds),
            MapTime(request));

    private static FilterTimeSelection MapTime(PipelineFilterBoundaryRequest request)
    {
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
        PipelineFilterContext filter,
        ICollection<FilterValidationIssue> issues)
    {
        ValidateIntSelection(filter.ProductIds, nameof(PipelineFilterContext.ProductIds), issues);
        ValidateIntSelection(filter.RepositoryIds, nameof(PipelineFilterContext.RepositoryIds), issues);

        switch (filter.Time.Mode)
        {
            case FilterTimeSelectionMode.None:
                break;
            case FilterTimeSelectionMode.Sprint:
                if (!filter.Time.SprintId.HasValue || filter.Time.SprintId.Value <= 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
                }
                break;
            case FilterTimeSelectionMode.DateRange:
                if (filter.Time.RangeStartUtc.HasValue
                    && filter.Time.RangeEndUtc.HasValue
                    && filter.Time.RangeStartUtc.Value > filter.Time.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Date-range time selection requires the start to be earlier than or equal to the end."));
                }
                break;
            default:
                issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Unsupported pipeline time filter."));
                break;
        }
    }

    private async Task<IReadOnlyList<int>> LoadOwnerProductIdsAsync(
        int? productOwnerId,
        CancellationToken cancellationToken)
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
            issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.ProductIds), "Product selection cannot be empty when not using ALL."));
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
            issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.ProductIds), "One or more selected products are outside the product owner's scope and were replaced with all owner products."));
            return FilterSelection<int>.Selected(ownerProductIds);
        }

        return FilterSelection<int>.Selected(normalizedRequestedIds);
    }

    private async Task<IReadOnlyList<int>> LoadRepositoryUniverseAsync(
        FilterSelection<int> productIds,
        CancellationToken cancellationToken)
    {
        var repositoryQuery = _context.Repositories
            .AsNoTracking()
            .Select(repository => new { repository.ProductId, repository.Id });

        var definitionQuery = _context.PipelineDefinitions
            .AsNoTracking()
            .Select(definition => new { definition.ProductId, definition.RepositoryId });

        if (!productIds.IsAll)
        {
            var scopedProductIds = productIds.Values.ToArray();
            repositoryQuery = repositoryQuery.Where(repository => scopedProductIds.Contains(repository.ProductId));
            definitionQuery = definitionQuery.Where(definition => scopedProductIds.Contains(definition.ProductId));
        }

        var repositories = await repositoryQuery.Select(repository => repository.Id).ToListAsync(cancellationToken);
        var definitions = await definitionQuery.Select(definition => definition.RepositoryId).ToListAsync(cancellationToken);

        return repositories
            .Concat(definitions)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    private static IReadOnlyList<int> ResolveRepositoryScope(
        FilterSelection<int> requestedRepositories,
        IReadOnlyList<int> repositoryUniverse,
        ICollection<FilterValidationIssue> issues)
    {
        var requestedValues = requestedRepositories.Values
            .Where(value => value > 0)
            .Distinct()
            .ToArray();

        if (requestedValues.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.RepositoryIds), "Repository selection cannot be empty when not using ALL."));
            return Array.Empty<int>();
        }

        var universeLookup = repositoryUniverse.ToHashSet();
        return requestedValues
            .Where(universeLookup.Contains)
            .ToArray();
    }

    private async Task<List<PipelineDefinitionScope>> LoadPipelineDefinitionsAsync(
        FilterSelection<int> productIds,
        IReadOnlyList<int> repositoryScope,
        CancellationToken cancellationToken)
    {
        var query = _context.PipelineDefinitions
            .AsNoTracking()
            .Select(definition => new PipelineDefinitionScope(
                definition.PipelineDefinitionId,
                definition.ProductId,
                definition.RepositoryId,
                definition.DefaultBranch));

        if (!productIds.IsAll)
        {
            var scopedProductIds = productIds.Values.ToArray();
            query = query.Where(definition => scopedProductIds.Contains(definition.ProductId));
        }

        if (repositoryScope.Count > 0)
        {
            query = query.Where(definition => repositoryScope.Contains(definition.RepositoryId));
        }

        return await query
            .Distinct()
            .OrderBy(definition => definition.PipelineDefinitionId)
            .ToListAsync(cancellationToken);
    }

    private async Task<(FilterTimeSelection Time, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, int? SprintId)> ResolveTimeAsync(
        FilterTimeSelection requestedTime,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        switch (requestedTime.Mode)
        {
            case FilterTimeSelectionMode.None:
                return (FilterTimeSelection.None(), null, null, null);

            case FilterTimeSelectionMode.DateRange:
                if (requestedTime.RangeStartUtc.HasValue
                    && requestedTime.RangeEndUtc.HasValue
                    && requestedTime.RangeStartUtc.Value > requestedTime.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Invalid date range was replaced with no time constraint."));
                    return (FilterTimeSelection.None(), null, null, null);
                }

                return (
                    FilterTimeSelection.DateRange(requestedTime.RangeStartUtc, requestedTime.RangeEndUtc),
                    requestedTime.RangeStartUtc,
                    requestedTime.RangeEndUtc,
                    null);

            case FilterTimeSelectionMode.Sprint:
            {
                var sprint = await _context.Sprints
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        value => value.Id == requestedTime.SprintId
                            && value.StartDateUtc.HasValue
                            && value.EndDateUtc.HasValue,
                        cancellationToken);

                if (sprint is null)
                {
                    issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Requested sprint was not found or has no valid date boundaries."));
                    return (FilterTimeSelection.None(), null, null, null);
                }

                return (
                    FilterTimeSelection.Sprint(sprint.Id),
                    new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero),
                    new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero),
                    sprint.Id);
            }

            default:
                issues.Add(new FilterValidationIssue(nameof(PipelineFilterContext.Time), "Unsupported pipeline time filter."));
                return (FilterTimeSelection.None(), null, null, null);
        }
    }

    private static PipelineFilterContextDto ToDto(PipelineFilterContext filter)
        => new()
        {
            ProductIds = ToDto(filter.ProductIds),
            TeamIds = ToDto(filter.TeamIds),
            RepositoryIds = ToDto(filter.RepositoryIds),
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
            issues.Add(new FilterValidationIssue(field, $"{field} must contain one or more valid positive identifiers when not using ALL."));
        }
    }

    private static FilterSelection<int> ToIntSelection(IReadOnlyList<int>? values)
        => values is null
            ? FilterSelection<int>.All()
            : FilterSelection<int>.Selected(values);

    private sealed record PipelineDefinitionScope(
        int PipelineDefinitionId,
        int ProductId,
        int RepositoryId,
        string? DefaultBranch);
}
