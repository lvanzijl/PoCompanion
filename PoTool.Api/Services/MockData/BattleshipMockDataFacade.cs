using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using Microsoft.Extensions.Logging;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Facade that coordinates all mock data generators with hybrid caching.
/// Provides a single entry point for accessing all mock data.
/// </summary>
public class BattleshipMockDataFacade
{
    private readonly BattleshipWorkItemGenerator _workItemGenerator;
    private readonly BattleshipDependencyGenerator _dependencyGenerator;
    private readonly BattleshipPullRequestGenerator _pullRequestGenerator;
    private readonly MockDataValidator _validator;
    private readonly ILogger<BattleshipMockDataFacade> _logger;

    // Cache fields
    private List<WorkItemDto>? _cachedWorkItems;
    private List<DependencyLink>? _cachedDependencies;
    private List<PullRequestDto>? _cachedPullRequests;
    private List<PullRequestIterationDto>? _cachedIterations;
    private List<PullRequestCommentDto>? _cachedComments;
    private List<PullRequestFileChangeDto>? _cachedFileChanges;
    private List<PrWorkItemLink>? _cachedPrWorkItemLinks;
    private ValidationReport? _cachedValidationReport;
    private readonly object _cacheLock = new object();

    public BattleshipMockDataFacade(
        BattleshipWorkItemGenerator workItemGenerator,
        BattleshipDependencyGenerator dependencyGenerator,
        BattleshipPullRequestGenerator pullRequestGenerator,
        MockDataValidator validator,
        ILogger<BattleshipMockDataFacade> logger)
    {
        _workItemGenerator = workItemGenerator;
        _dependencyGenerator = dependencyGenerator;
        _pullRequestGenerator = pullRequestGenerator;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete mock work item hierarchy (with caching)
    /// </summary>
    public List<WorkItemDto> GetMockHierarchy()
    {
        if (_cachedWorkItems != null)
            return _cachedWorkItems;

        lock (_cacheLock)
        {
            if (_cachedWorkItems != null)
                return _cachedWorkItems;

            _logger.LogInformation("Generating mock work item hierarchy...");
            var startTime = DateTimeOffset.UtcNow;

            _cachedWorkItems = _workItemGenerator.GenerateHierarchy();

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Generated {Count} work items in {Elapsed:F2} seconds",
                _cachedWorkItems.Count,
                elapsed);

            return _cachedWorkItems;
        }
    }

    /// <summary>
    /// Gets mock hierarchy filtered by specific goal IDs
    /// </summary>
    public List<WorkItemDto> GetMockHierarchyForGoals(List<int> goalIds)
    {
        var allItems = GetMockHierarchy();
        return WorkItemHierarchyHelper.FilterDescendants(goalIds, allItems);
    }

