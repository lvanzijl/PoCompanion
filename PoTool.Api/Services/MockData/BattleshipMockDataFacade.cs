using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;
using PoTool.Core.Contracts;
using PoTool.Shared.Contracts.TfsVerification;
using Microsoft.Extensions.Logging;
using System.Text.Json;

using PoTool.Core.PullRequests;

using PoTool.Core.WorkItems;

using PoTool.Core.Pipelines;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Facade that coordinates all mock data generators with hybrid caching.
/// Implements ITfsClient so real and mock paths share the exact same interface.
/// Performance is a first-class concern - mock implementations expose inefficient access patterns early.
/// </summary>
public class BattleshipMockDataFacade : ITfsClient
{
    // Performance instrumentation: track API call counts to surface N+1 patterns
    private int _apiCallCount;
    private readonly object _apiCallCountLock = new object();
    private readonly BattleshipWorkItemGenerator _workItemGenerator;
    private readonly BattleshipDependencyGenerator _dependencyGenerator;
    private readonly BattleshipPullRequestGenerator _pullRequestGenerator;
    private readonly BattleshipPipelineGenerator _pipelineGenerator;
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
    private List<PipelineDto>? _cachedPipelines;
    private List<PipelineRunDto>? _cachedPipelineRuns;
    private ValidationReport? _cachedValidationReport;
    private readonly object _cacheLock = new object();

    public BattleshipMockDataFacade(
        BattleshipWorkItemGenerator workItemGenerator,
        BattleshipDependencyGenerator dependencyGenerator,
        BattleshipPullRequestGenerator pullRequestGenerator,
        BattleshipPipelineGenerator pipelineGenerator,
        MockDataValidator validator,
        ILogger<BattleshipMockDataFacade> logger)
    {
        _workItemGenerator = workItemGenerator;
        _dependencyGenerator = dependencyGenerator;
        _pullRequestGenerator = pullRequestGenerator;
        _pipelineGenerator = pipelineGenerator;
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
    /// Gets all mock pipelines (with caching)
    /// </summary>
    public List<PipelineDto> GetMockPipelines()
    {
        if (_cachedPipelines != null)
            return _cachedPipelines;

        lock (_cacheLock)
        {
            if (_cachedPipelines != null)
                return _cachedPipelines;

            _logger.LogInformation("Generating mock pipelines...");
            var startTime = DateTimeOffset.UtcNow;

            _cachedPipelines = _pipelineGenerator.GeneratePipelines();

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Generated {Count} pipelines in {Elapsed:F2} seconds",
                _cachedPipelines.Count,
                elapsed);

            return _cachedPipelines;
        }
    }

    /// <summary>
    /// Gets all mock pipeline runs (with caching)
    /// </summary>
    public List<PipelineRunDto> GetMockPipelineRuns()
    {
        if (_cachedPipelineRuns != null)
            return _cachedPipelineRuns;

        lock (_cacheLock)
        {
            if (_cachedPipelineRuns != null)
                return _cachedPipelineRuns;

            _logger.LogInformation("Generating mock pipeline runs...");
            var startTime = DateTimeOffset.UtcNow;

            var pipelines = GetMockPipelines();
            _cachedPipelineRuns = _pipelineGenerator.GenerateRuns(pipelines);

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Generated {Count} pipeline runs in {Elapsed:F2} seconds",
                _cachedPipelineRuns.Count,
                elapsed);

            return _cachedPipelineRuns;
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
            _cachedPipelines = null;
            _cachedPipelineRuns = null;
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
        GetMockPipelines();
        GetMockPipelineRuns();
        ValidateData();

        var elapsed = (DateTimeOffset.UtcNow - overallStartTime).TotalSeconds;
        _logger.LogInformation("Cache warmup completed in {Elapsed:F2} seconds", elapsed);
    }

    // ============================================
    // ITfsClient Implementation
    // ============================================

    /// <summary>
    /// Increments API call counter for performance instrumentation.
    /// Helps surface inefficient access patterns during development.
    /// </summary>
    private int IncrementAndGetApiCallCount()
    {
        lock (_apiCallCountLock)
        {
            return ++_apiCallCount;
        }
    }

    /// <summary>
    /// Resets the API call counter. Used for testing and monitoring.
    /// </summary>
    public void ResetApiCallCount()
    {
        lock (_apiCallCountLock)
        {
            _apiCallCount = 0;
        }
    }

