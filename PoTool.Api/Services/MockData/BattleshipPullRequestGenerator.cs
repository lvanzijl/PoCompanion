using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates pull requests with full metadata following Battleship theme.
/// Creates minimum 100-200 PRs with reviews, comments, and work item links.
/// </summary>
public class BattleshipPullRequestGenerator
{
    private readonly Random _random;
    private const int Seed = 44;

    public BattleshipPullRequestGenerator()
    {
        _random = new Random(Seed);
    }

    /// <summary>
    /// Generates pull requests with full metadata
    /// </summary>
    public List<PullRequestDto> GeneratePullRequests(int workItemCount)
    {
        var pullRequests = new List<PullRequestDto>();
        var now = DateTimeOffset.UtcNow;
        
        // Generate 100-200 PRs (target ~150)
        var prCount = 150;
        
        var developers = GetDevelopers();
        var repositories = new[] { "Battleship-Incident-Backend", "Battleship-Web-UI", "Battleship-Mobile-App", "Battleship-Sensor-Integration" };
        var quarters = new[] { "Q1", "Q2", "Q3", "Q4" };

        for (var i = 0; i < prCount; i++)
        {
            var prId = 1001 + i;
            var repository = repositories[_random.Next(repositories.Length)];
            var developer = developers[_random.Next(developers.Length)];
            var quarter = quarters[i / 40]; // Distribute across quarters
            var sprint = _random.Next(1, 101); // Sprints 1-100
            
            var createdDate = now.AddDays(-_random.Next(1, 365));
            var status = GetPrStatus();
            DateTimeOffset? completedDate = status == "completed" ? createdDate.AddDays(_random.Next(1, 15)) : null;

            var title = GetPrTitle(i, repository);
            var sourceBranch = GetBranchName(title);
            var targetBranch = _random.NextDouble() < 0.9 ? "main" : "develop";

            pullRequests.Add(new PullRequestDto(
                Id: prId,
                RepositoryName: repository,
                Title: title,
                CreatedBy: developer,
                CreatedDate: createdDate,
                CompletedDate: completedDate,
                Status: status,
                IterationPath: $"\\Battleship Systems\\2025\\{quarter}\\Sprint {sprint}",
                SourceBranch: $"refs/heads/{sourceBranch}",
                TargetBranch: $"refs/heads/{targetBranch}",
                RetrievedAt: now
            ));
        }

        return pullRequests;
    }

    /// <summary>
    /// Generates PR iterations (1-5 per PR)
    /// </summary>
    public List<PullRequestIterationDto> GenerateIterations(List<PullRequestDto> pullRequests)
    {
        var iterations = new List<PullRequestIterationDto>();
        var now = DateTimeOffset.UtcNow;

        foreach (var pr in pullRequests)
        {
            var iterationCount = _random.Next(1, 6); // 1-5 iterations
            var currentDate = pr.CreatedDate;

            for (var i = 1; i <= iterationCount; i++)
            {
                var commitCount = _random.Next(1, 51);
                var changeCount = _random.Next(10, 700);

                iterations.Add(new PullRequestIterationDto(
                    PullRequestId: pr.Id,
                    IterationNumber: i,
                    CreatedDate: currentDate,
                    UpdatedDate: currentDate.AddHours(_random.Next(1, 48)),
                    CommitCount: commitCount,
                    ChangeCount: changeCount
                ));

                currentDate = currentDate.AddDays(_random.Next(1, 5));
            }
        }

        return iterations;
    }

    /// <summary>
    /// Generates PR comments (0-20 per PR)
    /// </summary>
    public List<PullRequestCommentDto> GenerateComments(List<PullRequestDto> pullRequests)
    {
        var comments = new List<PullRequestCommentDto>();
        var commentId = 1;
        var now = DateTimeOffset.UtcNow;

        var reviewers = GetDevelopers();
        var commentTexts = GetCommentTexts();

        foreach (var pr in pullRequests)
        {
            // Determine comment count based on distribution
            var rand = _random.NextDouble();
            int commentCount;
            if (rand < 0.20) commentCount = 0;
            else if (rand < 0.70) commentCount = _random.Next(1, 6);
            else if (rand < 0.90) commentCount = _random.Next(6, 11);
            else commentCount = _random.Next(11, 21);

            for (var c = 0; c < commentCount; c++)
            {
                var threadId = c + 1;
                var reviewer = reviewers[_random.Next(reviewers.Length)];
                var commentText = commentTexts[_random.Next(commentTexts.Length)];
                var createdCommentDate = pr.CreatedDate.AddDays(_random.Next(0, 10));
                var isResolved = pr.Status == "completed" && _random.NextDouble() < 0.8;
                var resolvedDate = isResolved ? createdCommentDate.AddDays(_random.Next(1, 5)) : (DateTimeOffset?)null;
                var resolvedBy = isResolved ? pr.CreatedBy : null;

                comments.Add(new PullRequestCommentDto(
                    Id: commentId++,
                    PullRequestId: pr.Id,
                    ThreadId: threadId,
                    Author: reviewer,
                    Content: commentText,
                    CreatedDate: createdCommentDate,
                    UpdatedDate: null,
                    IsResolved: isResolved,
                    ResolvedDate: resolvedDate,
                    ResolvedBy: resolvedBy
                ));
            }
        }

        return comments;
    }

