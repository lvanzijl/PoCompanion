using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;
using PoTool.Api.Services.MockData;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Contracts.TfsVerification;

using PoTool.Core.PullRequests;

using PoTool.Core.WorkItems;

using PoTool.Core.Pipelines;

namespace PoTool.Api.Services;

/// <summary>
/// Mock TFS client for development and testing purposes.
/// Returns predefined mock data instead of connecting to a real TFS/Azure DevOps instance.
/// </summary>
public class MockTfsClient : ITfsClient
{
    private readonly BattleshipMockDataFacade _mockDataFacade;
    private readonly ILogger<MockTfsClient> _logger;

    public MockTfsClient(
        BattleshipMockDataFacade mockDataFacade,
        ILogger<MockTfsClient> logger)
    {
        _mockDataFacade = mockDataFacade;
        _logger = logger;
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: ValidateConnectionAsync called - always returns true");
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetAreaPathsAsync called with depth={Depth}", depth);

        // Get all mock work items to extract area paths
        var allWorkItems = _mockDataFacade.GetMockHierarchy();

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

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemsAsync called for areaPath={AreaPath}, since={Since}", areaPath, since);

        // Get all mock work items from the new Battleship system
        var allWorkItems = _mockDataFacade.GetMockHierarchy();

        // Filter by area path (simple contains check for mock purposes)
        var filtered = allWorkItems.Where(wi => wi.AreaPath.Contains(areaPath, StringComparison.OrdinalIgnoreCase));

        // Filter by date if specified (incremental sync)
        if (since.HasValue)
        {
            filtered = filtered.Where(wi => wi.RetrievedAt >= since.Value);
        }

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} work items", result.Count);

