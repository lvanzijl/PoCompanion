using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

public sealed record PullRequestFilterResolution(
    PullRequestFilterContext RequestedFilter,
    PullRequestEffectiveFilter EffectiveFilter,
    FilterValidationResult Validation);

public sealed record PullRequestFilterBoundaryRequest(
    IReadOnlyList<int>? ProductIds = null,
    int? TeamId = null,
    string? RepositoryName = null,
    string? IterationPath = null,
    string? CreatedBy = null,
    string? Status = null,
    int? SprintId = null,
    IReadOnlyList<int>? SprintIds = null,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null);

public sealed class PullRequestFilterResolutionService
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<PullRequestFilterResolutionService> _logger;

    public PullRequestFilterResolutionService(
        PoToolDbContext context,
        ILogger<PullRequestFilterResolutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PullRequestFilterResolution> ResolveAsync(
        PullRequestFilterBoundaryRequest request,
        string boundaryName,
        CancellationToken cancellationToken)
    {
        var requestedFilter = MapRequestedFilter(request);
        var issues = new List<FilterValidationIssue>();
        ValidateRequestedFilter(requestedFilter, issues);

        var effectiveProductIds = ResolveProductIds(requestedFilter.ProductIds);
        var effectiveTeamIds = ResolveTeamIds(requestedFilter.TeamIds, issues);

        if (effectiveProductIds.IsAll && !effectiveTeamIds.IsAll)
        {
            var linkedProductIds = await _context.ProductTeamLinks
                .AsNoTracking()
                .Where(link => effectiveTeamIds.Values.Contains(link.TeamId))
                .Select(link => link.ProductId)
                .Distinct()
                .OrderBy(productId => productId)
                .ToArrayAsync(cancellationToken);

            effectiveProductIds = linkedProductIds.Length == 0
                ? FilterSelection<int>.Selected(Array.Empty<int>())
                : FilterSelection<int>.Selected(linkedProductIds);
        }

        var repositoryUniverse = await LoadRepositoryUniverseAsync(effectiveProductIds, cancellationToken);
        var repositoryScope = ResolveRepositoryScope(
            requestedFilter.RepositoryNames,
            repositoryUniverse,
            issues);

        var (effectiveTime, rangeStartUtc, rangeEndUtc, sprintId, sprintIds) = await ResolveTimeAsync(
            requestedFilter.Time,
            cancellationToken,
            issues);

        var effectiveFilterContext = new PullRequestFilterContext(
            effectiveProductIds,
            effectiveTeamIds,
            requestedFilter.RepositoryNames.IsAll
                ? FilterSelection<string>.All()
                : FilterSelection<string>.Selected(requestedFilter.RepositoryNames.Values),
            NormalizeStringSelection(requestedFilter.IterationPaths),
            NormalizeStringSelection(requestedFilter.CreatedBys),
            NormalizeStringSelection(requestedFilter.Statuses),
            effectiveTime);

        var effectiveFilter = new PullRequestEffectiveFilter(
            effectiveFilterContext,
            repositoryScope,
            rangeStartUtc,
            rangeEndUtc,
            sprintId,
            sprintIds);

        var validation = FilterValidationResult.FromIssues(issues);

        _logger.LogInformation(
            "Resolved PR filter for {Boundary}. RequestedFilter: {@RequestedFilter}; EffectiveFilter: {@EffectiveFilter}; InvalidFields: {@InvalidFields}; RepositoryScopeCount: {RepositoryScopeCount}; RangeStartUtc: {RangeStartUtc}; RangeEndUtc: {RangeEndUtc}",
            boundaryName,
            requestedFilter,
            effectiveFilter,
            validation.InvalidFields,
            effectiveFilter.RepositoryScope.Count,
            effectiveFilter.RangeStartUtc,
            effectiveFilter.RangeEndUtc);

        return new PullRequestFilterResolution(requestedFilter, effectiveFilter, validation);
    }

    public static PullRequestQueryResponseDto<T> ToResponse<T>(T data, PullRequestFilterResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new PullRequestQueryResponseDto<T>
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

    private static PullRequestFilterContext MapRequestedFilter(PullRequestFilterBoundaryRequest request)
        => new(
            ToIntSelection(request.ProductIds),
            request.TeamId.HasValue
                ? FilterSelection<int>.Selected([request.TeamId.Value])
                : FilterSelection<int>.All(),
            ToStringSelection(request.RepositoryName),
            ToStringSelection(request.IterationPath),
            ToStringSelection(request.CreatedBy),
            ToStringSelection(request.Status),
            MapTime(request));

    private static FilterTimeSelection MapTime(PullRequestFilterBoundaryRequest request)
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
        PullRequestFilterContext filter,
        ICollection<FilterValidationIssue> issues)
    {
        ValidateIntSelection(filter.ProductIds, nameof(PullRequestFilterContext.ProductIds), issues);
        ValidateIntSelection(filter.TeamIds, nameof(PullRequestFilterContext.TeamIds), issues);
        ValidateStringSelection(filter.RepositoryNames, nameof(PullRequestFilterContext.RepositoryNames), issues);
        ValidateStringSelection(filter.IterationPaths, nameof(PullRequestFilterContext.IterationPaths), issues);
        ValidateStringSelection(filter.CreatedBys, nameof(PullRequestFilterContext.CreatedBys), issues);
        ValidateStringSelection(filter.Statuses, nameof(PullRequestFilterContext.Statuses), issues);

        switch (filter.Time.Mode)
        {
            case FilterTimeSelectionMode.None:
                break;
            case FilterTimeSelectionMode.Sprint:
                if (!filter.Time.SprintId.HasValue || filter.Time.SprintId.Value <= 0)
                {
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Single-sprint time selection requires a valid sprint identifier."));
                }
                break;
            case FilterTimeSelectionMode.MultiSprint:
                if (filter.Time.SprintIds.Count == 0 || filter.Time.SprintIds.Any(id => id <= 0))
                {
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Multi-sprint time selection requires one or more valid sprint identifiers."));
                }
                break;
            case FilterTimeSelectionMode.DateRange:
                if (filter.Time.RangeStartUtc.HasValue
                    && filter.Time.RangeEndUtc.HasValue
                    && filter.Time.RangeStartUtc.Value > filter.Time.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Date-range time selection requires the start to be earlier than or equal to the end."));
                }
                break;
            default:
                issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Unsupported time selection mode."));
                break;
        }
    }

    private static FilterSelection<int> ResolveProductIds(FilterSelection<int> requested)
    {
        if (requested.IsAll)
        {
            return FilterSelection<int>.All();
        }

        return FilterSelection<int>.Selected(requested.Values.Where(value => value > 0).Distinct());
    }

    private static FilterSelection<int> ResolveTeamIds(
        FilterSelection<int> requested,
        ICollection<FilterValidationIssue> issues)
    {
        if (requested.IsAll)
        {
            return FilterSelection<int>.All();
        }

        var teamIds = requested.Values.Where(value => value > 0).Distinct().ToArray();
        if (teamIds.Length == 0)
        {
            issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.TeamIds), "Team selection cannot be empty when not using ALL."));
            return FilterSelection<int>.Selected(Array.Empty<int>());
        }

        return FilterSelection<int>.Selected(teamIds);
    }

    private async Task<IReadOnlyList<string>> LoadRepositoryUniverseAsync(
        FilterSelection<int> productIds,
        CancellationToken cancellationToken)
    {
        var configuredRepositoryNamesQuery = _context.Repositories
            .AsNoTracking()
            .Select(repository => new { repository.ProductId, repository.Name });

        var cachedRepositoryNamesQuery = _context.PullRequests
            .AsNoTracking()
            .Where(pr => !string.IsNullOrWhiteSpace(pr.RepositoryName))
            .Select(pr => new { ProductId = pr.ProductId ?? 0, Name = pr.RepositoryName });

        if (!productIds.IsAll)
        {
            var scopedProductIds = productIds.Values.ToArray();
            configuredRepositoryNamesQuery = configuredRepositoryNamesQuery
                .Where(repository => scopedProductIds.Contains(repository.ProductId));
            cachedRepositoryNamesQuery = cachedRepositoryNamesQuery
                .Where(pr => scopedProductIds.Contains(pr.ProductId));
        }

        var configuredRepositoryNames = await configuredRepositoryNamesQuery
            .Select(repository => repository.Name)
            .ToListAsync(cancellationToken);
        var cachedRepositoryNames = await cachedRepositoryNamesQuery
            .Select(pr => pr.Name)
            .ToListAsync(cancellationToken);

        return configuredRepositoryNames
            .Concat(cachedRepositoryNames)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRepositoryScope(
        FilterSelection<string> requestedRepositories,
        IReadOnlyList<string> repositoryUniverse,
        ICollection<FilterValidationIssue> issues)
    {
        if (!requestedRepositories.IsAll)
        {
            var requestedValues = requestedRepositories.Values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (requestedValues.Length == 0)
            {
                issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.RepositoryNames), "Repository selection cannot be empty when not using ALL."));
            }

            var universeLookup = repositoryUniverse.ToDictionary(value => value, value => value, StringComparer.OrdinalIgnoreCase);
            return requestedValues
                .Where(universeLookup.ContainsKey)
                .Select(value => universeLookup[value])
                .ToArray();
        }

        return repositoryUniverse;
    }

    private async Task<(FilterTimeSelection Time, DateTimeOffset? RangeStartUtc, DateTimeOffset? RangeEndUtc, int? SprintId, IReadOnlyList<int> SprintIds)> ResolveTimeAsync(
        FilterTimeSelection requestedTime,
        CancellationToken cancellationToken,
        ICollection<FilterValidationIssue> issues)
    {
        switch (requestedTime.Mode)
        {
            case FilterTimeSelectionMode.None:
                return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());

            case FilterTimeSelectionMode.DateRange:
                if (requestedTime.RangeStartUtc.HasValue
                    && requestedTime.RangeEndUtc.HasValue
                    && requestedTime.RangeStartUtc.Value > requestedTime.RangeEndUtc.Value)
                {
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Invalid date range was replaced with no time constraint."));
                    return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
                }

                return (
                    FilterTimeSelection.DateRange(requestedTime.RangeStartUtc, requestedTime.RangeEndUtc),
                    requestedTime.RangeStartUtc,
                    requestedTime.RangeEndUtc,
                    null,
                    Array.Empty<int>());

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
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Requested sprint was not found or has no valid date boundaries."));
                    return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
                }

                return (
                    FilterTimeSelection.Sprint(sprint.Id),
                    new DateTimeOffset(sprint.StartDateUtc!.Value, TimeSpan.Zero),
                    new DateTimeOffset(sprint.EndDateUtc!.Value, TimeSpan.Zero),
                    sprint.Id,
                    Array.Empty<int>());
            }

            case FilterTimeSelectionMode.MultiSprint:
            {
                var requestedSprintIds = requestedTime.SprintIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToArray();

                var sprints = await _context.Sprints
                    .AsNoTracking()
                    .Where(sprint => requestedSprintIds.Contains(sprint.Id)
                        && sprint.StartDateUtc.HasValue
                        && sprint.EndDateUtc.HasValue)
                    .OrderBy(sprint => sprint.StartDateUtc)
                    .ToListAsync(cancellationToken);

                if (sprints.Count != requestedSprintIds.Length)
                {
                    issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "One or more requested sprints were not found or have invalid date boundaries."));
                }

                if (sprints.Count == 0)
                {
                    return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
                }

                return (
                    FilterTimeSelection.MultiSprint(sprints.Select(sprint => sprint.Id)),
                    new DateTimeOffset(sprints.First().StartDateUtc!.Value, TimeSpan.Zero),
                    new DateTimeOffset(sprints.Last().EndDateUtc!.Value, TimeSpan.Zero),
                    null,
                    sprints.Select(sprint => sprint.Id).ToArray());
            }

            default:
                issues.Add(new FilterValidationIssue(nameof(PullRequestFilterContext.Time), "Unsupported PR time filter."));
                return (FilterTimeSelection.None(), null, null, null, Array.Empty<int>());
        }
    }

    private static PullRequestFilterContextDto ToDto(PullRequestFilterContext filter)
        => new()
        {
            ProductIds = ToDto(filter.ProductIds),
            TeamIds = ToDto(filter.TeamIds),
            RepositoryNames = ToDto(filter.RepositoryNames),
            IterationPaths = ToDto(filter.IterationPaths),
            CreatedBys = ToDto(filter.CreatedBys),
            Statuses = ToDto(filter.Statuses),
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

    private static FilterSelection<string> ToStringSelection(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? FilterSelection<string>.All()
            : FilterSelection<string>.Selected([value.Trim()]);

    private static FilterSelection<string> NormalizeStringSelection(FilterSelection<string> selection)
        => selection.IsAll
            ? FilterSelection<string>.All()
            : FilterSelection<string>.Selected(
                selection.Values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));

    private static void ValidateIntSelection(
        FilterSelection<int> selection,
        string fieldName,
        ICollection<FilterValidationIssue> issues)
    {
        if (!selection.IsAll && (selection.Values.Count == 0 || selection.Values.Any(value => value <= 0)))
        {
            issues.Add(new FilterValidationIssue(fieldName, $"{fieldName} contains one or more invalid values."));
        }
    }

    private static void ValidateStringSelection(
        FilterSelection<string> selection,
        string fieldName,
        ICollection<FilterValidationIssue> issues)
    {
        if (!selection.IsAll && selection.Values.Any(value => string.IsNullOrWhiteSpace(value)))
        {
            issues.Add(new FilterValidationIssue(fieldName, $"{fieldName} contains one or more invalid values."));
        }
    }
}