    /// <summary>
    /// Generates PR file changes
    /// </summary>
    public List<PullRequestFileChangeDto> GenerateFileChanges(List<PullRequestDto> pullRequests)
    {
        var fileChanges = new List<PullRequestFileChangeDto>();

        foreach (var pr in pullRequests)
        {
            var iterationCount = _random.Next(1, 6);
            
            for (var iteration = 1; iteration <= iterationCount; iteration++)
            {
                var fileCount = _random.Next(1, 51);
                
                for (var f = 0; f < fileCount; f++)
                {
                    var fileName = GetFileName(pr.RepositoryName, f);
                    var changeType = _random.NextDouble() switch
                    {
                        < 0.50 => "edit",
                        < 0.80 => "add",
                        _ => "delete"
                    };
                    
                    var linesAdded = changeType == "delete" ? 0 : _random.Next(5, 200);
                    var linesDeleted = changeType == "add" ? 0 : _random.Next(0, 150);
                    var linesModified = changeType == "edit" ? _random.Next(5, 100) : 0;

                    fileChanges.Add(new PullRequestFileChangeDto(
                        PullRequestId: pr.Id,
                        IterationId: iteration,
                        FilePath: fileName,
                        ChangeType: changeType,
                        LinesAdded: linesAdded,
                        LinesDeleted: linesDeleted,
                        LinesModified: linesModified
                    ));
                }
            }
        }

        return fileChanges;
    }

    /// <summary>
    /// Generates PR-to-WorkItem links (70-80% of PRs linked to work items)
    /// </summary>
    public List<PrWorkItemLink> GeneratePrWorkItemLinks(List<PullRequestDto> pullRequests, int workItemCount)
    {
        var links = new List<PrWorkItemLink>();
        var linkId = 1;

        foreach (var pr in pullRequests)
        {
            // 70-80% of PRs should have work item links
            if (_random.NextDouble() < 0.75)
            {
                // Determine number of links (1-4)
                var rand = _random.NextDouble();
                int linkCount;
                if (rand < 0.50) linkCount = 1;
                else if (rand < 0.75) linkCount = 2;
                else if (rand < 0.90) linkCount = 3;
                else linkCount = 4;

                for (var i = 0; i < linkCount; i++)
                {
                    // Generate a realistic work item ID (between 1000 and workItemCount)
                    var workItemId = _random.Next(1000, 1000 + workItemCount);

                    links.Add(new PrWorkItemLink
                    {
                        Id = linkId++,
                        PullRequestId = pr.Id,
                        WorkItemId = workItemId
                    });
                }
            }
        }

        return links;
    }

    /// <summary>
    /// Generates PR reviews (1-5 reviewers per PR)
    /// </summary>
    public List<PrReview> GenerateReviews(List<PullRequestDto> pullRequests)
    {
        var reviews = new List<PrReview>();
        var reviewId = 1;
        var reviewers = GetDevelopers();

        foreach (var pr in pullRequests)
        {
            var reviewerCount = _random.Next(1, 6);
            var selectedReviewers = reviewers.OrderBy(x => _random.Next()).Take(reviewerCount).ToList();

            foreach (var reviewer in selectedReviewers)
            {
                var vote = GetReviewVote();
                var reviewDate = pr.CreatedDate.AddDays(_random.Next(0, 7));

                reviews.Add(new PrReview
                {
                    Id = reviewId++,
                    PullRequestId = pr.Id,
                    Reviewer = reviewer,
                    Vote = vote,
                    ReviewDate = reviewDate,
                    Comments = vote == "Rejected" || vote == "Waiting" ? GetReviewComment() : "LGTM"
                });
            }
        }

        return reviews;
    }

    /// <summary>
    /// Generates PR labels
    /// </summary>
    public List<PrLabel> GenerateLabels(List<PullRequestDto> pullRequests)
    {
        var labels = new List<PrLabel>();
        var labelId = 1;
        var availableLabels = new[]
        {
            "bug-fix", "feature", "refactoring", "documentation", "high-priority",
            "breaking-change", "needs-testing", "security", "performance", "ui-improvement"
        };

        foreach (var pr in pullRequests)
        {
            // Each PR gets 1-3 labels
            var labelCount = _random.Next(1, 4);
            var selectedLabels = availableLabels.OrderBy(x => _random.Next()).Take(labelCount).ToList();

            foreach (var label in selectedLabels)
            {
                labels.Add(new PrLabel
                {
                    Id = labelId++,
                    PullRequestId = pr.Id,
                    LabelName = label
                });
            }
        }

        return labels;
    }

