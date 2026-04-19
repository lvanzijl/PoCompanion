using PoTool.Core.Contracts;
using PoTool.Core.Planning;
using PoTool.Core.Pipelines;
using PoTool.Core.PullRequests;
using PoTool.Core.WorkItems;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Domain.Tests;

internal static class ProductPlanningBoardTestFactory
{
    private static readonly DateTime DefaultSprintStartUtc = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    internal static ProductPlanningBoardService CreateService(params WorkItemDto[] workItems)
    {
        return CreateService(new InMemoryProductPlanningSessionStore(), new InMemoryProductPlanningIntentStore(), new RecordingTfsClient(), workItems);
    }

    internal static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        params WorkItemDto[] workItems)
    {
        return CreateService(sessionStore, new InMemoryProductPlanningIntentStore(), new RecordingTfsClient(), workItems);
    }

    internal static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        InMemoryProductPlanningIntentStore intentStore,
        RecordingTfsClient tfsClient,
        params WorkItemDto[] workItems)
    {
        return CreateService(
            sessionStore,
            intentStore,
            tfsClient,
            [CreateProduct(7, "Roadmap Product", 100)],
            new Dictionary<int, IReadOnlyList<WorkItemDto>>
            {
                [100] = workItems
            },
            CreateDefaultSprintsByTeam([10]));
    }

    internal static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        InMemoryProductPlanningIntentStore intentStore,
        RecordingTfsClient tfsClient,
        IReadOnlyList<ProductDto> products,
        IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>> workItemsByRootId,
        IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> sprintsByTeamId)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(products.ToArray()),
            new FakeWorkItemReadProvider(workItemsByRootId),
            sessionStore,
            intentStore,
            new FakeSprintRepository(sprintsByTeamId),
            tfsClient);
    }

    internal static ProductDto CreateProduct(int productId, string name, params int[] rootIds)
    {
        return CreateProduct(productId, name, rootIds, [10]);
    }

    internal static ProductDto CreateProduct(int productId, string name, IReadOnlyList<int> rootIds, IReadOnlyList<int> teamIds)
    {
        return new ProductDto(
            productId,
            null,
            name,
            rootIds.ToList(),
            0,
            ProductPictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            teamIds.ToList(),
            []);
    }

    internal static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        int? parentId,
        double? backlogPriority,
        string? tags,
        DateTimeOffset? startDate = null,
        DateTimeOffset? targetDate = null)
    {
        return new WorkItemDto(
            tfsId,
            type,
            title,
            parentId,
            "Area",
            "Iteration",
            "Active",
            DateTimeOffset.UtcNow,
            null,
            null,
            BacklogPriority: backlogPriority,
            Tags: tags,
            StartDate: startDate,
            TargetDate: targetDate);
    }

    internal static IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> CreateDefaultSprintsByTeam(IEnumerable<int> teamIds, int sprintCount = 10)
    {
        return teamIds.ToDictionary(
            static teamId => teamId,
            teamId => (IReadOnlyList<SprintDto>)CreateSequentialSprints(teamId, DefaultSprintStartUtc, sprintCount));
    }

    internal static List<SprintDto> CreateSequentialSprints(int teamId, DateTime startDateUtc, int count, int sprintLengthDays = 14)
    {
        var result = new List<SprintDto>(count);
        for (var index = 0; index < count; index++)
        {
            var sprintStart = DateTime.SpecifyKind(startDateUtc.Date.AddDays(index * sprintLengthDays), DateTimeKind.Utc);
            var sprintEndExclusive = sprintStart.AddDays(sprintLengthDays);
            result.Add(new SprintDto(
                index + 1,
                teamId,
                $"team-{teamId}-sprint-{index + 1}",
                $"Team {teamId}\\Sprint {index + 1}",
                $"Sprint {index + 1}",
                new DateTimeOffset(sprintStart, TimeSpan.Zero),
                new DateTimeOffset(sprintEndExclusive, TimeSpan.Zero),
                null,
                DateTimeOffset.UtcNow));
        }

        return result;
    }

    internal sealed class FakeProductRepository : IProductRepository
    {
        private readonly Dictionary<int, ProductDto> _products;

        public FakeProductRepository(params ProductDto[] products)
        {
            _products = products.ToDictionary(static product => product.Id);
        }

        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.GetValueOrDefault(id));
        }

        public Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> CreateProductAsync(int? productOwnerId, string name, List<int> backlogRootWorkItemIds, ProductPictureType pictureType, int defaultPictureId, string? customPicturePath, EstimationMode estimationMode = EstimationMode.StoryPoints, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> UpdateProductAsync(int id, string name, List<int> backlogRootWorkItemIds, ProductPictureType? pictureType, int? defaultPictureId, string? customPicturePath, EstimationMode? estimationMode = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> ReorderProductsAsync(int productOwnerId, List<int> productIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeWorkItemReadProvider : IWorkItemReadProvider
    {
        private readonly IReadOnlyList<WorkItemDto>? _workItems;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>>? _workItemsByRootId;

        public FakeWorkItemReadProvider(params WorkItemDto[] workItems)
        {
            _workItems = workItems;
        }

        public FakeWorkItemReadProvider(IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>> workItemsByRootId)
        {
            _workItemsByRootId = workItemsByRootId;
        }

        public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
        {
            if (_workItemsByRootId is not null)
            {
                var workItems = rootWorkItemIds
                    .Distinct()
                    .OrderBy(static rootId => rootId)
                    .SelectMany(rootId => _workItemsByRootId.GetValueOrDefault(rootId, Array.Empty<WorkItemDto>()))
                    .ToArray();

                return Task.FromResult<IEnumerable<WorkItemDto>>(workItems);
            }

            return Task.FromResult<IEnumerable<WorkItemDto>>(_workItems ?? Array.Empty<WorkItemDto>());
        }

        public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeSprintRepository : ISprintRepository
    {
        private readonly IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> _sprintsByTeamId;

        public FakeSprintRepository(IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> sprintsByTeamId)
        {
            _sprintsByTeamId = sprintsByTeamId;
        }

        public Task<IEnumerable<SprintDto>> GetAllSprintsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SprintDto>>(_sprintsByTeamId.Values.SelectMany(static sprints => sprints).ToArray());

        public Task<IEnumerable<SprintDto>> GetSprintsForTeamAsync(int teamId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SprintDto>>(_sprintsByTeamId.GetValueOrDefault(teamId, Array.Empty<SprintDto>()));

        public Task<SprintDto?> GetCurrentSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(_sprintsByTeamId.GetValueOrDefault(teamId, Array.Empty<SprintDto>()).FirstOrDefault());

        public Task UpsertSprintsForTeamAsync(int teamId, IEnumerable<TeamIterationDto> iterations, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveStaleSprintsAsync(int teamId, DateTimeOffset olderThan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class InMemoryProductPlanningIntentStore : IProductPlanningIntentStore
    {
        private readonly Dictionary<int, Dictionary<int, ProductPlanningIntentRecord>> _store = new();

        public Task<IReadOnlyList<ProductPlanningIntentRecord>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProductPlanningIntentRecord> intents = _store.TryGetValue(productId, out var byEpic)
                ? byEpic.Values.OrderBy(static intent => intent.EpicId).ToArray()
                : Array.Empty<ProductPlanningIntentRecord>();
            return Task.FromResult(intents);
        }

        public Task UpsertForProductAsync(int productId, IReadOnlyList<ProductPlanningIntentRecord> intents, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(productId, out var byEpic))
            {
                byEpic = new Dictionary<int, ProductPlanningIntentRecord>();
                _store[productId] = byEpic;
            }

            foreach (var intent in intents)
            {
                byEpic[intent.EpicId] = intent;
            }

            return Task.CompletedTask;
        }

        public Task DeleteMissingEpicsAsync(int productId, IReadOnlyCollection<int> activeEpicIds, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(productId, out var byEpic))
            {
                return Task.CompletedTask;
            }

            var activeSet = activeEpicIds.ToHashSet();
            foreach (var staleEpicId in byEpic.Keys.Where(epicId => !activeSet.Contains(epicId)).ToArray())
            {
                byEpic.Remove(staleEpicId);
            }

            return Task.CompletedTask;
        }

        public Task ClearProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            _store.Remove(productId);
            return Task.CompletedTask;
        }

        public void Seed(params ProductPlanningIntentRecord[] intents)
        {
            foreach (var group in intents.GroupBy(static intent => intent.ProductId))
            {
                if (!_store.TryGetValue(group.Key, out var byEpic))
                {
                    byEpic = new Dictionary<int, ProductPlanningIntentRecord>();
                    _store[group.Key] = byEpic;
                }

                foreach (var intent in group)
                {
                    byEpic[intent.EpicId] = intent;
                }
            }
        }
    }

    internal sealed class RecordingTfsClient : ITfsClient
    {
        public List<(int WorkItemId, DateOnly StartDate, DateOnly TargetDate)> PlanningDateUpdates { get; } = [];

        public Task<bool> UpdateWorkItemPlanningDatesAsync(int workItemId, DateOnly startDate, DateOnly targetDate, CancellationToken cancellationToken = default)
        {
            PlanningDateUpdates.Add((workItemId, startDate, targetDate));
            return Task.FromResult(targetDate >= startDate);
        }

        public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetWorkItemsByTypeAsync(string workItemType, string areaPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(int[] rootWorkItemIds, DateTimeOffset? since = null, Action<int, int, string>? progressCallback = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(int[] rootWorkItemIds, DateTimeOffset? since = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(string? repositoryName = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(int pullRequestId, string repositoryName, int iterationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<int>> GetPullRequestWorkItemLinksAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(int workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkItemUpdate>> GetWorkItemUpdatesAsync(int workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemSeverityAsync(int workItemId, string severity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> UpdateWorkItemSeverityAndReturnAsync(int workItemId, string severity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemTagsAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> UpdateWorkItemTagsAndReturnAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemBacklogPriorityAsync(int workItemId, double priority, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemIterationPathAsync(int workItemId, string iterationPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> UpdateWorkItemTitleDescriptionAsync(int workItemId, string? title, string? description, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TfsVerificationReport> VerifyCapabilitiesAsync(bool includeWriteChecks = false, int? workItemIdForWriteCheck = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(IEnumerable<WorkItemEffortUpdate> updates, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BulkUpdateResult> UpdateWorkItemsStateAsync(IEnumerable<WorkItemStateUpdate> updates, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(IEnumerable<int> workItemIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemCreateResult> CreateWorkItemAsync(WorkItemCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateWorkItemParentAsync(int workItemId, int newParentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PipelineDto>> GetPipelinesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PipelineDto?> GetPipelineByIdAsync(int pipelineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(IEnumerable<int> pipelineIds, string? branchName = null, DateTimeOffset? minStartTime = null, int top = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetRepositoryIdByNameAsync(string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TestRunDto>> GetTestRunsByBuildIdsAsync(IEnumerable<int> buildIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<CoverageDto>> GetCoverageByBuildIdsAsync(IEnumerable<int> buildIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TfsProjectDto>> GetTfsProjectsAsync(string organizationUrl, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TfsTeamDto>> GetTfsTeamsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<(string Name, string Id)>> GetGitRepositoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(string projectName, string teamName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemTypeDefinitionDto>> GetWorkItemTypeDefinitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