    /// <summary>
    /// Gets all mock dependencies (with caching)
    /// </summary>
    public List<DependencyLink> GetMockDependencies()
    {
        if (_cachedDependencies != null)
            return _cachedDependencies;

        lock (_cacheLock)
        {
            if (_cachedDependencies != null)
                return _cachedDependencies;

            _logger.LogInformation("Generating mock dependencies...");
            var startTime = DateTimeOffset.UtcNow;

            var workItems = GetMockHierarchy();
            var validDependencies = _dependencyGenerator.GenerateDependencies(workItems);
            var invalidDependencies = _dependencyGenerator.GenerateInvalidDependencies(workItems, validDependencies.Count);

            _cachedDependencies = validDependencies.Concat(invalidDependencies).ToList();

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Generated {Count} dependency links in {Elapsed:F2} seconds",
                _cachedDependencies.Count,
                elapsed);

            return _cachedDependencies;
        }
    }

    /// <summary>
    /// Gets all mock pull requests (with caching)
    /// </summary>
    public List<PullRequestDto> GetMockPullRequests()
    {
        if (_cachedPullRequests != null)
            return _cachedPullRequests;

        lock (_cacheLock)
        {
            if (_cachedPullRequests != null)
                return _cachedPullRequests;

            _logger.LogInformation("Generating mock pull requests...");
            var startTime = DateTimeOffset.UtcNow;

            var workItems = GetMockHierarchy();
            _cachedPullRequests = _pullRequestGenerator.GeneratePullRequests(workItems.Count);

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Generated {Count} pull requests in {Elapsed:F2} seconds",
                _cachedPullRequests.Count,
                elapsed);

            return _cachedPullRequests;
        }
    }

    /// <summary>
    /// Gets mock PR iterations (with caching)
    /// </summary>
    public List<PullRequestIterationDto> GetMockIterations()
    {
        if (_cachedIterations != null)
            return _cachedIterations;

        lock (_cacheLock)
        {
            if (_cachedIterations != null)
                return _cachedIterations;

            var pullRequests = GetMockPullRequests();
            _cachedIterations = _pullRequestGenerator.GenerateIterations(pullRequests);

            _logger.LogInformation("Generated {Count} PR iterations", _cachedIterations.Count);
            return _cachedIterations;
        }
    }

    /// <summary>
    /// Gets mock PR comments (with caching)
    /// </summary>
    public List<PullRequestCommentDto> GetMockComments()
    {
        if (_cachedComments != null)
            return _cachedComments;

        lock (_cacheLock)
        {
            if (_cachedComments != null)
                return _cachedComments;

            var pullRequests = GetMockPullRequests();
            _cachedComments = _pullRequestGenerator.GenerateComments(pullRequests);

            _logger.LogInformation("Generated {Count} PR comments", _cachedComments.Count);
            return _cachedComments;
        }
    }

    /// <summary>
    /// Gets mock PR file changes (with caching)
    /// </summary>
    public List<PullRequestFileChangeDto> GetMockFileChanges()
    {
        if (_cachedFileChanges != null)
            return _cachedFileChanges;

        lock (_cacheLock)
        {
            if (_cachedFileChanges != null)
                return _cachedFileChanges;

            var pullRequests = GetMockPullRequests();
            _cachedFileChanges = _pullRequestGenerator.GenerateFileChanges(pullRequests);

            _logger.LogInformation("Generated {Count} PR file changes", _cachedFileChanges.Count);
            return _cachedFileChanges;
        }
    }

    /// <summary>
    /// Gets mock PR-to-WorkItem links (with caching)
    /// </summary>
    public List<PrWorkItemLink> GetMockPrWorkItemLinks()
    {
        if (_cachedPrWorkItemLinks != null)
            return _cachedPrWorkItemLinks;

        lock (_cacheLock)
        {
            if (_cachedPrWorkItemLinks != null)
                return _cachedPrWorkItemLinks;

            var pullRequests = GetMockPullRequests();
            var workItems = GetMockHierarchy();
            _cachedPrWorkItemLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, workItems.Count);

            _logger.LogInformation("Generated {Count} PR-WorkItem links", _cachedPrWorkItemLinks.Count);
            return _cachedPrWorkItemLinks;
        }
    }

    /// <summary>
    /// Gets the IDs of all generated Goals
    /// </summary>
    public List<int> GetGoalIds()
    {
        var workItems = GetMockHierarchy();
        return workItems
            .Where(w => w.Type == WorkItemType.Goal)
            .Select(w => w.TfsId)
            .ToList();
    }

    /// <summary>
    /// Gets the first N goal IDs (useful for default settings)
    /// </summary>
    public List<int> GetDefaultGoalIds(int count = 2)
    {
        return GetGoalIds().Take(count).ToList();
    }

    /// <summary>
    /// Validates all generated mock data and returns a validation report
    /// </summary>
    public ValidationReport ValidateData()
    {
        if (_cachedValidationReport != null)
            return _cachedValidationReport;

        lock (_cacheLock)
        {
            if (_cachedValidationReport != null)
                return _cachedValidationReport;

            _logger.LogInformation("Validating mock data...");
            var startTime = DateTimeOffset.UtcNow;

            var report = new ValidationReport();

            // Validate work items
            var workItems = GetMockHierarchy();
            report = _validator.ValidateWorkItems(workItems);

            // Validate dependencies
            var dependencies = GetMockDependencies();
            _validator.ValidateDependencies(dependencies, workItems, report);

            // Validate pull requests
            var pullRequests = GetMockPullRequests();
            var prWorkItemLinks = GetMockPrWorkItemLinks();
            _validator.ValidatePullRequests(pullRequests, prWorkItemLinks, report);

            _cachedValidationReport = report;

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Validation completed in {Elapsed:F2} seconds. Overall valid: {IsValid}",
                elapsed,
                report.IsValid());

            // Log the full report
            _logger.LogInformation("Validation Report:\n{Report}", report.GetSummary());

            return _cachedValidationReport;
        }
    }

    /// <summary>
    /// Invalidates the cache and forces regeneration on next access
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _logger.LogInformation("Invalidating mock data cache");
            _cachedWorkItems = null;
            _cachedDependencies = null;
            _cachedPullRequests = null;
            _cachedIterations = null;
            _cachedComments = null;
            _cachedFileChanges = null;
            _cachedPrWorkItemLinks = null;
            _cachedValidationReport = null;
        }
    }

    /// <summary>
    /// Pre-generates all data (useful for warming up the cache)
    /// </summary>
    public void WarmupCache()
    {
        _logger.LogInformation("Warming up mock data cache...");
        var overallStartTime = DateTimeOffset.UtcNow;

        GetMockHierarchy();
        GetMockDependencies();
        GetMockPullRequests();
        GetMockIterations();
        GetMockComments();
        GetMockFileChanges();
        GetMockPrWorkItemLinks();
        ValidateData();

        var elapsed = (DateTimeOffset.UtcNow - overallStartTime).TotalSeconds;
        _logger.LogInformation("Cache warmup completed in {Elapsed:F2} seconds", elapsed);
    }
}