    private string GetPrStatus()
    {
        var statuses = new[] { "active", "completed", "abandoned" };
        var weights = new[] { 0.18, 0.72, 0.10 };

        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return statuses[i];
        }
        return statuses[1];
    }

    private string GetPrTitle(int index, string repository)
    {
        var titles = new[]
        {
            "Add fire detection sensor integration",
            "Implement crew safety tracking dashboard",
            "Fix hull integrity monitoring alerts",
            "Update damage control workflow",
            "Add emergency broadcast system",
            "Implement collision detection algorithm",
            "Fix leakage monitoring false positives",
            "Add medical emergency response module",
            "Update incident reporting interface",
            "Implement real-time compartment status",
            "Add evacuation route optimization",
            "Fix fire suppression activation delay",
            "Update crew location tracking accuracy",
            "Implement emergency protocol automation",
            "Add command center integration",
            "Fix sensor data synchronization",
            "Update damage assessment visualization",
            "Implement predictive maintenance alerts",
            "Add inter-department messaging",
            "Fix incident log corruption issue"
        };
        
        return titles[index % titles.Length];
    }

    private string GetBranchName(string title)
    {
        return "feature/" + title
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("add", "")
            .Replace("fix", "bugfix")
            .Replace("update", "enhancement")
            .Replace("implement", "")
            .Trim('-');
    }

    private string[] GetDevelopers()
    {
        return new[]
        {
            "alice.johnson@battleship.mil",
            "bob.smith@battleship.mil",
            "charlie.davis@battleship.mil",
            "diana.evans@battleship.mil",
            "eve.foster@battleship.mil",
            "frank.green@battleship.mil",
            "grace.hill@battleship.mil",
            "henry.irving@battleship.mil",
            "ivy.jones@battleship.mil",
            "jack.king@battleship.mil"
        };
    }

    private string[] GetCommentTexts()
    {
        return new[]
        {
            "LGTM, excellent implementation",
            "Please add unit tests for sensor validation",
            "Consider extracting alert logic to a separate service",
            "Documentation needed for API endpoints",
            "Performance optimization: cache sensor data",
            "Security concern: validate all user inputs",
            "Code style: use consistent naming conventions",
            "Add error handling for network failures",
            "Consider using async/await for database calls",
            "Excellent work on the damage control module",
            "Please fix merge conflicts",
            "Add integration tests for emergency protocols",
            "Update API documentation",
            "Refactor alert threshold validation",
            "Add logging for debugging purposes",
            "Consider using dependency injection",
            "Optimize database queries",
            "Add null checks for safety",
            "Update configuration settings",
            "Great improvement to crew safety features"
        };
    }

    private string GetReviewVote()
    {
        var votes = new[] { "Approved", "Approved with suggestions", "Waiting", "Rejected" };
        var weights = new[] { 0.65, 0.18, 0.12, 0.05 };

        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return votes[i];
        }
        return votes[0];
    }

    private string GetReviewComment()
    {
        var comments = new[]
        {
            "Please address test failures before merging",
            "Security review required for authentication changes",
            "Performance benchmarks needed",
            "Breaking changes need documentation update",
            "Code coverage below threshold"
        };
        return comments[_random.Next(comments.Length)];
    }

    private string GetFileName(string repository, int index)
    {
        var paths = repository switch
        {
            "Battleship-Incident-Backend" => new[]
            {
                "src/Services/IncidentDetectionService.cs",
                "src/Services/DamageControlService.cs",
                "src/Controllers/CrewSafetyController.cs",
                "src/Models/HullIntegrityData.cs",
                "src/Repositories/IncidentRepository.cs",
                "tests/Services/IncidentDetectionServiceTests.cs"
            },
            "Battleship-Web-UI" => new[]
            {
                "src/components/DamageControlDashboard.tsx",
                "src/components/CrewSafetyPanel.tsx",
                "src/services/IncidentApiService.ts",
                "src/styles/dashboard.css",
                "tests/components/DamageControlDashboard.test.tsx"
            },
            "Battleship-Mobile-App" => new[]
            {
                "src/screens/IncidentAlertScreen.dart",
                "src/screens/CrewLocationScreen.dart",
                "src/services/NotificationService.dart",
                "tests/screens/IncidentAlertScreen_test.dart"
            },
            _ => new[]
            {
                "src/sensors/FireDetectionSensor.cs",
                "src/sensors/LeakageMonitorSensor.cs",
                "src/integration/SensorDataProcessor.cs",
                "tests/sensors/FireDetectionSensorTests.cs"
            }
        };

        return paths[index % paths.Length];
    }
}

/// <summary>
/// Represents a link between a PR and a work item
/// </summary>
public class PrWorkItemLink
{
    public int Id { get; set; }
    public int PullRequestId { get; set; }
    public int WorkItemId { get; set; }
}

/// <summary>
/// Represents a PR review
/// </summary>
public class PrReview
{
    public int Id { get; set; }
    public int PullRequestId { get; set; }
    public string Reviewer { get; set; } = string.Empty;
    public string Vote { get; set; } = string.Empty;
    public DateTimeOffset ReviewDate { get; set; }
    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// Represents a PR label
/// </summary>
public class PrLabel
{
    public int Id { get; set; }
    public int PullRequestId { get; set; }
    public string LabelName { get; set; } = string.Empty;
}
