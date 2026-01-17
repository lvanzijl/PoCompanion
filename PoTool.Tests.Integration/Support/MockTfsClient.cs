using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Pipelines;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Mock TFS client for integration testing.
/// Uses file-based test data instead of real TFS API calls.
/// </summary>
public class MockTfsClient : ITfsClient
{
    private readonly List<WorkItemDto> _mockWorkItems = new();
    private readonly List<PullRequestDto> _mockPullRequests = new();
    private readonly List<PullRequestIterationDto> _mockIterations = new();
    private readonly List<PullRequestCommentDto> _mockComments = new();
    private readonly List<PullRequestFileChangeDto> _mockFileChanges = new();

    public MockTfsClient()
    {
        // Initialize with sample test data
        _mockWorkItems = new List<WorkItemDto>
        {
            new WorkItemDto(
                TfsId: 1000,
                Type: "Goal",
                Title: "Test Goal",
                ParentTfsId: null,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null
            ),
            new WorkItemDto(
                TfsId: 1001,
                Type: "Objective",
                Title: "Test Objective",
                ParentTfsId: 1000,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null
            ),
            new WorkItemDto(
                TfsId: 1002,
                Type: "Epic",
                Title: "Test Epic",
                ParentTfsId: 1001,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "New",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null
            )
        };

        // Initialize mock pull requests
        _mockPullRequests = new List<PullRequestDto>
        {
            new PullRequestDto(
                Id: 1,
                RepositoryName: "TestRepo",
                Title: "Add feature X",
                CreatedBy: "John Doe",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-7),
                CompletedDate: DateTimeOffset.UtcNow.AddDays(-2),
                Status: "completed",
                IterationPath: "TestProject\\Sprint1",
                SourceBranch: "refs/heads/feature/x",
                TargetBranch: "refs/heads/main",
                RetrievedAt: DateTimeOffset.UtcNow
            ),
            new PullRequestDto(
                Id: 2,
                RepositoryName: "TestRepo",
                Title: "Fix bug Y",
                CreatedBy: "Jane Smith",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-3),
                CompletedDate: null,
                Status: "active",
                IterationPath: "TestProject\\Sprint1",
                SourceBranch: "refs/heads/bugfix/y",
                TargetBranch: "refs/heads/main",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        // Initialize mock iterations
        _mockIterations = new List<PullRequestIterationDto>
        {
            new PullRequestIterationDto(
                PullRequestId: 1,
                IterationNumber: 1,
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-7),
                UpdatedDate: DateTimeOffset.UtcNow.AddDays(-6),
                CommitCount: 3,
                ChangeCount: 5
            ),
            new PullRequestIterationDto(
                PullRequestId: 1,
                IterationNumber: 2,
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-5),
                UpdatedDate: DateTimeOffset.UtcNow.AddDays(-4),
                CommitCount: 2,
                ChangeCount: 3
            )
        };

        // Initialize mock comments
        _mockComments = new List<PullRequestCommentDto>
        {
            new PullRequestCommentDto(
                Id: 1,
                PullRequestId: 1,
                ThreadId: 1,
                Author: "Reviewer One",
                Content: "Please fix the indentation",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-6),
                UpdatedDate: null,
                IsResolved: true,
                ResolvedDate: DateTimeOffset.UtcNow.AddDays(-5),
                ResolvedBy: "John Doe"
            ),
            new PullRequestCommentDto(
                Id: 2,
                PullRequestId: 1,
                ThreadId: 2,
                Author: "Reviewer Two",
                Content: "Looks good!",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedDate: null,
                IsResolved: false,
                ResolvedDate: null,
                ResolvedBy: null
            )
        };

        // Initialize mock file changes
        _mockFileChanges = new List<PullRequestFileChangeDto>
        {
            new PullRequestFileChangeDto(
                PullRequestId: 1,
                IterationId: 1,
                FilePath: "/src/feature.cs",
                ChangeType: "edit",
                LinesAdded: 50,
                LinesDeleted: 10,
                LinesModified: 5
            ),
            new PullRequestFileChangeDto(
                PullRequestId: 1,
                IterationId: 1,
                FilePath: "/tests/feature.test.cs",
                ChangeType: "add",
                LinesAdded: 100,
                LinesDeleted: 0,
                LinesModified: 0
            )
        };
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Always return true for integration tests
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default)
    {
        // Extract distinct area paths from mock work items
        var areaPaths = _mockWorkItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(areaPaths);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        // Filter by date if specified (incremental sync)
        var filtered = since.HasValue
            ? _mockWorkItems.Where(wi => wi.RetrievedAt >= since.Value)
            : _mockWorkItems;

        return Task.FromResult<IEnumerable<WorkItemDto>>(filtered);
    }

    public Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        // Find work item by TFS ID
        var workItem = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == workItemId);
        return Task.FromResult(workItem);
    }

    /// <summary>
    /// Adds a mock work item for testing purposes.
    /// </summary>
    public void AddMockWorkItem(WorkItemDto workItem)
    {
        _mockWorkItems.Add(workItem);
    }

    /// <summary>
    /// Clears all mock work items.
    /// </summary>
    public void ClearMockWorkItems()
    {
        _mockWorkItems.Clear();
    }

    // Pull Request methods - return mock data for integration tests
    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = _mockPullRequests.AsEnumerable();

        if (!string.IsNullOrEmpty(repositoryName))
        {
            filtered = filtered.Where(pr => pr.RepositoryName == repositoryName);
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= toDate.Value);
        }

        return Task.FromResult(filtered);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var iterations = _mockIterations.Where(i => i.PullRequestId == pullRequestId);
        return Task.FromResult(iterations);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var comments = _mockComments.Where(c => c.PullRequestId == pullRequestId);
        return Task.FromResult(comments);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        var changes = _mockFileChanges.Where(fc => fc.PullRequestId == pullRequestId && fc.IterationId == iterationId);
        return Task.FromResult(changes);
    }

    public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        // Return mock revision history for testing
        var mockRevisions = new List<WorkItemRevisionDto>
        {
            new WorkItemRevisionDto(
                RevisionNumber: 1,
                WorkItemId: workItemId,
                ChangedBy: "Test User",
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
                ChangedBy: "Test User 2",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-5),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["System.State"] = new WorkItemFieldChange("System.State", "New", "Active"),
                    ["System.AssignedTo"] = new WorkItemFieldChange("System.AssignedTo", null, "Test User 2")
                },
                Comment: "Started work on this item"
            ),
            new WorkItemRevisionDto(
                RevisionNumber: 3,
                WorkItemId: workItemId,
                ChangedBy: "Test User 2",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-2),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["Microsoft.VSTS.Scheduling.Effort"] = new WorkItemFieldChange("Microsoft.VSTS.Scheduling.Effort", null, "5")
                },
                Comment: "Added effort estimate"
            )
        };

        return Task.FromResult<IEnumerable<WorkItemRevisionDto>>(mockRevisions);
    }

    /// <summary>
    /// Adds a mock pull request for testing purposes.
    /// </summary>
    public void AddMockPullRequest(PullRequestDto pullRequest)
    {
        _mockPullRequests.Add(pullRequest);
    }

    /// <summary>
    /// Adds a mock iteration for testing purposes.
    /// </summary>
    public void AddMockIteration(PullRequestIterationDto iteration)
    {
        _mockIterations.Add(iteration);
    }

    /// <summary>
    /// Adds a mock comment for testing purposes.
    /// </summary>
    public void AddMockComment(PullRequestCommentDto comment)
    {
        _mockComments.Add(comment);
    }

    /// <summary>
    /// Adds a mock file change for testing purposes.
    /// </summary>
    public void AddMockFileChange(PullRequestFileChangeDto fileChange)
    {
        _mockFileChanges.Add(fileChange);
    }

    public Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
    {
        // Mock implementation for integration tests
        // Always return true for valid state values to simulate successful TFS update
        var validStates = new[] { "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed" };
        var isValidState = validStates.Contains(newState, StringComparer.OrdinalIgnoreCase);

        if (!isValidState)
        {
            return Task.FromResult(false);
        }

        // Find and update the work item if it exists in our mock list
        var workItem = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == workItemId);
        if (workItem != null)
        {
            // Remove old and add updated work item (since WorkItemDto is immutable)
            _mockWorkItems.Remove(workItem);
            var updatedWorkItem = workItem with { State = newState };
            _mockWorkItems.Add(updatedWorkItem);
        }

        // Return true regardless of whether work item is in our mock list
        // The handler will check the database repository, not our internal list
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        // Mock implementation for integration tests
        // Always return true for valid effort values to simulate successful TFS update
        if (effort < 0)
        {
            return Task.FromResult(false);
        }

        // Find and update the work item if it exists in our mock list
        var workItem = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == workItemId);
        if (workItem != null)
        {
            // Remove old and add updated work item (since WorkItemDto is immutable)
            _mockWorkItems.Remove(workItem);
            var updatedWorkItem = workItem with { Effort = effort };
            _mockWorkItems.Add(updatedWorkItem);
        }

        // Return true regardless of whether work item is in our mock list
        // The handler will check the database repository, not our internal list
        return Task.FromResult(true);
    }

    public Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        // Mock implementation for integration tests - always return success
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
                ObservedBehavior = "Mock project accessible"
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
                ObservedBehavior = "Mock Git repositories API accessible"
            }
        };

        if (includeWriteChecks && workItemIdForWriteCheck.HasValue)
        {
            checks.Add(new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-update",
                Success = true,
                ImpactedFunctionality = "Work item modifications",
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
            ServerUrl = "https://mock-tfs-test.example.com",
            ProjectName = "TestProject",
            ApiVersion = "7.0",
            IncludedWriteChecks = includeWriteChecks,
            Success = true,
            Checks = checks
        };

        return Task.FromResult(report);
    }

    // ============================================
    // BULK METHODS - Prevent N+1 query patterns
    // ============================================

    public Task<PullRequestSyncResult> GetPullRequestsWithDetailsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = _mockPullRequests.AsEnumerable();

        if (!string.IsNullOrEmpty(repositoryName))
        {
            filtered = filtered.Where(pr => pr.RepositoryName == repositoryName);
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

        var iterations = _mockIterations.Where(i => prIds.Contains(i.PullRequestId)).ToList();
        var comments = _mockComments.Where(c => prIds.Contains(c.PullRequestId)).ToList();
        var fileChanges = _mockFileChanges.Where(fc => prIds.Contains(fc.PullRequestId)).ToList();

        return Task.FromResult(new PullRequestSyncResult(
            PullRequests: prs,
            Iterations: iterations,
            Comments: comments,
            FileChanges: fileChanges,
            TfsCallCount: 1
        ));
    }

    public Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var update in updatesList)
        {
            if (update.EffortValue < 0)
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, false, "Invalid effort value"));
                failedCount++;
            }
            else
            {
                var workItem = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == update.WorkItemId);
                if (workItem != null)
                {
                    _mockWorkItems.Remove(workItem);
                    _mockWorkItems.Add(workItem with { Effort = update.EffortValue });
                }
                results.Add(new BulkUpdateItemResult(update.WorkItemId, true));
                successCount++;
            }
        }

        return Task.FromResult(new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: 1
        ));
    }

    public Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        var validStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed"
        };

        foreach (var update in updatesList)
        {
            if (!validStates.Contains(update.NewState))
            {
                results.Add(new BulkUpdateItemResult(update.WorkItemId, false, "Invalid state"));
                failedCount++;
            }
            else
            {
                var workItem = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == update.WorkItemId);
                if (workItem != null)
                {
                    _mockWorkItems.Remove(workItem);
                    _mockWorkItems.Add(workItem with { State = update.NewState });
                }
                results.Add(new BulkUpdateItemResult(update.WorkItemId, true));
                successCount++;
            }
        }

        return Task.FromResult(new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: 1
        ));
    }

    public Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, IEnumerable<WorkItemRevisionDto>>();

        foreach (var workItemId in workItemIds)
        {
            var revisions = new List<WorkItemRevisionDto>
            {
                new WorkItemRevisionDto(
                    RevisionNumber: 1,
                    WorkItemId: workItemId,
                    ChangedBy: "Test User",
                    ChangedDate: DateTimeOffset.UtcNow.AddDays(-10),
                    FieldChanges: new Dictionary<string, WorkItemFieldChange>
                    {
                        ["System.State"] = new WorkItemFieldChange("System.State", null, "New")
                    },
                    Comment: "Created"
                )
            };
            results[workItemId] = revisions;
        }

        return Task.FromResult<IDictionary<int, IEnumerable<WorkItemRevisionDto>>>(results);
    }

    public Task<WorkItemCreateResult> CreateWorkItemAsync(
        WorkItemCreateRequest request,
        CancellationToken cancellationToken = default)
    {
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
        return Task.FromResult(true);
    }

    // ============================================
    // PIPELINE METHODS
    // ============================================

    public Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        var pipelines = new List<PipelineDto>
        {
            new PipelineDto(1, "TestBuild.CI", PipelineType.Build, "\\Test", DateTimeOffset.UtcNow),
            new PipelineDto(2, "TestRelease.Deploy", PipelineType.Release, "\\Test", DateTimeOffset.UtcNow)
        };
        return Task.FromResult<IEnumerable<PipelineDto>>(pipelines);
    }

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        var runs = new List<PipelineRunDto>
        {
            new PipelineRunDto(
                RunId: 1000,
                PipelineId: pipelineId,
                PipelineName: "TestBuild.CI",
                StartTime: DateTimeOffset.UtcNow.AddHours(-2),
                FinishTime: DateTimeOffset.UtcNow.AddHours(-1),
                Duration: TimeSpan.FromHours(1),
                Result: PipelineRunResult.Succeeded,
                Trigger: PipelineRunTrigger.ContinuousIntegration,
                TriggerInfo: "Triggered by push",
                Branch: "main",
                RequestedFor: "test.user@test.com",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };
        return Task.FromResult<IEnumerable<PipelineRunDto>>(runs);
    }

    public Task<PipelineSyncResult> GetPipelinesWithRunsAsync(
        int runsPerPipeline = 50,
        CancellationToken cancellationToken = default)
    {
        var pipelines = new List<PipelineDto>
        {
            new PipelineDto(1, "TestBuild.CI", PipelineType.Build, "\\Test", DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new PipelineRunDto(
                RunId: 1000,
                PipelineId: 1,
                PipelineName: "TestBuild.CI",
                StartTime: DateTimeOffset.UtcNow.AddHours(-2),
                FinishTime: DateTimeOffset.UtcNow.AddHours(-1),
                Duration: TimeSpan.FromHours(1),
                Result: PipelineRunResult.Succeeded,
                Trigger: PipelineRunTrigger.ContinuousIntegration,
                TriggerInfo: "Triggered by push",
                Branch: "main",
                RequestedFor: "test.user@test.com",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        return Task.FromResult(new PipelineSyncResult(
            Pipelines: pipelines,
            Runs: runs,
            TfsCallCount: 1,
            SyncedAt: DateTimeOffset.UtcNow
        ));
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Build hierarchy map to find descendants
        var results = new List<WorkItemDto>();
        var processedIds = new HashSet<int>();

        void CollectHierarchy(int parentId)
        {
            if (processedIds.Contains(parentId)) return;

            var item = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == parentId);
            if (item != null)
            {
                processedIds.Add(parentId);
                results.Add(item);

                // Find children
                var children = _mockWorkItems.Where(wi => wi.ParentTfsId == parentId);
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

        return Task.FromResult<IEnumerable<WorkItemDto>>(results);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<SyncProgressDto>? detailedProgressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Build hierarchy map to find descendants
        var results = new List<WorkItemDto>();
        var processedIds = new HashSet<int>();

        void CollectHierarchy(int parentId)
        {
            if (processedIds.Contains(parentId)) return;

            var item = _mockWorkItems.FirstOrDefault(wi => wi.TfsId == parentId);
            if (item != null)
            {
                processedIds.Add(parentId);
                results.Add(item);

                // Find children
                var children = _mockWorkItems.Where(wi => wi.ParentTfsId == parentId);
                foreach (var child in children)
                {
                    CollectHierarchy(child.TfsId);
                }
            }
        }

        // Report detailed progress
        detailedProgressCallback?.Invoke(new SyncProgressDto
        {
            Status = "InProgress",
            Message = "Finding root work items...",
            Phase = "Discovery",
            BatchIndex = 1,
            TotalBatches = 1
        });

        foreach (var rootId in rootWorkItemIds)
        {
            CollectHierarchy(rootId);
        }

        detailedProgressCallback?.Invoke(new SyncProgressDto
        {
            Status = "InProgress",
            Message = $"Processing {results.Count} work items...",
            Phase = "Processing",
            BatchIndex = 1,
            TotalBatches = 1,
            IdCount = results.Count
        });

        // Filter by date if specified
        if (since.HasValue)
        {
            results = results.Where(wi => wi.RetrievedAt >= since.Value).ToList();
        }

        detailedProgressCallback?.Invoke(new SyncProgressDto
        {
            Status = "InProgress",
            Message = "Complete",
            Phase = "Complete",
            BatchIndex = 1,
            TotalBatches = 1,
            ProcessedCount = results.Count,
            TotalCount = results.Count
        });

        return Task.FromResult<IEnumerable<WorkItemDto>>(results);
    }

    public Task<string?> GetRepositoryIdByNameAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        // Return a deterministic mock GUID based on repository name
        var mockGuid = $"mock-repo-{repositoryName.ToLowerInvariant()}-guid";
        return Task.FromResult<string?>(mockGuid);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        // Return empty list for tests
        return Task.FromResult<IEnumerable<PipelineDefinitionDto>>(Array.Empty<PipelineDefinitionDto>());
    }

    public Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(
        string projectName,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        // Return mock team iterations for testing
        var now = DateTimeOffset.UtcNow;
        var iterations = new List<TeamIterationDto>
        {
            new TeamIterationDto(
                "test-iteration-1",
                "Sprint 1",
                $"\\{projectName}\\Sprint 1",
                now.AddDays(-14),
                now.AddDays(-7),
                "past"
            ),
            new TeamIterationDto(
                "test-iteration-2",
                "Sprint 2",
                $"\\{projectName}\\Sprint 2",
                now.AddDays(-7),
                now.AddDays(7),
                "current"
            ),
            new TeamIterationDto(
                "test-iteration-3",
                "Sprint 3",
                $"\\{projectName}\\Sprint 3",
                now.AddDays(7),
                now.AddDays(21),
                "future"
            )
        };

        return Task.FromResult<IEnumerable<TeamIterationDto>>(iterations);
    }
}
