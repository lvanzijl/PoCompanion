using PoTool.Core.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Provides mock pull request data for testing and development.
/// </summary>
public class MockPullRequestDataProvider
{
    /// <summary>
    /// Generates a complete set of mock pull requests with various scenarios.
    /// </summary>
    public List<PullRequestDto> GetMockPullRequests()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<PullRequestDto>();

        // Scenario 1: Quick PR - completed in 1 day, minimal iterations
        items.Add(new PullRequestDto(
            Id: 101,
            RepositoryName: "PoCompanion",
            Title: "Fix typo in documentation",
            CreatedBy: "Alice Johnson",
            CreatedDate: now.AddDays(-10),
            CompletedDate: now.AddDays(-9),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            SourceBranch: "refs/heads/fix/typo-docs",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 2: Medium PR - 3 days, couple of iterations
        items.Add(new PullRequestDto(
            Id: 102,
            RepositoryName: "PoCompanion",
            Title: "Add validation for user input",
            CreatedBy: "Bob Smith",
            CreatedDate: now.AddDays(-7),
            CompletedDate: now.AddDays(-4),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            SourceBranch: "refs/heads/feature/input-validation",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 3: Long-running PR - 14 days, many iterations
        items.Add(new PullRequestDto(
            Id: 103,
            RepositoryName: "PoCompanion",
            Title: "Implement new authentication system",
            CreatedBy: "Charlie Davis",
            CreatedDate: now.AddDays(-20),
            CompletedDate: now.AddDays(-6),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/feature/new-auth",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 4: Large PR - many files changed
        items.Add(new PullRequestDto(
            Id: 104,
            RepositoryName: "PoCompanion",
            Title: "Refactor data access layer",
            CreatedBy: "Diana Evans",
            CreatedDate: now.AddDays(-12),
            CompletedDate: now.AddDays(-3),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/refactor/data-layer",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 5: Active PR - currently open
        items.Add(new PullRequestDto(
            Id: 105,
            RepositoryName: "PoCompanion",
            Title: "Add dark mode support",
            CreatedBy: "Eve Foster",
            CreatedDate: now.AddDays(-5),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/feature/dark-mode",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 6: Active PR with many comments
        items.Add(new PullRequestDto(
            Id: 106,
            RepositoryName: "PoCompanion",
            Title: "Update API documentation",
            CreatedBy: "Frank Green",
            CreatedDate: now.AddDays(-8),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/docs/api-update",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 7: Different iteration path
        items.Add(new PullRequestDto(
            Id: 107,
            RepositoryName: "PoCompanion",
            Title: "Performance optimization",
            CreatedBy: "Grace Hill",
            CreatedDate: now.AddDays(-15),
            CompletedDate: now.AddDays(-2),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            SourceBranch: "refs/heads/perf/optimization",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Scenario 8: Abandoned PR
        items.Add(new PullRequestDto(
            Id: 108,
            RepositoryName: "PoCompanion",
            Title: "Experimental feature test",
            CreatedBy: "Henry Irving",
            CreatedDate: now.AddDays(-30),
            CompletedDate: now.AddDays(-25),
            Status: "abandoned",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            SourceBranch: "refs/heads/experiment/feature-test",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        return items;
    }

    /// <summary>
    /// Generates mock iterations for pull requests.
    /// </summary>
    public List<PullRequestIterationDto> GetMockIterations()
    {
        var now = DateTimeOffset.UtcNow;
        var iterations = new List<PullRequestIterationDto>();

        // PR 101: Quick PR - 1 iteration
        iterations.Add(new PullRequestIterationDto(101, 1, now.AddDays(-10), now.AddDays(-10), 1, 2));

        // PR 102: Medium PR - 2 iterations
        iterations.Add(new PullRequestIterationDto(102, 1, now.AddDays(-7), now.AddDays(-7), 3, 8));
        iterations.Add(new PullRequestIterationDto(102, 2, now.AddDays(-5), now.AddDays(-5), 2, 3));

        // PR 103: Long-running PR - 5 iterations (lots of rework)
        iterations.Add(new PullRequestIterationDto(103, 1, now.AddDays(-20), now.AddDays(-20), 5, 25));
        iterations.Add(new PullRequestIterationDto(103, 2, now.AddDays(-17), now.AddDays(-17), 3, 12));
        iterations.Add(new PullRequestIterationDto(103, 3, now.AddDays(-14), now.AddDays(-14), 4, 15));
        iterations.Add(new PullRequestIterationDto(103, 4, now.AddDays(-10), now.AddDays(-10), 2, 8));
        iterations.Add(new PullRequestIterationDto(103, 5, now.AddDays(-8), now.AddDays(-8), 1, 3));

        // PR 104: Large PR - 3 iterations
        iterations.Add(new PullRequestIterationDto(104, 1, now.AddDays(-12), now.AddDays(-12), 8, 45));
        iterations.Add(new PullRequestIterationDto(104, 2, now.AddDays(-8), now.AddDays(-8), 4, 18));
        iterations.Add(new PullRequestIterationDto(104, 3, now.AddDays(-5), now.AddDays(-5), 2, 7));

        // PR 105: Active PR - 2 iterations so far
        iterations.Add(new PullRequestIterationDto(105, 1, now.AddDays(-5), now.AddDays(-5), 4, 12));
        iterations.Add(new PullRequestIterationDto(105, 2, now.AddDays(-2), now.AddDays(-2), 2, 5));

        // PR 106: Active PR with many comments - 4 iterations
        iterations.Add(new PullRequestIterationDto(106, 1, now.AddDays(-8), now.AddDays(-8), 3, 10));
        iterations.Add(new PullRequestIterationDto(106, 2, now.AddDays(-6), now.AddDays(-6), 2, 7));
        iterations.Add(new PullRequestIterationDto(106, 3, now.AddDays(-4), now.AddDays(-4), 3, 9));
        iterations.Add(new PullRequestIterationDto(106, 4, now.AddDays(-1), now.AddDays(-1), 1, 4));

        // PR 107: Performance optimization - 2 iterations
        iterations.Add(new PullRequestIterationDto(107, 1, now.AddDays(-15), now.AddDays(-15), 6, 20));
        iterations.Add(new PullRequestIterationDto(107, 2, now.AddDays(-10), now.AddDays(-10), 3, 8));

        // PR 108: Abandoned PR - 1 iteration
        iterations.Add(new PullRequestIterationDto(108, 1, now.AddDays(-30), now.AddDays(-30), 2, 5));

        return iterations;
    }

    /// <summary>
    /// Generates mock comments for pull requests.
    /// </summary>
    public List<PullRequestCommentDto> GetMockComments()
    {
        var now = DateTimeOffset.UtcNow;
        var comments = new List<PullRequestCommentDto>();

        // PR 101: Quick PR - minimal comments, all resolved
        comments.Add(new PullRequestCommentDto(1, 101, 1, "Alice Johnson", "LGTM", now.AddDays(-9).AddHours(-2), null, true, now.AddDays(-9).AddHours(-1), "Bob Smith"));

        // PR 102: Medium PR - some comments with resolution
        comments.Add(new PullRequestCommentDto(2, 102, 1, "Charlie Davis", "Please add null checks", now.AddDays(-6), now.AddDays(-5), true, now.AddDays(-5), "Bob Smith"));
        comments.Add(new PullRequestCommentDto(3, 102, 2, "Diana Evans", "Consider using string interpolation", now.AddDays(-5), null, true, now.AddDays(-4).AddHours(-3), "Bob Smith"));

        // PR 103: Long-running PR - many comments showing rework
        comments.Add(new PullRequestCommentDto(4, 103, 1, "Alice Johnson", "Architecture concerns - needs refactoring", now.AddDays(-19), now.AddDays(-17), true, now.AddDays(-17), "Charlie Davis"));
        comments.Add(new PullRequestCommentDto(5, 103, 2, "Bob Smith", "Security issue with token handling", now.AddDays(-18), now.AddDays(-14), true, now.AddDays(-14), "Charlie Davis"));
        comments.Add(new PullRequestCommentDto(6, 103, 3, "Diana Evans", "Missing unit tests", now.AddDays(-16), now.AddDays(-10), true, now.AddDays(-10), "Charlie Davis"));
        comments.Add(new PullRequestCommentDto(7, 103, 4, "Eve Foster", "Performance concerns", now.AddDays(-12), now.AddDays(-8), true, now.AddDays(-8), "Charlie Davis"));
        comments.Add(new PullRequestCommentDto(8, 103, 5, "Frank Green", "Documentation needed", now.AddDays(-9), now.AddDays(-7), true, now.AddDays(-7), "Charlie Davis"));

        // PR 104: Large PR - moderate comments
        comments.Add(new PullRequestCommentDto(9, 104, 1, "Grace Hill", "Consider breaking into smaller PRs", now.AddDays(-11), null, true, now.AddDays(-8), "Diana Evans"));
        comments.Add(new PullRequestCommentDto(10, 104, 2, "Henry Irving", "Naming conventions", now.AddDays(-9), now.AddDays(-5), true, now.AddDays(-5), "Diana Evans"));

        // PR 105: Active PR - some resolved, some unresolved
        comments.Add(new PullRequestCommentDto(11, 105, 1, "Alice Johnson", "Dark mode colors need adjustment", now.AddDays(-4), now.AddDays(-2), true, now.AddDays(-2), "Eve Foster"));
        comments.Add(new PullRequestCommentDto(12, 105, 2, "Bob Smith", "Accessibility concerns", now.AddDays(-3), null, false, null, null));
        comments.Add(new PullRequestCommentDto(13, 105, 3, "Charlie Davis", "Missing contrast ratios", now.AddDays(-2), null, false, null, null));

        // PR 106: Active PR with many comments - high activity
        comments.Add(new PullRequestCommentDto(14, 106, 1, "Diana Evans", "Incomplete documentation", now.AddDays(-7), now.AddDays(-6), true, now.AddDays(-6), "Frank Green"));
        comments.Add(new PullRequestCommentDto(15, 106, 2, "Eve Foster", "Missing examples", now.AddDays(-6), now.AddDays(-4), true, now.AddDays(-4), "Frank Green"));
        comments.Add(new PullRequestCommentDto(16, 106, 3, "Grace Hill", "Incorrect API endpoint", now.AddDays(-5), now.AddDays(-4), true, now.AddDays(-4), "Frank Green"));
        comments.Add(new PullRequestCommentDto(17, 106, 4, "Henry Irving", "Add authentication examples", now.AddDays(-4), now.AddDays(-1), true, now.AddDays(-1), "Frank Green"));
        comments.Add(new PullRequestCommentDto(18, 106, 5, "Alice Johnson", "Format consistency", now.AddDays(-3), null, false, null, null));
        comments.Add(new PullRequestCommentDto(19, 106, 6, "Bob Smith", "Typo in parameter name", now.AddDays(-2), null, false, null, null));

        // PR 107: Performance optimization - technical comments
        comments.Add(new PullRequestCommentDto(20, 107, 1, "Charlie Davis", "Good improvements, consider caching", now.AddDays(-13), now.AddDays(-10), true, now.AddDays(-10), "Grace Hill"));
        comments.Add(new PullRequestCommentDto(21, 107, 2, "Diana Evans", "Memory usage concerns", now.AddDays(-11), now.AddDays(-10), true, now.AddDays(-10), "Grace Hill"));

        return comments;
    }

    /// <summary>
    /// Generates mock file changes for pull requests.
    /// </summary>
    public List<PullRequestFileChangeDto> GetMockFileChanges()
    {
        var fileChanges = new List<PullRequestFileChangeDto>();

        // PR 101: Quick PR - small change
        fileChanges.Add(new PullRequestFileChangeDto(101, 1, "README.md", "edit", 2, 2, 0));

        // PR 102: Medium PR - several files
        fileChanges.Add(new PullRequestFileChangeDto(102, 1, "src/Validation/InputValidator.cs", "add", 85, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(102, 1, "src/Validation/ValidationRules.cs", "add", 120, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(102, 1, "src/Controllers/UserController.cs", "edit", 15, 5, 0));
        fileChanges.Add(new PullRequestFileChangeDto(102, 2, "src/Validation/InputValidator.cs", "edit", 12, 8, 0));

        // PR 103: Long-running PR - many files with rework
        fileChanges.Add(new PullRequestFileChangeDto(103, 1, "src/Auth/AuthenticationService.cs", "add", 250, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 1, "src/Auth/TokenManager.cs", "add", 180, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 1, "src/Auth/ClaimsProvider.cs", "add", 95, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 2, "src/Auth/TokenManager.cs", "edit", 45, 30, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 3, "src/Auth/AuthenticationService.cs", "edit", 60, 25, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 3, "tests/Auth/AuthTests.cs", "add", 150, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 4, "src/Auth/TokenManager.cs", "edit", 20, 15, 0));
        fileChanges.Add(new PullRequestFileChangeDto(103, 5, "docs/Authentication.md", "add", 85, 0, 0));

        // PR 104: Large PR - many files
        for (int i = 1; i <= 25; i++)
        {
            fileChanges.Add(new PullRequestFileChangeDto(104, 1, $"src/Data/Repository{i}.cs", "edit", 45, 30, 0));
        }
        fileChanges.Add(new PullRequestFileChangeDto(104, 2, "src/Data/RepositoryBase.cs", "edit", 75, 45, 0));
        fileChanges.Add(new PullRequestFileChangeDto(104, 3, "src/Data/DataContext.cs", "edit", 30, 20, 0));

        // PR 105: Active PR - moderate changes
        fileChanges.Add(new PullRequestFileChangeDto(105, 1, "src/Themes/DarkTheme.cs", "add", 120, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(105, 1, "src/Themes/ThemeManager.cs", "edit", 55, 10, 0));
        fileChanges.Add(new PullRequestFileChangeDto(105, 1, "src/UI/MainWindow.cs", "edit", 25, 5, 0));
        fileChanges.Add(new PullRequestFileChangeDto(105, 2, "src/Themes/DarkTheme.cs", "edit", 35, 15, 0));

        // PR 106: Documentation update - mostly additions
        fileChanges.Add(new PullRequestFileChangeDto(106, 1, "docs/API/Authentication.md", "add", 200, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(106, 1, "docs/API/Endpoints.md", "edit", 150, 30, 0));
        fileChanges.Add(new PullRequestFileChangeDto(106, 2, "docs/API/Examples.md", "add", 180, 0, 0));
        fileChanges.Add(new PullRequestFileChangeDto(106, 3, "docs/API/Authentication.md", "edit", 45, 20, 0));
        fileChanges.Add(new PullRequestFileChangeDto(106, 4, "docs/API/Endpoints.md", "edit", 30, 15, 0));

        // PR 107: Performance optimization - focused changes
        fileChanges.Add(new PullRequestFileChangeDto(107, 1, "src/Services/CacheService.cs", "edit", 95, 45, 0));
        fileChanges.Add(new PullRequestFileChangeDto(107, 1, "src/Services/DataService.cs", "edit", 110, 60, 0));
        fileChanges.Add(new PullRequestFileChangeDto(107, 2, "src/Services/CacheService.cs", "edit", 40, 20, 0));

        // PR 108: Abandoned PR - minimal
        fileChanges.Add(new PullRequestFileChangeDto(108, 1, "src/Experimental/Feature.cs", "add", 75, 0, 0));

        return fileChanges;
    }
}