    /// <summary>
    /// Gets the current API call count for performance monitoring.
    /// </summary>
    public int GetApiCallCount()
    {
        lock (_apiCallCountLock)
        {
            return _apiCallCount;
        }
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: ValidateConnectionAsync called - always returns true");
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetAreaPathsAsync with depth={Depth}", depth);

        // Get all mock work items to extract area paths
        var allWorkItems = GetMockHierarchy();

        // Extract distinct area paths from mock data
        var areaPaths = allWorkItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();

        _logger.LogInformation("Mock TFS client: Returning {Count} area paths", areaPaths.Count);

        return Task.FromResult<IEnumerable<string>>(areaPaths);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
    }

    public Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetWorkItemByIdAsync called for work item {WorkItemId}", workItemId);

        // Get all mock work items
        var allWorkItems = GetMockHierarchy();

        // Find the work item by TFS ID
        var workItem = allWorkItems.FirstOrDefault(wi => wi.TfsId == workItemId);

        if (workItem != null)
        {
            _logger.LogInformation("Mock TFS client: Found work item {WorkItemId}: {Title}", workItemId, workItem.Title);
        }
        else
        {
            _logger.LogInformation("Mock TFS client: Work item {WorkItemId} not found", workItemId);
        }

        return Task.FromResult(workItem);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetWorkItemsAsync called for areaPath={AreaPath}, since={Since}", areaPath, since);

        var allWorkItems = GetMockHierarchy();
        var filtered = allWorkItems.Where(wi => wi.AreaPath.Contains(areaPath, StringComparison.OrdinalIgnoreCase));

