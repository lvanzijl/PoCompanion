using PoTool.Core.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Provides mock pull request data for testing and development.
/// </summary>
public class MockPullRequestDataProvider
{
    /// <summary>
    /// Generates a complete set of mock pull requests with various scenarios.
    /// Covers all 12 sprints with varying characteristics.
    /// </summary>
    public List<PullRequestDto> GetMockPullRequests()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<PullRequestDto>();

        // Sprint 1 - Initial work, small PRs
        items.Add(new PullRequestDto(
            Id: 101,
            RepositoryName: "PoCompanion",
            Title: "Setup Settings page UI",
            CreatedBy: "Alice Johnson",
            CreatedDate: now.AddDays(-330),
            CompletedDate: now.AddDays(-328),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            SourceBranch: "refs/heads/feature/settings-ui",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 102,
            RepositoryName: "PoCompanion",
            Title: "Implement Goals persistence layer",
            CreatedBy: "Bob Smith",
            CreatedDate: now.AddDays(-327),
            CompletedDate: now.AddDays(-325),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 1",
            SourceBranch: "refs/heads/feature/goals-persistence",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 2 - Building momentum
        items.Add(new PullRequestDto(
            Id: 103,
            RepositoryName: "PoCompanion",
            Title: "Add multi-selection to tree view",
            CreatedBy: "Charlie Davis",
            CreatedDate: now.AddDays(-310),
            CompletedDate: now.AddDays(-307),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/feature/multi-select",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 104,
            RepositoryName: "PoCompanion",
            Title: "Goal filtering implementation",
            CreatedBy: "Diana Evans",
            CreatedDate: now.AddDays(-305),
            CompletedDate: now.AddDays(-303),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 2",
            SourceBranch: "refs/heads/feature/goal-filter",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 3 - Peak productivity
        items.Add(new PullRequestDto(
            Id: 105,
            RepositoryName: "PoCompanion",
            Title: "Collapsible tree nodes with icons",
            CreatedBy: "Eve Foster",
            CreatedDate: now.AddDays(-290),
            CompletedDate: now.AddDays(-286),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            SourceBranch: "refs/heads/feature/collapsible-nodes",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 106,
            RepositoryName: "PoCompanion",
            Title: "Drag and drop reordering",
            CreatedBy: "Frank Green",
            CreatedDate: now.AddDays(-288),
            CompletedDate: now.AddDays(-284),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            SourceBranch: "refs/heads/feature/drag-drop",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 107,
            RepositoryName: "PoCompanion",
            Title: "Keyboard navigation support",
            CreatedBy: "Grace Hill",
            CreatedDate: now.AddDays(-282),
            CompletedDate: now.AddDays(-280),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q1\\Sprint 3",
            SourceBranch: "refs/heads/feature/keyboard-nav",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 4 - Consistent delivery
        items.Add(new PullRequestDto(
            Id: 108,
            RepositoryName: "PoCompanion",
            Title: "IndexedDB cache implementation",
            CreatedBy: "Henry Irving",
            CreatedDate: now.AddDays(-265),
            CompletedDate: now.AddDays(-261),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 4",
            SourceBranch: "refs/heads/feature/indexeddb-cache",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 109,
            RepositoryName: "PoCompanion",
            Title: "Cache invalidation strategy",
            CreatedBy: "Ivy Jones",
            CreatedDate: now.AddDays(-262),
            CompletedDate: now.AddDays(-259),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 4",
            SourceBranch: "refs/heads/feature/cache-invalidation",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 5 - Reduced capacity
        items.Add(new PullRequestDto(
            Id: 110,
            RepositoryName: "PoCompanion",
            Title: "Lazy loading for datasets",
            CreatedBy: "Jack King",
            CreatedDate: now.AddDays(-245),
            CompletedDate: now.AddDays(-242),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 5",
            SourceBranch: "refs/heads/feature/lazy-loading",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 111,
            RepositoryName: "PoCompanion",
            Title: "Virtual scrolling component",
            CreatedBy: "Kate Lee",
            CreatedDate: now.AddDays(-240),
            CompletedDate: now.AddDays(-238),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 5",
            SourceBranch: "refs/heads/feature/virtual-scroll",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 6 - Recovery
        items.Add(new PullRequestDto(
            Id: 112,
            RepositoryName: "PoCompanion",
            Title: "Web worker integration for async processing",
            CreatedBy: "Liam Moore",
            CreatedDate: now.AddDays(-225),
            CompletedDate: now.AddDays(-220),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            SourceBranch: "refs/heads/feature/web-workers",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 113,
            RepositoryName: "PoCompanion",
            Title: "Progress indicators for long operations",
            CreatedBy: "Mia Nelson",
            CreatedDate: now.AddDays(-222),
            CompletedDate: now.AddDays(-219),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            SourceBranch: "refs/heads/feature/progress-indicators",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 114,
            RepositoryName: "PoCompanion",
            Title: "Cancellation token support",
            CreatedBy: "Noah Parker",
            CreatedDate: now.AddDays(-217),
            CompletedDate: now.AddDays(-215),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q2\\Sprint 6",
            SourceBranch: "refs/heads/feature/cancellation-tokens",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 7 - Testing focus
        items.Add(new PullRequestDto(
            Id: 115,
            RepositoryName: "PoCompanion",
            Title: "Repository layer unit tests",
            CreatedBy: "Olivia Quinn",
            CreatedDate: now.AddDays(-200),
            CompletedDate: now.AddDays(-197),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            SourceBranch: "refs/heads/test/repository-tests",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 116,
            RepositoryName: "PoCompanion",
            Title: "Service layer unit tests",
            CreatedBy: "Peter Roberts",
            CreatedDate: now.AddDays(-198),
            CompletedDate: now.AddDays(-194),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            SourceBranch: "refs/heads/test/service-tests",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 117,
            RepositoryName: "PoCompanion",
            Title: "Validation logic tests",
            CreatedBy: "Quinn Scott",
            CreatedDate: now.AddDays(-192),
            CompletedDate: now.AddDays(-190),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 7",
            SourceBranch: "refs/heads/test/validation-tests",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 8 - Stable delivery
        items.Add(new PullRequestDto(
            Id: 118,
            RepositoryName: "PoCompanion",
            Title: "Handler tests with comprehensive mocks",
            CreatedBy: "Rachel Taylor",
            CreatedDate: now.AddDays(-175),
            CompletedDate: now.AddDays(-170),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 8",
            SourceBranch: "refs/heads/test/handler-tests",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 119,
            RepositoryName: "PoCompanion",
            Title: "Edge case test coverage",
            CreatedBy: "Sam Turner",
            CreatedDate: now.AddDays(-172),
            CompletedDate: now.AddDays(-169),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 8",
            SourceBranch: "refs/heads/test/edge-cases",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 9 - High velocity
        items.Add(new PullRequestDto(
            Id: 120,
            RepositoryName: "PoCompanion",
            Title: "API endpoint integration tests",
            CreatedBy: "Tara White",
            CreatedDate: now.AddDays(-155),
            CompletedDate: now.AddDays(-150),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            SourceBranch: "refs/heads/test/api-integration",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 121,
            RepositoryName: "PoCompanion",
            Title: "Database integration tests",
            CreatedBy: "Uma Young",
            CreatedDate: now.AddDays(-152),
            CompletedDate: now.AddDays(-148),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            SourceBranch: "refs/heads/test/db-integration",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 122,
            RepositoryName: "PoCompanion",
            Title: "SignalR hub integration tests",
            CreatedBy: "Victor Zhang",
            CreatedDate: now.AddDays(-147),
            CompletedDate: now.AddDays(-144),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q3\\Sprint 9",
            SourceBranch: "refs/heads/test/signalr-tests",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 10 - Active development
        items.Add(new PullRequestDto(
            Id: 123,
            RepositoryName: "PoCompanion",
            Title: "Serilog integration for structured logging",
            CreatedBy: "Wendy Adams",
            CreatedDate: now.AddDays(-125),
            CompletedDate: now.AddDays(-122),
            Status: "completed",
            IterationPath: "PoCompanion\\2025\\Q4\\Sprint 10",
            SourceBranch: "refs/heads/feature/serilog",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 124,
            RepositoryName: "PoCompanion",
            Title: "Log levels configuration",
            CreatedBy: "Xander Brown",
            CreatedDate: now.AddDays(-120),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q4\\Sprint 10",
            SourceBranch: "refs/heads/feature/log-config",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 11 - Current sprint
        items.Add(new PullRequestDto(
            Id: 125,
            RepositoryName: "PoCompanion",
            Title: "Structured log events",
            CreatedBy: "Yara Clark",
            CreatedDate: now.AddDays(-10),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q4\\Sprint 11",
            SourceBranch: "refs/heads/feature/log-events",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        items.Add(new PullRequestDto(
            Id: 126,
            RepositoryName: "PoCompanion",
            Title: "Log aggregation setup",
            CreatedBy: "Zach Davis",
            CreatedDate: now.AddDays(-8),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q4\\Sprint 11",
            SourceBranch: "refs/heads/feature/log-aggregation",
            TargetBranch: "refs/heads/main",
            RetrievedAt: now
        ));

        // Sprint 12 - Future sprint
        items.Add(new PullRequestDto(
            Id: 127,
            RepositoryName: "PoCompanion",
            Title: "Swagger UI setup",
            CreatedBy: "Amy Evans",
            CreatedDate: now.AddDays(-2),
            CompletedDate: null,
            Status: "active",
            IterationPath: "PoCompanion\\2025\\Q4\\Sprint 12",
            SourceBranch: "refs/heads/feature/swagger-ui",
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

        // Sprint 1 PRs - Small iterations
        iterations.Add(new PullRequestIterationDto(101, 1, now.AddDays(-330), now.AddDays(-329), 2, 4));
        iterations.Add(new PullRequestIterationDto(101, 2, now.AddDays(-329), now.AddDays(-328), 1, 2));

        iterations.Add(new PullRequestIterationDto(102, 1, now.AddDays(-327), now.AddDays(-326), 3, 8));
        iterations.Add(new PullRequestIterationDto(102, 2, now.AddDays(-326), now.AddDays(-325), 2, 4));

        // Sprint 2 PRs - Medium complexity
        iterations.Add(new PullRequestIterationDto(103, 1, now.AddDays(-310), now.AddDays(-309), 4, 12));
        iterations.Add(new PullRequestIterationDto(103, 2, now.AddDays(-308), now.AddDays(-307), 2, 5));

        iterations.Add(new PullRequestIterationDto(104, 1, now.AddDays(-305), now.AddDays(-304), 3, 8));
        iterations.Add(new PullRequestIterationDto(104, 2, now.AddDays(-304), now.AddDays(-303), 1, 3));

        // Sprint 3 PRs - Higher complexity
        iterations.Add(new PullRequestIterationDto(105, 1, now.AddDays(-290), now.AddDays(-288), 5, 15));
        iterations.Add(new PullRequestIterationDto(105, 2, now.AddDays(-287), now.AddDays(-286), 2, 6));

        iterations.Add(new PullRequestIterationDto(106, 1, now.AddDays(-288), now.AddDays(-286), 4, 12));
        iterations.Add(new PullRequestIterationDto(106, 2, now.AddDays(-285), now.AddDays(-284), 3, 8));

        iterations.Add(new PullRequestIterationDto(107, 1, now.AddDays(-282), now.AddDays(-281), 3, 9));
        iterations.Add(new PullRequestIterationDto(107, 2, now.AddDays(-281), now.AddDays(-280), 1, 3));

        // Sprint 4 PRs - Consistent
        iterations.Add(new PullRequestIterationDto(108, 1, now.AddDays(-265), now.AddDays(-263), 6, 20));
        iterations.Add(new PullRequestIterationDto(108, 2, now.AddDays(-262), now.AddDays(-261), 3, 8));

        iterations.Add(new PullRequestIterationDto(109, 1, now.AddDays(-262), now.AddDays(-260), 4, 12));
        iterations.Add(new PullRequestIterationDto(109, 2, now.AddDays(-260), now.AddDays(-259), 2, 5));

        // Sprint 5 PRs - Reduced capacity
        iterations.Add(new PullRequestIterationDto(110, 1, now.AddDays(-245), now.AddDays(-243), 3, 10));
        iterations.Add(new PullRequestIterationDto(110, 2, now.AddDays(-243), now.AddDays(-242), 1, 4));

        iterations.Add(new PullRequestIterationDto(111, 1, now.AddDays(-240), now.AddDays(-239), 2, 7));
        iterations.Add(new PullRequestIterationDto(111, 2, now.AddDays(-239), now.AddDays(-238), 1, 3));

        // Sprint 6 PRs - Recovery
        iterations.Add(new PullRequestIterationDto(112, 1, now.AddDays(-225), now.AddDays(-222), 7, 25));
        iterations.Add(new PullRequestIterationDto(112, 2, now.AddDays(-221), now.AddDays(-220), 3, 10));

        iterations.Add(new PullRequestIterationDto(113, 1, now.AddDays(-222), now.AddDays(-220), 3, 9));
        iterations.Add(new PullRequestIterationDto(113, 2, now.AddDays(-220), now.AddDays(-219), 2, 5));

        iterations.Add(new PullRequestIterationDto(114, 1, now.AddDays(-217), now.AddDays(-216), 3, 8));
        iterations.Add(new PullRequestIterationDto(114, 2, now.AddDays(-216), now.AddDays(-215), 1, 3));

        // Sprint 7 PRs - Testing focus
        iterations.Add(new PullRequestIterationDto(115, 1, now.AddDays(-200), now.AddDays(-198), 4, 15));
        iterations.Add(new PullRequestIterationDto(115, 2, now.AddDays(-198), now.AddDays(-197), 1, 4));

        iterations.Add(new PullRequestIterationDto(116, 1, now.AddDays(-198), now.AddDays(-195), 5, 20));
        iterations.Add(new PullRequestIterationDto(116, 2, now.AddDays(-195), now.AddDays(-194), 2, 6));

        iterations.Add(new PullRequestIterationDto(117, 1, now.AddDays(-192), now.AddDays(-191), 2, 8));
        iterations.Add(new PullRequestIterationDto(117, 2, now.AddDays(-191), now.AddDays(-190), 1, 3));

        // Sprint 8 PRs - Stable
        iterations.Add(new PullRequestIterationDto(118, 1, now.AddDays(-175), now.AddDays(-172), 6, 22));
        iterations.Add(new PullRequestIterationDto(118, 2, now.AddDays(-171), now.AddDays(-170), 2, 7));

        iterations.Add(new PullRequestIterationDto(119, 1, now.AddDays(-172), now.AddDays(-170), 4, 14));
        iterations.Add(new PullRequestIterationDto(119, 2, now.AddDays(-170), now.AddDays(-169), 1, 4));

        // Sprint 9 PRs - High velocity
        iterations.Add(new PullRequestIterationDto(120, 1, now.AddDays(-155), now.AddDays(-152), 7, 25));
        iterations.Add(new PullRequestIterationDto(120, 2, now.AddDays(-151), now.AddDays(-150), 3, 9));

        iterations.Add(new PullRequestIterationDto(121, 1, now.AddDays(-152), now.AddDays(-150), 4, 16));
        iterations.Add(new PullRequestIterationDto(121, 2, now.AddDays(-149), now.AddDays(-148), 2, 6));

        iterations.Add(new PullRequestIterationDto(122, 1, now.AddDays(-147), now.AddDays(-145), 4, 14));
        iterations.Add(new PullRequestIterationDto(122, 2, now.AddDays(-145), now.AddDays(-144), 1, 4));

        // Sprint 10 PRs - Active
        iterations.Add(new PullRequestIterationDto(123, 1, now.AddDays(-125), now.AddDays(-123), 5, 18));
        iterations.Add(new PullRequestIterationDto(123, 2, now.AddDays(-123), now.AddDays(-122), 2, 6));

        iterations.Add(new PullRequestIterationDto(124, 1, now.AddDays(-120), now.AddDays(-120), 3, 10));

        // Sprint 11 PRs - Current
        iterations.Add(new PullRequestIterationDto(125, 1, now.AddDays(-10), now.AddDays(-10), 4, 12));
        iterations.Add(new PullRequestIterationDto(125, 2, now.AddDays(-5), now.AddDays(-5), 2, 5));

        iterations.Add(new PullRequestIterationDto(126, 1, now.AddDays(-8), now.AddDays(-8), 5, 16));
        iterations.Add(new PullRequestIterationDto(126, 2, now.AddDays(-3), now.AddDays(-3), 2, 7));

        // Sprint 12 PRs - Future
        iterations.Add(new PullRequestIterationDto(127, 1, now.AddDays(-2), now.AddDays(-2), 3, 9));

        return iterations;
    }

    /// <summary>
    /// Generates mock comments for pull requests.
    /// Provides representative comments for demonstration purposes.
    /// </summary>
    public List<PullRequestCommentDto> GetMockComments()
    {
        var now = DateTimeOffset.UtcNow;
        var comments = new List<PullRequestCommentDto>();

        // Sprint 1 PRs - Simple, quick reviews
        comments.Add(new PullRequestCommentDto(1, 101, 1, "Bob Smith", "LGTM, nice clean UI", now.AddDays(-329), null, true, now.AddDays(-328), "Alice Johnson"));
        comments.Add(new PullRequestCommentDto(2, 102, 1, "Charlie Davis", "Good persistence implementation", now.AddDays(-326), null, true, now.AddDays(-325), "Bob Smith"));

        // Sprint 2 PRs - Some iterations needed
        comments.Add(new PullRequestCommentDto(3, 103, 1, "Diana Evans", "Please add null checks for edge cases", now.AddDays(-309), now.AddDays(-308), true, now.AddDays(-308), "Charlie Davis"));
        comments.Add(new PullRequestCommentDto(4, 104, 1, "Eve Foster", "Consider performance optimization", now.AddDays(-304), null, true, now.AddDays(-303), "Diana Evans"));

        // Sprint 3 PRs - More complex reviews
        comments.Add(new PullRequestCommentDto(5, 105, 1, "Frank Green", "Icon sizing needs adjustment", now.AddDays(-289), now.AddDays(-287), true, now.AddDays(-287), "Eve Foster"));
        comments.Add(new PullRequestCommentDto(6, 106, 1, "Grace Hill", "Excellent drag-drop implementation!", now.AddDays(-287), null, true, now.AddDays(-284), "Frank Green"));
        comments.Add(new PullRequestCommentDto(7, 107, 1, "Henry Irving", "Add keyboard shortcut documentation", now.AddDays(-281), null, true, now.AddDays(-280), "Grace Hill"));

        // Sprint 4 PRs - Technical depth
        comments.Add(new PullRequestCommentDto(8, 108, 1, "Ivy Jones", "Cache key naming convention", now.AddDays(-264), now.AddDays(-262), true, now.AddDays(-262), "Henry Irving"));
        comments.Add(new PullRequestCommentDto(9, 109, 1, "Jack King", "Invalidation strategy looks solid", now.AddDays(-261), null, true, now.AddDays(-259), "Ivy Jones"));

        // Sprint 5 PRs
        comments.Add(new PullRequestCommentDto(10, 110, 1, "Kate Lee", "Performance improvement confirmed", now.AddDays(-244), null, true, now.AddDays(-242), "Jack King"));
        comments.Add(new PullRequestCommentDto(11, 111, 1, "Liam Moore", "Virtual scroll works great", now.AddDays(-239), null, true, now.AddDays(-238), "Kate Lee"));

        // Sprint 6 PRs - Complex features
        comments.Add(new PullRequestCommentDto(12, 112, 1, "Mia Nelson", "Web worker implementation needs error handling", now.AddDays(-224), now.AddDays(-221), true, now.AddDays(-221), "Liam Moore"));
        comments.Add(new PullRequestCommentDto(13, 112, 2, "Noah Parker", "Memory leak concern in worker thread", now.AddDays(-223), now.AddDays(-221), true, now.AddDays(-221), "Liam Moore"));
        comments.Add(new PullRequestCommentDto(14, 113, 1, "Olivia Quinn", "Progress indicators are smooth", now.AddDays(-221), null, true, now.AddDays(-219), "Mia Nelson"));

        // Sprint 7 PRs - Testing focus
        comments.Add(new PullRequestCommentDto(15, 115, 1, "Peter Roberts", "Excellent test coverage", now.AddDays(-199), null, true, now.AddDays(-197), "Olivia Quinn"));
        comments.Add(new PullRequestCommentDto(16, 116, 1, "Quinn Scott", "Add more edge case tests", now.AddDays(-197), now.AddDays(-195), true, now.AddDays(-195), "Peter Roberts"));
        comments.Add(new PullRequestCommentDto(17, 117, 1, "Rachel Taylor", "Validation logic is well tested", now.AddDays(-191), null, true, now.AddDays(-190), "Quinn Scott"));

        // Sprint 8 PRs
        comments.Add(new PullRequestCommentDto(18, 118, 1, "Sam Turner", "Handler tests are comprehensive", now.AddDays(-174), null, true, now.AddDays(-170), "Rachel Taylor"));
        comments.Add(new PullRequestCommentDto(19, 119, 1, "Tara White", "Great edge case coverage", now.AddDays(-171), null, true, now.AddDays(-169), "Sam Turner"));

        // Sprint 9 PRs - Integration testing
        comments.Add(new PullRequestCommentDto(20, 120, 1, "Uma Young", "API integration tests look good", now.AddDays(-154), null, true, now.AddDays(-150), "Tara White"));
        comments.Add(new PullRequestCommentDto(21, 121, 1, "Victor Zhang", "DB tests need cleanup in teardown", now.AddDays(-151), now.AddDays(-149), true, now.AddDays(-149), "Uma Young"));
        comments.Add(new PullRequestCommentDto(22, 122, 1, "Wendy Adams", "SignalR tests work perfectly", now.AddDays(-146), null, true, now.AddDays(-144), "Victor Zhang"));

        // Sprint 10 PRs - Current work
        comments.Add(new PullRequestCommentDto(23, 123, 1, "Xander Brown", "Serilog config looks correct", now.AddDays(-124), null, true, now.AddDays(-122), "Wendy Adams"));
        comments.Add(new PullRequestCommentDto(24, 124, 1, "Yara Clark", "Add more log level examples", now.AddDays(-120), null, false, null, null));

        // Sprint 11 PRs - Active reviews
        comments.Add(new PullRequestCommentDto(25, 125, 1, "Zach Davis", "Event structure needs refinement", now.AddDays(-9), null, false, null, null));
        comments.Add(new PullRequestCommentDto(26, 125, 2, "Amy Evans", "Consider using structured logging patterns", now.AddDays(-7), null, false, null, null));
        comments.Add(new PullRequestCommentDto(27, 126, 1, "Bob Smith", "Aggregation setup needs documentation", now.AddDays(-6), null, false, null, null));

        // Sprint 12 PR - Just started
        comments.Add(new PullRequestCommentDto(28, 127, 1, "Charlie Davis", "Swagger config looks good so far", now.AddDays(-1), null, false, null, null));

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