        return Task.FromResult<IEnumerable<WorkItemDto>>(result);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemsByRootIdsAsync called for rootIds=[{RootIds}], since={Since}",
            string.Join(", ", rootWorkItemIds), since);

        // Get all mock work items
        var allWorkItems = _mockDataFacade.GetMockHierarchy().ToList();

        // Build hierarchy map to find descendants
        // CRITICAL: Discovery phase NEVER filters by 'since' parameter
        // The complete hierarchy must always be discovered
        // 'since' parameter is reserved for future refresh optimization logic (not implemented yet)
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

        // NOTE: 'since' parameter is intentionally NOT used here for filtering.
        // Discovery phase must ALWAYS return the complete hierarchy.
        // Incremental sync logic (if needed) should be applied AFTER discovery
        // to decide which items need field refresh, not which items exist in the graph.
        // This ensures graph structure is never affected by incremental sync dates.

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
        _logger.LogInformation("Mock TFS client: GetWorkItemsByRootIdsWithDetailedProgressAsync called for rootIds=[{RootIds}], since={Since}",
            string.Join(", ", rootWorkItemIds), since);

        // Get all mock work items
        var allWorkItems = _mockDataFacade.GetMockHierarchy().ToList();

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

        // Report progress with detailed structure

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

    public Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemByIdAsync called for work item {WorkItemId}", workItemId);

        // Get all mock work items from the new Battleship system
        var allWorkItems = _mockDataFacade.GetMockHierarchy();

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

    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPullRequestsAsync called for repository={Repository}", repositoryName);

        // Get all mock pull requests from the new Battleship system
        var allPullRequests = _mockDataFacade.GetMockPullRequests();

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
        _logger.LogInformation("Mock TFS client: GetPullRequestIterationsAsync called for PR {PullRequestId}", pullRequestId);

        var allIterations = _mockDataFacade.GetMockIterations();
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
        _logger.LogInformation("Mock TFS client: GetPullRequestCommentsAsync called for PR {PullRequestId}", pullRequestId);

        var allComments = _mockDataFacade.GetMockComments();
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
        _logger.LogInformation("Mock TFS client: GetPullRequestFileChangesAsync called for PR {PullRequestId}, iteration {IterationId}",
            pullRequestId, iterationId);

        var allFileChanges = _mockDataFacade.GetMockFileChanges();
        var filtered = allFileChanges.Where(fc => fc.PullRequestId == pullRequestId && fc.IterationId == iterationId);

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} file changes", result.Count);
        return Task.FromResult<IEnumerable<PullRequestFileChangeDto>>(result);
    }

    public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemRevisionsAsync called for work item {WorkItemId}", workItemId);

        // Return sample revision history - simplified mock data
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
        _logger.LogInformation("Mock TFS client: UpdateWorkItemStateAsync called for workItemId={WorkItemId}, newState={NewState}",
            workItemId, newState);

        // Mock implementation always succeeds for valid state values
        var validStates = new[] { "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed", "Proposed", "Completed", "Approved", "Committed", "To Do" };
        var isValidState = validStates.Contains(newState, StringComparer.OrdinalIgnoreCase);

        if (!isValidState)
        {
            _logger.LogWarning("Mock TFS client: Invalid state '{NewState}' provided", newState);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} to state '{NewState}'",
            workItemId, newState);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: UpdateWorkItemEffortAsync called for workItemId={WorkItemId}, effort={Effort}",
            workItemId, effort);

        // Mock implementation always succeeds for valid effort values
        if (effort < 0)
        {
            _logger.LogWarning("Mock TFS client: Invalid effort value {Effort} provided (must be >= 0)", effort);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} effort to {Effort}",
            workItemId, effort);
        return Task.FromResult(true);
    }

    public Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: VerifyCapabilitiesAsync called. WriteChecks: {IncludeWriteChecks}",
            includeWriteChecks);

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
            ServerUrl = "https://mock-tfs.example.com",
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
    // Delegates to BattleshipMockDataFacade for implementation.
    // ============================================

    /// <summary>
    /// Returns all PR data in a single call. Delegates to BattleshipMockDataFacade.
    /// </summary>

    /// <summary>
    /// Updates effort for multiple work items in a single batch call.
    /// </summary>
    public Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        return _mockDataFacade.UpdateWorkItemsEffortAsync(updates, cancellationToken);
    }

    /// <summary>
    /// Updates state for multiple work items in a single batch call.
    /// </summary>
    public Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        return _mockDataFacade.UpdateWorkItemsStateAsync(updates, cancellationToken);
    }

    /// <summary>
    /// Gets revision history for multiple work items in a single batch call.
    /// </summary>
    public Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        return _mockDataFacade.GetWorkItemRevisionsBatchAsync(workItemIds, cancellationToken);
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
    // PIPELINE METHODS - Delegate to BattleshipMockDataFacade
    // ============================================

    public Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        return _mockDataFacade.GetPipelinesAsync(cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        return _mockDataFacade.GetPipelineRunsAsync(pipelineId, top, cancellationToken);
    }


    public Task<string?> GetRepositoryIdByNameAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetRepositoryIdByNameAsync called for repository={RepoName}", repositoryName);
        
        // Return a deterministic mock GUID based on repository name
        var mockGuid = $"mock-repo-{repositoryName.ToLowerInvariant()}-guid";
        return Task.FromResult<string?>(mockGuid);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPipelineDefinitionsForRepositoryAsync called for repository={RepoName}", repositoryName);
        
        // Return mock pipeline definitions (will be generated by mock data facade)
        return _mockDataFacade.GetPipelineDefinitionsForRepositoryAsync(repositoryName, cancellationToken);
    }

    // ============================================
    // TEAMS
    // ============================================

    public Task<IEnumerable<TfsTeamDto>> GetTfsTeamsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetTfsTeamsAsync called");

        // Return mock teams for the configured project
        var teams = new List<TfsTeamDto>
        {
            new TfsTeamDto(
                "team-1-guid",
                "Team Alpha",
                "MockProject",
                "Main development team",
                "MockProject\\Team Alpha"
            ),
            new TfsTeamDto(
                "team-2-guid",
                "Team Beta",
                "MockProject",
                "Quality assurance team",
                "MockProject\\Team Beta"
            ),
            new TfsTeamDto(
                "team-3-guid",
                "Team Gamma",
                "MockProject",
                "Infrastructure team",
                "MockProject\\Infrastructure"
            )
        };

        return Task.FromResult<IEnumerable<TfsTeamDto>>(teams);
    }

    // ============================================
    // TEAM ITERATIONS (SPRINTS)
    // ============================================

    public Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(
        string projectName,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock TFS client: GetTeamIterationsAsync called for Project='{Project}', Team='{Team}'",
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
            "Mock TFS client: Returning {Count} team iterations for Project='{Project}', Team='{Team}'",
            iterations.Count, projectName, teamName);

        return Task.FromResult<IEnumerable<TeamIterationDto>>(iterations);
    }
}
