using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;
using PoTool.Core.Contracts;
using PoTool.Shared.Contracts.TfsVerification;
using Microsoft.Extensions.Logging;

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

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
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
    public Task<PullRequestSyncResult> GetPullRequestsWithDetailsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        // Single API call for all PR data - no N+1 pattern
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPullRequestsWithDetailsAsync called (efficient bulk method)");

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

        var prs = filtered.ToList();
        var prIds = prs.Select(p => p.Id).ToHashSet();

        // Get all related data for the filtered PRs
        var iterations = GetMockIterations().Where(i => prIds.Contains(i.PullRequestId)).ToList();
        var comments = GetMockComments().Where(c => prIds.Contains(c.PullRequestId)).ToList();
        var fileChanges = GetMockFileChanges().Where(fc => prIds.Contains(fc.PullRequestId)).ToList();

        var result = new PullRequestSyncResult(
            PullRequests: prs,
            Iterations: iterations,
            Comments: comments,
            FileChanges: fileChanges,
            TfsCallCount: 1 // Only 1 conceptual API call instead of 1 + 3*N
        );

        _logger.LogInformation(
            "Mock TFS client: Bulk fetched {PrCount} PRs, {IterCount} iterations, {CommentCount} comments, {FileCount} file changes in 1 call",
            prs.Count, iterations.Count, comments.Count, fileChanges.Count);

        return Task.FromResult(result);
    }

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

    public Task<PipelineSyncResult> GetPipelinesWithRunsAsync(
        int runsPerPipeline = 50,
        CancellationToken cancellationToken = default)
    {
        IncrementAndGetApiCallCount();
        _logger.LogInformation("Mock TFS client: GetPipelinesWithRunsAsync called (efficient bulk method)");

        var pipelines = GetMockPipelines();
        var allRuns = GetMockPipelineRuns();

        // Limit runs per pipeline
        var pipelineIds = pipelines.Select(p => p.Id).ToHashSet();
        var runs = allRuns
            .Where(r => pipelineIds.Contains(r.PipelineId))
            .GroupBy(r => r.PipelineId)
            .SelectMany(g => g.Take(runsPerPipeline))
            .ToList();

        var result = new PipelineSyncResult(
            Pipelines: pipelines,
            Runs: runs,
            TfsCallCount: 1,
            SyncedAt: DateTimeOffset.UtcNow
        );

        _logger.LogInformation(
            "Mock TFS client: Bulk fetched {PipelineCount} pipelines, {RunCount} runs in 1 call",
            pipelines.Count, runs.Count);

        return Task.FromResult(result);
    }
}
