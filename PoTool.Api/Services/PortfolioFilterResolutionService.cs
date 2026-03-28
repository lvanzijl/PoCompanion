using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed record PortfolioFilterResolution(
    FilterContext RequestedFilter,
    FilterContext EffectiveFilter,
    FilterValidationResult Validation);

public sealed class PortfolioFilterResolutionService
{
    private readonly PoToolDbContext _context;
    private readonly FilterContextValidator _validator;
    private readonly ILogger<PortfolioFilterResolutionService> _logger;

    public PortfolioFilterResolutionService(
        PoToolDbContext context,
        FilterContextValidator validator,
        ILogger<PortfolioFilterResolutionService> logger)
    {
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    public async Task<PortfolioFilterResolution> ResolveAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        string boundaryName,
        CancellationToken cancellationToken)
    {
        var requestedOptions = options ?? new PortfolioReadQueryOptions();
        var requestedFilter = MapRequestedFilter(requestedOptions);
        var issues = _validator.Validate(requestedFilter).Messages.ToList();

        var ownerProductIds = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .OrderBy(productId => productId)
            .ToArrayAsync(cancellationToken);

        var effectiveProductIds = ResolveProductIds(requestedFilter.ProductIds, ownerProductIds, issues);
        var effectiveProjects = await ResolveProjectsAsync(ownerProductIds, effectiveProductIds, requestedFilter.ProjectNumbers, issues, cancellationToken);
        var effectiveWorkPackages = await ResolveWorkPackagesAsync(ownerProductIds, effectiveProductIds, effectiveProjects, requestedFilter.WorkPackages, issues, cancellationToken);
        var effectiveLifecycleStates = ResolveLifecycleStates(requestedFilter.LifecycleStates, issues);
        var effectiveTime = ResolveTime(requestedFilter.Time, issues);

        var effectiveFilter = new FilterContext(
            effectiveProductIds,
            effectiveProjects,
            effectiveWorkPackages,
            effectiveLifecycleStates,
            FilterSelection<int>.All(),
            effectiveTime);

        var validation = FilterValidationResult.FromIssues(issues);

        _logger.LogInformation(
            "Resolved portfolio filter for {Boundary} and ProductOwner {ProductOwnerId}. RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}",
            boundaryName,
            productOwnerId,
            requestedFilter,
            effectiveFilter,
            validation.InvalidFields);

        return new PortfolioFilterResolution(requestedFilter, effectiveFilter, validation);
    }

    public static FilterResponseMetadataDto ToMetadata(PortfolioFilterResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new FilterResponseMetadataDto
        {
            RequestedFilter = ToDto(resolution.RequestedFilter),
            EffectiveFilter = ToDto(resolution.EffectiveFilter),
            InvalidFields = resolution.Validation.InvalidFields,
            Messages = resolution.Validation.Messages
                .Select(issue => new FilterValidationIssueDto
                {
                    Field = issue.Field,
                    Message = issue.Message
                })
                .ToArray()
        };
    }

    private static FilterContext MapRequestedFilter(PortfolioReadQueryOptions options)
        => new(
            options.ProductId.HasValue
                ? FilterSelection<int>.Selected([options.ProductId.Value])
                : FilterSelection<int>.All(),
            string.IsNullOrWhiteSpace(options.ProjectNumber)
                ? FilterSelection<string>.All()
                : FilterSelection<string>.Selected([options.ProjectNumber.Trim()]),
            string.IsNullOrWhiteSpace(options.WorkPackage)
                ? FilterSelection<string>.All()
                : FilterSelection<string>.Selected([options.WorkPackage.Trim()]),
            options.LifecycleState.HasValue
                ? FilterSelection<PortfolioLifecycleState>.Selected([options.LifecycleState.Value])
                : FilterSelection<PortfolioLifecycleState>.All(),
            FilterSelection<int>.All(),
            options.RangeStartUtc.HasValue || options.RangeEndUtc.HasValue
                ? FilterTimeSelection.DateRange(options.RangeStartUtc, options.RangeEndUtc)
                : FilterTimeSelection.None());

