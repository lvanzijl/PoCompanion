using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;
using CdcModels = PoTool.Core.Domain.Models;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetSprintExecutionQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new PoToolDbContext(options);
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_UsesCdcSprintServices_ForExecutionReconstruction()
    {
        await SeedSprintExecutionScenarioAsync("Active", stateTransitions: [], iterationTransitions: []);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse());

        var commitmentService = new Mock<ISprintCommitmentService>(MockBehavior.Strict);
        var scopeChangeService = new Mock<ISprintScopeChangeService>(MockBehavior.Strict);
        var completionService = new Mock<ISprintCompletionService>(MockBehavior.Strict);
        var spilloverService = new Mock<ISprintSpilloverService>(MockBehavior.Strict);
        var sprintFactService = new Mock<ISprintFactService>(MockBehavior.Strict);
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        commitmentService
            .Setup(service => service.GetCommitmentTimestamp(sprintStart))
            .Returns(sprintStart.AddDays(1));
        commitmentService
            .Setup(service => service.BuildCommittedWorkItemIds(
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                "\\Project\\Sprint 1",
                sprintStart.AddDays(1)))
            .Returns(new HashSet<int>());
        scopeChangeService
            .Setup(service => service.DetectScopeAdded(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>()))
            .Returns(Array.Empty<SprintScopeAdded>());
        scopeChangeService
            .Setup(service => service.DetectScopeRemoved(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>()))
            .Returns(Array.Empty<SprintScopeRemoved>());
        completionService
            .Setup(service => service.BuildFirstDoneByWorkItem(
                It.IsAny<IEnumerable<CdcModels.FieldChangeEvent>>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<(string WorkItemType, string StateName), PoTool.Core.Domain.Models.StateClassification>?>()))
            .Returns(new Dictionary<int, DateTimeOffset>());
        spilloverService
            .Setup(service => service.GetNextSprintPath(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IEnumerable<CdcModels.SprintDefinition>>()))
            .Returns((string?)null);
        spilloverService
            .Setup(service => service.BuildSpilloverWorkItemIds(
                It.IsAny<IReadOnlySet<int>>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<(string WorkItemType, string StateName), PoTool.Core.Domain.Models.StateClassification>?>(),
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>()))
            .Returns(new HashSet<int>());
        sprintFactService
            .Setup(service => service.BuildSprintFactResult(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.CanonicalWorkItem>>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<(string WorkItemType, string StateName), PoTool.Core.Domain.Models.StateClassification>?>(),
                It.IsAny<string?>()))
            .Returns(new SprintFactResult(
                CommittedStoryPoints: 5,
                AddedStoryPoints: 0,
                RemovedStoryPoints: 0,
                DeliveredStoryPoints: 0,
                DeliveredFromAddedStoryPoints: 0,
                SpilloverStoryPoints: 0,
                RemainingStoryPoints: 5));

        var handler = new GetSprintExecutionQueryHandler(
            _context,
            _stateClassificationService.Object,
            commitmentService.Object,
            scopeChangeService.Object,
            completionService.Object,
            spilloverService.Object,
            sprintFactService.Object,
            new PoTool.Core.Domain.Metrics.SprintExecutionMetricsCalculator(),
            NullLogger<GetSprintExecutionQueryHandler>.Instance);

        var result = await handler.Handle(new GetSprintExecutionQuery(1, 1, 1), CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(0, result.Summary.InitialScopeCount);
        Assert.AreEqual(1, result.Summary.UnfinishedCount);
        commitmentService.VerifyAll();
        scopeChangeService.VerifyAll();
        completionService.VerifyAll();
        spilloverService.VerifyAll();
        sprintFactService.VerifyAll();
    }

    private GetSprintExecutionQueryHandler CreateHandler()
    {
        return new GetSprintExecutionQueryHandler(
            _context,
            _stateClassificationService.Object,
            new SprintCommitmentService(),
            new SprintScopeChangeService(),
            new SprintCompletionService(),
            new SprintSpilloverService(),
            new SprintFactService(
                new SprintCommitmentService(),
                new SprintScopeChangeService(),
                new SprintCompletionService(),
                new SprintSpilloverService(),
                new CanonicalStoryPointResolutionService()),
            new PoTool.Core.Domain.Metrics.SprintExecutionMetricsCalculator(),
            NullLogger<GetSprintExecutionQueryHandler>.Instance);
    }

    private async Task SeedSprintExecutionScenarioAsync(
        string workItemState,
        string? currentIterationPath = null,
        int? storyPoints = 5,
        int? businessValue = null,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)>? stateTransitions = null,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)>? iterationTransitions = null,
        IEnumerable<(int Id, string Name, string Path, DateTimeOffset StartUtc, DateTimeOffset EndUtc)>? additionalSprints = null,
        IEnumerable<SprintExecutionTestWorkItem>? additionalWorkItems = null)
    {
        var profile = new ProfileEntity
        {
            Id = 1,
            Name = "PO"
        };

        var team = new TeamEntity
        {
            Id = 1,
            Name = "Team",
            TeamAreaPath = "\\Project\\Team"
        };

        var product = new ProductEntity
        {
            Id = 1,
            ProductOwnerId = profile.Id,
            Name = "Product"
        };

        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));

        var sprint = new SprintEntity
        {
            Id = 1,
            TeamId = team.Id,
            Name = "Sprint 1",
            Path = "\\Project\\Sprint 1",
            StartUtc = sprintStart,
            StartDateUtc = sprintStart.UtcDateTime,
            EndUtc = sprintEnd,
            EndDateUtc = sprintEnd.UtcDateTime,
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow
        };

        var workItem = new WorkItemEntity
        {
            TfsId = 101,
            Type = WorkItemType.Pbi,
            Title = "PBI 101",
            ParentTfsId = null,
            AreaPath = "\\Project",
            IterationPath = currentIterationPath ?? sprint.Path,
            State = workItemState,
            Effort = 8,
            StoryPoints = storyPoints,
            BusinessValue = businessValue,
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };

        var resolved = new ResolvedWorkItemEntity
        {
            WorkItemId = 101,
            WorkItemType = WorkItemType.Pbi,
            ResolvedProductId = product.Id,
            ResolvedSprintId = sprint.Id,
            ResolutionStatus = ResolutionStatus.Resolved,
            ResolvedAtRevision = 1
        };

        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Sprints.Add(sprint);

        foreach (var (id, name, path, startUtc, endUtc) in additionalSprints ?? [])
        {
            _context.Sprints.Add(new SprintEntity
            {
                Id = id,
                TeamId = team.Id,
                Name = name,
                Path = path,
                StartUtc = startUtc,
                StartDateUtc = startUtc.UtcDateTime,
                EndUtc = endUtc,
                EndDateUtc = endUtc.UtcDateTime,
                LastSyncedUtc = DateTimeOffset.UtcNow,
                LastSyncedDateUtc = DateTime.UtcNow
            });
        }

        _context.WorkItems.Add(workItem);
        _context.ResolvedWorkItems.Add(resolved);
        var updateId = 1;

        AddStateTransitions(workItem.TfsId, profile.Id, stateTransitions ?? [(sprintStart.AddDays(2), "Active", workItemState)], ref updateId);
        AddIterationTransitions(workItem.TfsId, profile.Id, iterationTransitions ?? [], ref updateId);

        foreach (var additionalWorkItem in additionalWorkItems ?? [])
        {
            _context.WorkItems.Add(new WorkItemEntity
            {
                TfsId = additionalWorkItem.TfsId,
                Type = additionalWorkItem.Type,
                Title = additionalWorkItem.Title,
                ParentTfsId = additionalWorkItem.ParentTfsId,
                AreaPath = "\\Project",
                IterationPath = additionalWorkItem.CurrentIterationPath ?? sprint.Path,
                State = additionalWorkItem.State,
                Effort = additionalWorkItem.Effort,
                StoryPoints = additionalWorkItem.StoryPoints,
                BusinessValue = additionalWorkItem.BusinessValue,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            });

            _context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
            {
                WorkItemId = additionalWorkItem.TfsId,
                WorkItemType = additionalWorkItem.Type,
                ResolvedProductId = product.Id,
                ResolvedSprintId = sprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

            AddStateTransitions(
                additionalWorkItem.TfsId,
                profile.Id,
                additionalWorkItem.StateTransitions ?? [(sprintStart.AddDays(2), "Active", additionalWorkItem.State)],
                ref updateId);
            AddIterationTransitions(
                additionalWorkItem.TfsId,
                profile.Id,
                additionalWorkItem.IterationTransitions ?? [],
                ref updateId);
        }

        await _context.SaveChangesAsync();
    }

    private void AddStateTransitions(
        int workItemId,
        int productOwnerId,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)> transitions,
        ref int updateId)
    {
        foreach (var (timestamp, oldState, newState) in transitions)
        {
            _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = workItemId,
                UpdateId = updateId++,
                FieldRefName = "System.State",
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldState,
                NewValue = newState
            });
        }
    }

    private void AddIterationTransitions(
        int workItemId,
        int productOwnerId,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)> transitions,
        ref int updateId)
    {
        foreach (var (timestamp, oldIterationPath, newIterationPath) in transitions)
        {
            _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = workItemId,
                UpdateId = updateId++,
                FieldRefName = "System.IterationPath",
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldIterationPath,
                NewValue = newIterationPath
            });
        }
    }

    private static GetStateClassificationsResponse BuildStateClassificationsResponse(
        params (string WorkItemType, string StateName, PoTool.Shared.Settings.StateClassification Classification)[] classifications)
    {
        return new GetStateClassificationsResponse
        {
            ProjectName = "Test",
            IsDefault = false,
            Classifications = classifications.Select(classification => new WorkItemStateClassificationDto
            {
                WorkItemType = classification.WorkItemType,
                StateName = classification.StateName,
                Classification = classification.Classification
            }).ToList()
        };
    }

    private sealed record SprintExecutionTestWorkItem(
        int TfsId,
        string Type,
        string Title,
        string State,
        string? CurrentIterationPath,
        int? Effort,
        int? StoryPoints,
        int? BusinessValue,
        int? ParentTfsId = null,
        IEnumerable<(DateTimeOffset Timestamp, string OldState, string NewState)>? StateTransitions = null,
        IEnumerable<(DateTimeOffset Timestamp, string? OldIterationPath, string? NewIterationPath)>? IterationTransitions = null);
}
