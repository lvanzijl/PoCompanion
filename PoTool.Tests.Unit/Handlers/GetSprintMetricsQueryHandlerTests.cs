using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;
using CdcModels = PoTool.Core.Domain.Models;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetSprintMetricsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<ISprintRepository> _sprintRepository = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;
    private ISprintCommitmentService _sprintCommitmentService = null!;
    private ISprintScopeChangeService _sprintScopeChangeService = null!;
    private ISprintCompletionService _sprintCompletionService = null!;
    private ISprintFactService _sprintFactService = null!;
    private Mock<IMediator> _mediator = null!;
    private Mock<IWorkItemReadProvider> _workItemReadProvider = null!;
    private GetSprintMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new PoToolDbContext(options);
        _productRepository = new Mock<IProductRepository>();
        _sprintRepository = new Mock<ISprintRepository>();
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>();
        _sprintCommitmentService = new SprintCommitmentService();
        _sprintScopeChangeService = new SprintScopeChangeService();
        _sprintCompletionService = new SprintCompletionService();
        _sprintFactService = new SprintFactService(
            _sprintCommitmentService,
            _sprintScopeChangeService,
            _sprintCompletionService,
            new SprintSpilloverService(),
            new CanonicalStoryPointResolutionService());
        _mediator = new Mock<IMediator>();
        _workItemReadProvider = new Mock<IWorkItemReadProvider>();

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildStateClassificationsResponse(
                (WorkItemType.Pbi, "Resolved", PoTool.Shared.Settings.StateClassification.Done),
                (WorkItemType.Pbi, "Active", PoTool.Shared.Settings.StateClassification.InProgress),
                (WorkItemType.Bug, "Resolved", PoTool.Shared.Settings.StateClassification.Done),
                (WorkItemType.Bug, "Active", PoTool.Shared.Settings.StateClassification.InProgress),
                (WorkItemType.Task, "Resolved", PoTool.Shared.Settings.StateClassification.Done),
                (WorkItemType.Task, "Active", PoTool.Shared.Settings.StateClassification.InProgress)));
        _workItemReadProvider
            .Setup(provider => provider.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = CreateHandler();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_ReturnsNull_WhenSprintMetadataIsMissing()
    {
        await SeedWorkItemAsync(101, WorkItemType.Pbi, "\\Project\\Sprint 1", "Resolved", storyPoints: 5);
        _sprintRepository
            .Setup(repository => repository.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetSprintMetricsQuery("\\Project\\Sprint 1"), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_UsesCdcSprintServices_ForScopeAndCompletionReconstruction()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));
        const string sprintPath = "\\Project\\Sprint 1";

        _sprintRepository
            .Setup(repository => repository.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateSprint(1, sprintPath, "Sprint 1", sprintStart, sprintEnd)
            ]);

        await SeedWorkItemAsync(101, WorkItemType.Pbi, sprintPath, "Active", storyPoints: 5);

        var commitmentService = new Mock<ISprintCommitmentService>(MockBehavior.Strict);
        var scopeChangeService = new Mock<ISprintScopeChangeService>(MockBehavior.Strict);
        var completionService = new Mock<ISprintCompletionService>(MockBehavior.Strict);
        var sprintFactService = new Mock<ISprintFactService>(MockBehavior.Strict);

        commitmentService
            .Setup(service => service.GetCommitmentTimestamp(sprintStart))
            .Returns(sprintStart.AddDays(1));
        commitmentService
            .Setup(service => service.BuildCommittedWorkItemIds(
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                sprintPath,
                sprintStart.AddDays(1)))
            .Returns(new HashSet<int>());
        scopeChangeService
            .Setup(service => service.DetectScopeAdded(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>()))
            .Returns(Array.Empty<SprintScopeAdded>());
        completionService
            .Setup(service => service.BuildFirstDoneByWorkItem(
                It.IsAny<IEnumerable<CdcModels.FieldChangeEvent>>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<(string WorkItemType, string StateName), PoTool.Core.Domain.Models.StateClassification>?>()))
            .Returns(new Dictionary<int, DateTimeOffset>());
        sprintFactService
            .Setup(service => service.BuildSprintFactResult(
                It.IsAny<CdcModels.SprintDefinition>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.CanonicalWorkItem>>(),
                It.IsAny<IReadOnlyDictionary<int, CdcModels.WorkItemSnapshot>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<int, IReadOnlyList<CdcModels.FieldChangeEvent>>>(),
                It.IsAny<IReadOnlyDictionary<(string WorkItemType, string StateName), PoTool.Core.Domain.Models.StateClassification>?>(),
                null))
            .Returns(new SprintFactResult(
                CommittedStoryPoints: 5,
                AddedStoryPoints: 0,
                RemovedStoryPoints: 0,
                DeliveredStoryPoints: 3,
                DeliveredFromAddedStoryPoints: 0,
                SpilloverStoryPoints: 0,
                RemainingStoryPoints: 2));

        var handler = CreateHandler(
            commitmentService.Object,
            scopeChangeService.Object,
            completionService.Object,
            sprintFactService.Object);

        var result = await handler.Handle(new GetSprintMetricsQuery(sprintPath), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.PlannedStoryPoints);
        Assert.AreEqual(3, result.CompletedStoryPoints);
        Assert.AreEqual(0, result.TotalWorkItemCount);
        commitmentService.VerifyAll();
        scopeChangeService.VerifyAll();
        completionService.VerifyAll();
        sprintFactService.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_ReturnsZeroMetrics_ForKnownSprintWithoutHistoricalScope()
    {
        var sprintStart = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprintEnd = new DateTimeOffset(new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc));

        _sprintRepository
            .Setup(repository => repository.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateSprint(1, "\\Project\\Sprint 1", "Sprint 1", sprintStart, sprintEnd)
            ]);

        var result = await _handler.Handle(new GetSprintMetricsQuery("\\Project\\Sprint 1"), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.PlannedStoryPoints);
        Assert.AreEqual(0, result.CompletedStoryPoints);
        Assert.AreEqual(0, result.CompletedWorkItemCount);
        Assert.AreEqual(0, result.TotalWorkItemCount);
        Assert.AreEqual(sprintStart, result.StartDate);
        Assert.AreEqual(sprintEnd, result.EndDate);
    }

    private async Task SeedWorkItemAsync(
        int id,
        string type,
        string iterationPath,
        string state,
        int? effort = null,
        int? storyPoints = null,
        int? businessValue = null)
    {
        _context.WorkItems.Add(new WorkItemEntity
        {
            TfsId = id,
            Type = type,
            Title = $"Work Item {id}",
            AreaPath = "\\Project",
            IterationPath = iterationPath,
            State = state,
            Effort = effort,
            StoryPoints = storyPoints,
            BusinessValue = businessValue,
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _workItemReadProvider
            .Setup(provider => provider.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildReadModelWorkItems());
    }

    private GetSprintMetricsQueryHandler CreateHandler(
        ISprintCommitmentService? sprintCommitmentService = null,
        ISprintScopeChangeService? sprintScopeChangeService = null,
        ISprintCompletionService? sprintCompletionService = null,
        ISprintFactService? sprintFactService = null)
    {
        return new GetSprintMetricsQueryHandler(
            _sprintRepository.Object,
            _stateClassificationService.Object,
            sprintCommitmentService ?? _sprintCommitmentService,
            sprintScopeChangeService ?? _sprintScopeChangeService,
            sprintCompletionService ?? _sprintCompletionService,
            sprintFactService ?? _sprintFactService,
            new SprintScopedWorkItemLoader(
                _workItemReadProvider.Object,
                _productRepository.Object,
                _mediator.Object),
            _context,
            NullLogger<GetSprintMetricsQueryHandler>.Instance);
    }

    private List<PoTool.Shared.WorkItems.WorkItemDto> BuildReadModelWorkItems()
    {
        return _context.WorkItems
            .AsNoTracking()
            .ToList()
            .Select(entity => new PoTool.Shared.WorkItems.WorkItemDto(
                TfsId: entity.TfsId,
                Type: entity.Type,
                Title: entity.Title,
                ParentTfsId: entity.ParentTfsId,
                AreaPath: entity.AreaPath,
                IterationPath: entity.IterationPath,
                State: entity.State,
                RetrievedAt: entity.RetrievedAt,
                Effort: entity.Effort,
                Description: entity.Description,
                CreatedDate: entity.CreatedDate,
                ClosedDate: entity.ClosedDate,
                Severity: entity.Severity,
                Tags: entity.Tags,
                BusinessValue: entity.BusinessValue,
                BacklogPriority: entity.BacklogPriority,
                StoryPoints: entity.StoryPoints,
                TimeCriticality: entity.TimeCriticality,
                ProjectNumber: entity.ProjectNumber,
                ProjectElement: entity.ProjectElement,
                ChangedDate: entity.TfsChangedDate))
            .ToList();
    }

    private async Task SeedStateEventAsync(int workItemId, DateTimeOffset timestamp, string oldState, string newState)
    {
        _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
        {
            ProductOwnerId = 1,
            WorkItemId = workItemId,
            UpdateId = NextUpdateId(),
            FieldRefName = "System.State",
            EventTimestamp = timestamp,
            EventTimestampUtc = timestamp.UtcDateTime,
            OldValue = oldState,
            NewValue = newState
        });

        await _context.SaveChangesAsync();
    }

    private async Task SeedIterationEventAsync(int workItemId, DateTimeOffset timestamp, string? oldIterationPath, string? newIterationPath)
    {
        _context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
        {
            ProductOwnerId = 1,
            WorkItemId = workItemId,
            UpdateId = NextUpdateId(),
            FieldRefName = "System.IterationPath",
            EventTimestamp = timestamp,
            EventTimestampUtc = timestamp.UtcDateTime,
            OldValue = oldIterationPath,
            NewValue = newIterationPath
        });

        await _context.SaveChangesAsync();
    }

    private int NextUpdateId()
    {
        return _context.ActivityEventLedgerEntries.Count() + 1;
    }

    private static SprintDto CreateSprint(
        int id,
        string path,
        string name,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        return new SprintDto(
            Id: id,
            TeamId: 1,
            TfsIterationId: null,
            Path: path,
            Name: name,
            StartUtc: startUtc,
            EndUtc: endUtc,
            TimeFrame: null,
            LastSyncedUtc: DateTimeOffset.UtcNow);
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
}