    private static FilterContextDto ToDto(FilterContext filter)
        => new()
        {
            ProductIds = ToDto(filter.ProductIds),
            ProjectNumbers = ToDto(filter.ProjectNumbers),
            WorkPackages = ToDto(filter.WorkPackages),
            LifecycleStates = ToDto(filter.LifecycleStates),
            TeamIds = ToDto(filter.TeamIds),
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

    private static FilterSelection<int> ResolveProductIds(
        FilterSelection<int> requested,
        IReadOnlyList<int> ownerProductIds,
        ICollection<FilterValidationIssue> issues)
    {
        if (ownerProductIds.Count == 0)
        {
            return FilterSelection<int>.All();
        }

        if (requested.IsAll)
        {
            return FilterSelection<int>.All();
        }

        var validIds = requested.Values
            .Where(ownerProductIds.Contains)
            .Distinct()
            .ToArray();

        if (validIds.Length != requested.Values.Count)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.ProductIds), "One or more selected products are outside the product owner's scope and were replaced with ALL."));
            return FilterSelection<int>.All();
        }

        return FilterSelection<int>.Selected(validIds);
    }

    private async Task<FilterSelection<string>> ResolveProjectsAsync(
        IReadOnlyList<int> ownerProductIds,
        FilterSelection<int> effectiveProductIds,
        FilterSelection<string> requestedProjects,
        ICollection<FilterValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (requestedProjects.IsAll)
        {
            return FilterSelection<string>.All();
        }

        var scopeProductIds = GetScopedProductIds(ownerProductIds, effectiveProductIds);
        if (scopeProductIds.Count == 0)
        {
            return FilterSelection<string>.All();
        }

        var requestedValues = requestedProjects.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedValues.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.ProjectNumbers), "Project selection cannot be empty when not using ALL. The filter was replaced with ALL."));
            return FilterSelection<string>.All();
        }

        var validProjects = await _context.PortfolioSnapshotItems
            .AsNoTracking()
            .Where(item => scopeProductIds.Contains(item.Snapshot.ProductId)
                && requestedValues.Contains(item.ProjectNumber))
            .Select(item => item.ProjectNumber)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (validProjects.Length != requestedValues.Length)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.ProjectNumbers), "One or more selected projects do not belong to the selected product scope and were replaced with ALL."));
            return FilterSelection<string>.All();
        }

        return FilterSelection<string>.Selected(validProjects.Order(StringComparer.OrdinalIgnoreCase));
    }

    private async Task<FilterSelection<string>> ResolveWorkPackagesAsync(
        IReadOnlyList<int> ownerProductIds,
        FilterSelection<int> effectiveProductIds,
        FilterSelection<string> effectiveProjects,
        FilterSelection<string> requestedWorkPackages,
        ICollection<FilterValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (requestedWorkPackages.IsAll)
        {
            return FilterSelection<string>.All();
        }

        var scopeProductIds = GetScopedProductIds(ownerProductIds, effectiveProductIds);
        if (scopeProductIds.Count == 0)
        {
            return FilterSelection<string>.All();
        }

        var requestedValues = requestedWorkPackages.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedValues.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.WorkPackages), "Work-package selection cannot be empty when not using ALL. The filter was replaced with ALL."));
            return FilterSelection<string>.All();
        }

        var query = _context.PortfolioSnapshotItems
            .AsNoTracking()
            .Where(item => item.WorkPackage != null
                && scopeProductIds.Contains(item.Snapshot.ProductId)
                && requestedValues.Contains(item.WorkPackage));

        if (!effectiveProjects.IsAll)
        {
            query = query.Where(item => effectiveProjects.Values.Contains(item.ProjectNumber));
        }

        var validWorkPackages = await query
            .Select(item => item.WorkPackage!)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (validWorkPackages.Length != requestedValues.Length)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.WorkPackages), "One or more selected work packages do not belong to the effective product/project scope and were replaced with ALL."));
            return FilterSelection<string>.All();
        }

        return FilterSelection<string>.Selected(validWorkPackages.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static FilterSelection<PortfolioLifecycleState> ResolveLifecycleStates(
        FilterSelection<PortfolioLifecycleState> requested,
        ICollection<FilterValidationIssue> issues)
    {
        if (requested.IsAll)
        {
            return FilterSelection<PortfolioLifecycleState>.All();
        }

        var values = requested.Values.Distinct().ToArray();
        if (values.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(FilterContext.LifecycleStates), "Lifecycle-state selection cannot be empty when not using ALL. The filter was replaced with ALL."));
            return FilterSelection<PortfolioLifecycleState>.All();
        }

        return FilterSelection<PortfolioLifecycleState>.Selected(values);
    }

    private static FilterTimeSelection ResolveTime(
        FilterTimeSelection requested,
        ICollection<FilterValidationIssue> issues)
    {
        switch (requested.Mode)
        {
            case FilterTimeSelectionMode.None:
            case FilterTimeSelectionMode.DateRange:
                if (requested.RangeStartUtc.HasValue && requested.RangeEndUtc.HasValue && requested.RangeStartUtc > requested.RangeEndUtc)
                {
                    issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Invalid date-range selection was replaced with no time constraint."));
                    return FilterTimeSelection.None();
                }

                return requested;

            default:
                issues.Add(new FilterValidationIssue(nameof(FilterContext.Time), "Portfolio read queries only support no time constraint or an explicit date range. The requested time filter was replaced with no time constraint."));
                return FilterTimeSelection.None();
        }
    }

    private static IReadOnlyList<int> GetScopedProductIds(IReadOnlyList<int> ownerProductIds, FilterSelection<int> selection)
        => selection.IsAll
            ? ownerProductIds
            : selection.Values.Where(ownerProductIds.Contains).Distinct().ToArray();
}