        if (since.HasValue)
        {
            filtered = filtered.Where(wi => wi.RetrievedAt >= since.Value);
        }

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} work items", result.Count);

        return Task.FromResult<IEnumerable<WorkItemDto>>(result);
    }

    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPullRequestsAsync called for repository={Repository}", repositoryName);

        var allPullRequests = GetMockPullRequests();
        var filtered = allPullRequests.AsEnumerable();

        if (!string.IsNullOrEmpty(repositoryName))
        {
            filtered = filtered.Where(pr => pr.RepositoryName.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= toDate.Value);
        }

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} pull requests", result.Count);

        return Task.FromResult<IEnumerable<PullRequestDto>>(result);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per PR in the old pattern.
        // Use GetPullRequestsWithDetailsAsync for efficient bulk fetching.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPullRequestIterationsAsync called for PR {PullRequestId} (N+1 pattern - prefer GetPullRequestsWithDetailsAsync)", pullRequestId);

        var allIterations = GetMockIterations();
        var filtered = allIterations.Where(i => i.PullRequestId == pullRequestId);

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} iterations", result.Count);
        return Task.FromResult<IEnumerable<PullRequestIterationDto>>(result);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per PR in the old pattern.
        // Use GetPullRequestsWithDetailsAsync for efficient bulk fetching.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPullRequestCommentsAsync called for PR {PullRequestId} (N+1 pattern - prefer GetPullRequestsWithDetailsAsync)", pullRequestId);

        var allComments = GetMockComments();
        var filtered = allComments.Where(c => c.PullRequestId == pullRequestId);

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} comments", result.Count);
        return Task.FromResult<IEnumerable<PullRequestCommentDto>>(result);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per iteration in the old pattern.
        // Use GetPullRequestsWithDetailsAsync for efficient bulk fetching.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPullRequestFileChangesAsync called for PR {PullRequestId}, iteration {IterationId} (N+1 pattern)", pullRequestId, iterationId);

        var allFileChanges = GetMockFileChanges();
        var filtered = allFileChanges.Where(fc => fc.PullRequestId == pullRequestId && fc.IterationId == iterationId);

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} file changes", result.Count);
        return Task.FromResult<IEnumerable<PullRequestFileChangeDto>>(result);
    }

    public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per work item in the old pattern.
        // Use GetWorkItemRevisionsBatchAsync for efficient bulk fetching.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetWorkItemRevisionsAsync called for work item {WorkItemId} (N+1 pattern - prefer GetWorkItemRevisionsBatchAsync)", workItemId);

        // Return sample revision history for the requested work item
        var mockRevisions = new List<WorkItemRevisionDto>
        {
            new WorkItemRevisionDto(
                RevisionNumber: 1,
                WorkItemId: workItemId,
                ChangedBy: "system.user@battleship.mil",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-10),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["System.Title"] = new WorkItemFieldChange("System.Title", null, "Initial Title"),
                    ["System.State"] = new WorkItemFieldChange("System.State", null, "New")
                },
                Comment: "Work item created"
            ),
            new WorkItemRevisionDto(
                RevisionNumber: 2,
                WorkItemId: workItemId,
                ChangedBy: "alice.johnson@battleship.mil",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-5),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["System.State"] = new WorkItemFieldChange("System.State", "New", "Active"),
                    ["System.AssignedTo"] = new WorkItemFieldChange("System.AssignedTo", null, "alice.johnson@battleship.mil")
                },
                Comment: "Started work on this item"
            )
        };

        _logger.LogInformation("Mock TFS client: Returning {Count} revisions", mockRevisions.Count);
        return Task.FromResult<IEnumerable<WorkItemRevisionDto>>(mockRevisions);
    }

    public Task<IReadOnlyList<WorkItemUpdate>> GetWorkItemUpdatesAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<WorkItemUpdate> updates =
        [
            new WorkItemUpdate
            {
                WorkItemId = workItemId,
                UpdateId = 1,
                RevisedDate = now.AddDays(-10),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.Title"] = new("System.Title", null, "Initial Title"),
                    ["System.State"] = new("System.State", null, "New")
                }
            },
            new WorkItemUpdate
            {
                WorkItemId = workItemId,
                UpdateId = 2,
                RevisedDate = now.AddDays(-5),
                FieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["System.State"] = new("System.State", "New", "Active")
                }
            }
        ];

        return Task.FromResult(updates);
    }

    public Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per work item in the old pattern.
        // Use UpdateWorkItemsStateAsync for efficient bulk updates.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemStateAsync called for workItemId={WorkItemId}, newState={NewState} (N+1 pattern - prefer UpdateWorkItemsStateAsync)",
            workItemId, newState);

        var validStates = new[] { "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed", "Proposed", "Completed", "Approved", "Committed", "To Do" };
        var isValidState = validStates.Contains(newState, StringComparer.OrdinalIgnoreCase);

        if (!isValidState)
        {
            _logger.LogWarning("Mock TFS client: Invalid state '{NewState}' provided", newState);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} to state '{NewState}'", workItemId, newState);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        // N+1 WARNING: This method is called once per work item in the old pattern.
        // Use UpdateWorkItemsEffortAsync for efficient bulk updates.
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemEffortAsync called for workItemId={WorkItemId}, effort={Effort} (N+1 pattern - prefer UpdateWorkItemsEffortAsync)",
            workItemId, effort);

        if (effort < 0)
        {
            _logger.LogWarning("Mock TFS client: Invalid effort value {Effort} provided (must be >= 0)", effort);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} effort to {Effort}", workItemId, effort);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemSeverityAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemSeverityAsync called for workItemId={WorkItemId}, severity='{Severity}'",
            workItemId, severity);

        var validSeverities = new[] { "1 - Critical", "2 - High", "3 - Medium", "4 - Low", "Critical", "High", "Medium", "Low" };
        if (string.IsNullOrEmpty(severity) || !validSeverities.Any(s => s.Equals(severity, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Mock TFS client: Invalid severity value '{Severity}' provided", severity);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} severity to '{Severity}'", workItemId, severity);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemTagsAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemTagsAsync called for workItemId={WorkItemId}, tags='{Tags}'",
            workItemId, string.Join("; ", tags));

        // Verify the work item exists in mock data
        var mockHierarchy = GetMockHierarchy();
        var workItem = mockHierarchy.FirstOrDefault(w => w.TfsId == workItemId);
        if (workItem != null)
        {
            _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} tags to '{Tags}'", 
                workItemId, string.Join("; ", tags));
            return Task.FromResult(true);
        }
        else
        {
            _logger.LogWarning("Mock TFS client: Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult(false);
        }
    }

    public Task<WorkItemDto?> UpdateWorkItemTagsAndReturnAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemTagsAndReturnAsync called for workItemId={WorkItemId}, tags='{Tags}'",
            workItemId, string.Join("; ", tags));

        // Verify the work item exists in mock data
        var mockHierarchy = GetMockHierarchy();
        var workItem = mockHierarchy.FirstOrDefault(w => w.TfsId == workItemId);
        if (workItem != null)
        {
            // Update tags and return the updated work item
            var tagsString = string.Join("; ", tags);
            var updatedWorkItem = workItem with { Tags = tagsString };
            
            _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} tags to '{Tags}' and returning updated item", 
                workItemId, tagsString);
            return Task.FromResult<WorkItemDto?>(updatedWorkItem);
        }
        else
        {
            _logger.LogWarning("Mock TFS client: Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult<WorkItemDto?>(null);
        }
    }

    public Task<WorkItemDto?> UpdateWorkItemSeverityAndReturnAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemSeverityAndReturnAsync called for workItemId={WorkItemId}, severity='{Severity}'",
            workItemId, severity);

        // Verify the work item exists in mock data
        var mockHierarchy = GetMockHierarchy();
        var workItem = mockHierarchy.FirstOrDefault(w => w.TfsId == workItemId);
        if (workItem != null)
        {
            // Update severity and return the updated work item
            var updatedWorkItem = workItem with { Severity = severity };
            
            _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} severity to '{Severity}' and returning updated item", 
                workItemId, severity);
            return Task.FromResult<WorkItemDto?>(updatedWorkItem);
        }
        else
        {
            _logger.LogWarning("Mock TFS client: Work item {WorkItemId} not found in mock data", workItemId);
            return Task.FromResult<WorkItemDto?>(null);
        }
    }

    public Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: VerifyCapabilitiesAsync called. WriteChecks: {IncludeWriteChecks}", includeWriteChecks);

        var checks = new List<TfsCapabilityCheckResult>
        {
            new TfsCapabilityCheckResult
            {
                CapabilityId = "server-reachability",
                Success = true,
                ImpactedFunctionality = "All TFS integration features",
                ExpectedBehavior = "Server responds to API requests with valid authentication",
                ObservedBehavior = "Mock server reachable, authentication successful"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "project-access",
                Success = true,
                ImpactedFunctionality = "Work item retrieval, project-specific operations",
                ExpectedBehavior = "Project exists and is accessible",
                ObservedBehavior = "Mock project 'Battleship' accessible"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-query",
                Success = true,
                ImpactedFunctionality = "Work item search and filtering",
                ExpectedBehavior = "WIQL queries execute successfully",
                ObservedBehavior = "Mock WIQL query executed successfully"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-fields",
                Success = true,
                ImpactedFunctionality = "Work item display and processing",
                ExpectedBehavior = "Required work item fields are accessible",
                ObservedBehavior = "All required fields present in mock data"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "batch-read",
                Success = true,
                ImpactedFunctionality = "Efficient work item synchronization",
                ExpectedBehavior = "Batch work item retrieval is supported",
                ObservedBehavior = "Mock batch API endpoint simulated successfully"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-revisions",
                Success = true,
                ImpactedFunctionality = "Work item history and change tracking",
                ExpectedBehavior = "Work item revision history API is accessible",
                ObservedBehavior = "Mock revision history endpoint accessible"
            },
            new TfsCapabilityCheckResult
            {
                CapabilityId = "pull-requests",
                Success = true,
                ImpactedFunctionality = "Pull request retrieval and analysis",
                ExpectedBehavior = "Git repositories and pull request API are accessible",
                ObservedBehavior = "Mock Git repositories API accessible (5 repositories)"
            }
        };

        if (includeWriteChecks && workItemIdForWriteCheck.HasValue)
        {
            checks.Add(new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-update",
                Success = true,
                ImpactedFunctionality = "Work item modifications (state changes, effort updates)",
                ExpectedBehavior = "Can update work item fields",
                ObservedBehavior = $"Mock work item {workItemIdForWriteCheck.Value} is writable",
                TargetScope = $"Work Item #{workItemIdForWriteCheck.Value}",
                MutationType = MutationType.Update,
                CleanupStatus = CleanupStatus.NotRequired
            });
        }

        var report = new TfsVerificationReport
        {
            VerifiedAt = DateTimeOffset.UtcNow,
            ServerUrl = "https://mock-tfs.battleship.mil",
            ProjectName = "Battleship",
            ApiVersion = "7.0",
            IncludedWriteChecks = includeWriteChecks,
            Success = true,
            Checks = checks
        };

        _logger.LogInformation("Mock TFS client: Verification completed successfully ({Count} checks)", checks.Count);
        return Task.FromResult(report);
    }

    // ============================================
    // BULK METHODS - Prevent N+1 query patterns
    // These methods are designed for efficient data retrieval and updates.
    // ============================================

    /// <summary>
    /// Returns all PR data in a single call. This is the efficient alternative to calling
    /// GetPullRequestsAsync + GetPullRequestIterationsAsync + GetPullRequestCommentsAsync + GetPullRequestFileChangesAsync
    /// for each PR. Reduces N+1 to O(1) calls.
    /// </summary>

    /// <summary>
    /// Updates effort for multiple work items in a single batch call.
    /// Reduces N+1 to O(1) calls by batching all updates together.
    /// </summary>
    public Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        // Single API call for all effort updates - no N+1 pattern
        IncrementAndGetApiCallCount();
        var updatesList = updates.ToList();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemsEffortAsync called for {Count} work items (efficient bulk method)", updatesList.Count);

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var update in updatesList)
        {
            if (update.EffortValue < 0)
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, false, $"Invalid effort value {update.EffortValue} (must be >= 0)"));
                failedCount++;
            }
            else
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, true));
                successCount++;
            }
        }

        var result = new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: 1 // Single batch call instead of N calls
        );

        _logger.LogInformation("Mock TFS client: Bulk updated {Success}/{Total} work item efforts in 1 call", successCount, updatesList.Count);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Updates state for multiple work items in a single batch call.
    /// Reduces N+1 to O(1) calls by batching all updates together.
    /// </summary>
    public Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        // Single API call for all state updates - no N+1 pattern
        IncrementAndGetApiCallCount();
        var updatesList = updates.ToList();
        _logger.LogInformation("Mock TFS client: UpdateWorkItemsStateAsync called for {Count} work items (efficient bulk method)", updatesList.Count);

        var validStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed",
            "Proposed", "Completed", "Approved", "Committed", "To Do"
        };

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var update in updatesList)
        {
            if (!validStates.Contains(update.NewState))
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, false, $"Invalid state '{update.NewState}'"));
                failedCount++;
            }
            else
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, true));
                successCount++;
            }
        }

        var result = new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: 1 // Single batch call instead of N calls
        );

        _logger.LogInformation("Mock TFS client: Bulk updated {Success}/{Total} work item states in 1 call", successCount, updatesList.Count);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets revision history for multiple work items in a single batch call.
    /// Reduces N+1 to O(1) calls by fetching all revisions together.
    /// </summary>
    public Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        // Single API call for all revision histories - no N+1 pattern
        IncrementAndGetApiCallCount();
        var idsList = workItemIds.ToList();
        _logger.LogInformation("Mock TFS client: GetWorkItemRevisionsBatchAsync called for {Count} work items (efficient bulk method)", idsList.Count);

        var results = new Dictionary<int, IEnumerable<WorkItemRevisionDto>>();

        foreach (var workItemId in idsList)
        {
            // Generate mock revision history for each work item
            var revisions = new List<WorkItemRevisionDto>
            {
                new WorkItemRevisionDto(
                    RevisionNumber: 1,
                    WorkItemId: workItemId,
                    ChangedBy: "system.user@battleship.mil",
                    ChangedDate: DateTimeOffset.UtcNow.AddDays(-10),
                    FieldChanges: new Dictionary<string, WorkItemFieldChange>
                    {
                        ["System.Title"] = new WorkItemFieldChange("System.Title", null, "Initial Title"),
                        ["System.State"] = new WorkItemFieldChange("System.State", null, "New")
                    },
                    Comment: "Work item created"
                ),
                new WorkItemRevisionDto(
                    RevisionNumber: 2,
                    WorkItemId: workItemId,
                    ChangedBy: "alice.johnson@battleship.mil",
                    ChangedDate: DateTimeOffset.UtcNow.AddDays(-5),
                    FieldChanges: new Dictionary<string, WorkItemFieldChange>
                    {
                        ["System.State"] = new WorkItemFieldChange("System.State", "New", "Active")
                    },
                    Comment: "Started work"
                )
            };
            results[workItemId] = revisions;
        }

        _logger.LogInformation("Mock TFS client: Bulk fetched revisions for {Count} work items in 1 call", idsList.Count);
        return Task.FromResult<IDictionary<int, IEnumerable<WorkItemRevisionDto>>>(results);
    }

    public Task<WorkItemCreateResult> CreateWorkItemAsync(
        WorkItemCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: CreateWorkItemAsync called for type={WorkItemType}, title={Title}",
            request.WorkItemType, request.Title);

        // Generate a mock work item ID
        var mockId = new Random().Next(100000, 999999);

        return Task.FromResult(new WorkItemCreateResult
        {
            Success = true,
            WorkItemId = mockId
        });
    }

    public Task<bool> UpdateWorkItemParentAsync(
        int workItemId,
        int newParentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: UpdateWorkItemParentAsync called for workItemId={WorkItemId}, newParentId={NewParentId}",
            workItemId, newParentId);

        return Task.FromResult(true);
    }

    // ============================================
    // PIPELINE METHODS
    // ============================================

    public Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPipelinesAsync called");

        var pipelines = GetMockPipelines();
        _logger.LogInformation("Mock TFS client: Returning {Count} pipelines", pipelines.Count);

        return Task.FromResult<IEnumerable<PipelineDto>>(pipelines);
    }

    public Task<PipelineDto?> GetPipelineByIdAsync(
        int pipelineId,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPipelineByIdAsync called for pipeline {PipelineId}", pipelineId);

        var pipelines = GetMockPipelines();
        var pipeline = pipelines.FirstOrDefault(p => p.Id == pipelineId);
        
        if (pipeline == null)
        {
            _logger.LogWarning("Mock TFS client: Pipeline {PipelineId} not found", pipelineId);
        }

        return Task.FromResult(pipeline);
    }

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPipelineRunsAsync called for pipeline {PipelineId}", pipelineId);

        var allRuns = GetMockPipelineRuns();
        var filtered = allRuns.Where(r => r.PipelineId == pipelineId).Take(top);

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} pipeline runs", result.Count);
        return Task.FromResult<IEnumerable<PipelineRunDto>>(result);
    }

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation(
            "Mock TFS client: GetPipelineRunsAsync called for {Count} pipelines with filters (branch: {Branch}, minTime: {MinTime})",
            pipelineIds.Count(), branchName ?? "none", minStartTime?.ToString("o") ?? "none");

        var allRuns = GetMockPipelineRuns();
        var pipelineIdSet = new HashSet<int>(pipelineIds);
        
        var filtered = allRuns.Where(r => pipelineIdSet.Contains(r.PipelineId));
        
        // Apply filters
        if (!string.IsNullOrEmpty(branchName))
        {
            filtered = filtered.Where(r => r.Branch == branchName);
        }
        
        if (minStartTime.HasValue)
        {
            filtered = filtered.Where(r => r.StartTime.HasValue && r.StartTime.Value >= minStartTime.Value);
        }
        
        // Take top N per pipeline
        var result = filtered
            .GroupBy(r => r.PipelineId)
            .SelectMany(g => g.OrderByDescending(r => r.StartTime).Take(top))
            .ToList();
        
        _logger.LogInformation("Mock TFS client: Returning {Count} pipeline runs", result.Count);
        return Task.FromResult<IEnumerable<PipelineRunDto>>(result);
    }


    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetWorkItemsByRootIdsAsync called for rootIds=[{RootIds}], since={Since}",
            string.Join(", ", rootWorkItemIds), since);

        // Get all mock work items
        var allWorkItems = GetMockHierarchy();

        // Build hierarchy map to find descendants
        var results = new List<WorkItemDto>();
        var processedIds = new HashSet<int>();

        void CollectHierarchy(int parentId)
        {
            if (processedIds.Contains(parentId)) return;

            var item = allWorkItems.FirstOrDefault(wi => wi.TfsId == parentId);
            if (item != null)
            {
                processedIds.Add(parentId);
                results.Add(item);

                // Find children
                var children = allWorkItems.Where(wi => wi.ParentTfsId == parentId);
                foreach (var child in children)
                {
                    CollectHierarchy(child.TfsId);
                }
            }
        }

        // Report progress
        progressCallback?.Invoke(1, 3, "Finding root work items...");

        foreach (var rootId in rootWorkItemIds)
        {
            CollectHierarchy(rootId);
        }

        progressCallback?.Invoke(2, 3, $"Processing {results.Count} work items...");

        // Filter by date if specified
        if (since.HasValue)
        {
            results = results.Where(wi => wi.RetrievedAt >= since.Value).ToList();
        }

        progressCallback?.Invoke(3, 3, "Complete");

        _logger.LogInformation("Mock TFS client: Returning {Count} work items for root IDs [{RootIds}]",
            results.Count, string.Join(", ", rootWorkItemIds));

        return Task.FromResult<IEnumerable<WorkItemDto>>(results);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetWorkItemsByRootIdsWithDetailedProgressAsync called for rootIds=[{RootIds}], since={Since}",
            string.Join(", ", rootWorkItemIds), since);

        // Get all mock work items
        var allWorkItems = GetMockHierarchy();

        // Build hierarchy map to find descendants
        var results = new List<WorkItemDto>();
        var processedIds = new HashSet<int>();

        void CollectHierarchy(int parentId)
        {
            if (processedIds.Contains(parentId)) return;

            var item = allWorkItems.FirstOrDefault(wi => wi.TfsId == parentId);
            if (item != null)
            {
                processedIds.Add(parentId);
                results.Add(item);

                // Find children
                var children = allWorkItems.Where(wi => wi.ParentTfsId == parentId);
                foreach (var child in children)
                {
                    CollectHierarchy(child.TfsId);
                }
            }
        }

        // Report detailed progress

        foreach (var rootId in rootWorkItemIds)
        {
            CollectHierarchy(rootId);
        }


        // Filter by date if specified
        if (since.HasValue)
        {
            results = results.Where(wi => wi.RetrievedAt >= since.Value).ToList();
        }


        _logger.LogInformation("Mock TFS client: Returning {Count} work items for root IDs [{RootIds}]",
            results.Count, string.Join(", ", rootWorkItemIds));

        return Task.FromResult<IEnumerable<WorkItemDto>>(results);
    }

    public Task<string?> GetRepositoryIdByNameAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetRepositoryIdByNameAsync called for repository={RepoName}", repositoryName);
        
        // Return a deterministic mock GUID based on repository name
        var mockGuid = $"mock-repo-{repositoryName.ToLowerInvariant()}-guid";
        return Task.FromResult<string?>(mockGuid);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPipelineDefinitionsForRepositoryAsync called for repository={RepoName}", repositoryName);

        // Generate mock pipeline definitions for the repository
        var mockRepoId = $"mock-repo-{repositoryName.ToLowerInvariant()}-guid";
        var syncTime = DateTimeOffset.UtcNow;

        var definitions = new List<PipelineDefinitionDto>
        {
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 101,
                RepoId = mockRepoId,
                RepoName = repositoryName,
                Name = $"{repositoryName} - CI Build",
                YamlPath = "/pipelines/ci-build.yml",
                Folder = "\\CI",
                Url = $"https://mock-tfs/{repositoryName}/_build?definitionId=101",
                LastSyncedUtc = syncTime
            },
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 102,
                RepoId = mockRepoId,
                RepoName = repositoryName,
                Name = $"{repositoryName} - PR Validation",
                YamlPath = "/pipelines/pr-validation.yml",
                Folder = "\\PR",
                Url = $"https://mock-tfs/{repositoryName}/_build?definitionId=102",
                LastSyncedUtc = syncTime
            },
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 103,
                RepoId = mockRepoId,
                RepoName = repositoryName,
                Name = $"{repositoryName} - Release",
                YamlPath = "/pipelines/release.yml",
                Folder = "\\Release",
                Url = $"https://mock-tfs/{repositoryName}/_build?definitionId=103",
                LastSyncedUtc = syncTime
            }
        };

        _logger.LogInformation("Mock TFS client: Returning {Count} pipeline definitions for repository '{RepoName}'",
            definitions.Count, repositoryName);

        return Task.FromResult<IEnumerable<PipelineDefinitionDto>>(definitions);
    }

    // ============================================
    // TEAMS
    // ============================================

    public Task<IEnumerable<TfsProjectDto>> GetTfsProjectsAsync(string organizationUrl, CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client (BattleshipMockDataFacade): GetTfsProjectsAsync called for URL: {OrganizationUrl}", organizationUrl);

        // Return mock projects accessible to the user
        var projects = new List<TfsProjectDto>
        {
            new TfsProjectDto(
                "battleship-project-guid",
                "Battleship",
                "Battleship game development project"
            ),
            new TfsProjectDto(
                "game-platform-guid",
                "GamePlatform",
                "Shared game platform and services"
            ),
            new TfsProjectDto(
                "infrastructure-guid",
                "Infrastructure",
                "DevOps and infrastructure automation"
            )
        };

        return Task.FromResult<IEnumerable<TfsProjectDto>>(projects);
    }

    public Task<IEnumerable<TfsTeamDto>> GetTfsTeamsAsync(CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client (BattleshipMockDataFacade): GetTfsTeamsAsync called");

        // Return mock teams for the Battleship project
        var teams = new List<TfsTeamDto>
        {
            new TfsTeamDto(
                "team-alpha-guid",
                "Battleship Alpha Squad",
                "Battleship",
                "Primary development team for Battleship game",
                "Battleship\\Alpha Squad"
            ),
            new TfsTeamDto(
                "team-beta-guid",
                "Battleship Beta Team",
                "Battleship",
                "Feature development and enhancements",
                "Battleship\\Beta Team"
            ),
            new TfsTeamDto(
                "team-ops-guid",
                "Battleship Operations",
                "Battleship",
                "DevOps and infrastructure support",
                "Battleship\\Operations"
            )
        };

        return Task.FromResult<IEnumerable<TfsTeamDto>>(teams);
    }

    public Task<IEnumerable<(string Name, string Id)>> GetGitRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client (BattleshipMockDataFacade): GetGitRepositoriesAsync called");

        // Return mock Git repositories for the Battleship project
        var repositories = new List<(string Name, string Id)>
        {
            ("Battleship.Game", "repo-game-guid"),
            ("Battleship.API", "repo-api-guid"),
            ("Battleship.UI", "repo-ui-guid"),
            ("Battleship.Infrastructure", "repo-infra-guid"),
            ("Battleship.Tests", "repo-tests-guid")
        };

        return Task.FromResult<IEnumerable<(string Name, string Id)>>(repositories);
    }

    // ============================================
    // TEAM ITERATIONS (SPRINTS)
    // ============================================

    public Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(
        string projectName,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        // Single API call
        IncrementAndGetApiCallCount();
        _logger.LogInformation(
            "Mock TFS client (BattleshipMockDataFacade): GetTeamIterationsAsync called for Project='{Project}', Team='{Team}'",
            projectName, teamName);

        // Return mock team iterations with past, current, and future sprints
        var now = DateTimeOffset.UtcNow;
        var iterations = new List<TeamIterationDto>
        {
            // Past sprint
            new TeamIterationDto(
                "iteration-past-id",
                "Sprint 10",
                $"\\{projectName}\\Sprint 10",
                now.AddDays(-28),
                now.AddDays(-14),
                "past"
            ),
            // Current sprint
            new TeamIterationDto(
                "iteration-current-id",
                "Sprint 11",
                $"\\{projectName}\\Sprint 11",
                now.AddDays(-7),
                now.AddDays(7),
                "current"
            ),
            // Future sprints
            new TeamIterationDto(
                "iteration-future-1-id",
                "Sprint 12",
                $"\\{projectName}\\Sprint 12",
                now.AddDays(7),
                now.AddDays(21),
                "future"
            ),
            new TeamIterationDto(
                "iteration-future-2-id",
                "Sprint 13",
                $"\\{projectName}\\Sprint 13",
                now.AddDays(21),
                now.AddDays(35),
                "future"
            ),
            // Sprint without attributes (null dates)
            new TeamIterationDto(
                "iteration-no-dates-id",
                "Sprint 14",
                $"\\{projectName}\\Sprint 14",
                null,
                null,
                null
            )
        };

        _logger.LogInformation(
            "Mock TFS client (BattleshipMockDataFacade): Returning {Count} team iterations for Project='{Project}', Team='{Team}'",
            iterations.Count, projectName, teamName);

        return Task.FromResult<IEnumerable<TeamIterationDto>>(iterations);
    }

    /// <summary>
    /// Mock implementation of GetWorkItemTypeDefinitionsAsync.
    /// Returns definitions matching the actual TFS project structure.
    /// </summary>
    public Task<IEnumerable<WorkItemTypeDefinitionDto>> GetWorkItemTypeDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client (BattleshipMockDataFacade): Returning work item type definitions");

        var definitions = new List<WorkItemTypeDefinitionDto>
        {
            new WorkItemTypeDefinitionDto
            {
                TypeName = "goal",
                States = new List<string> { "Proposed", "Active", "Completed", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Objective",
                States = new List<string> { "Proposed", "Active", "Completed", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Epic",
                States = new List<string> { "New", "Active", "Resolved", "Closed", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Feature",
                States = new List<string> { "New", "Active", "Resolved", "Closed", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Product Backlog Item",
                States = new List<string> { "New", "Approved", "Committed", "Done", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Bug",
                States = new List<string> { "New", "Approved", "Committed", "Done", "Removed" }.AsReadOnly()
            },
            new WorkItemTypeDefinitionDto
            {
                TypeName = "Task",
                States = new List<string> { "To Do", "In Progress", "Done", "Removed" }.AsReadOnly()
            }
        };

        return Task.FromResult<IEnumerable<WorkItemTypeDefinitionDto>>(definitions);
    }
}
