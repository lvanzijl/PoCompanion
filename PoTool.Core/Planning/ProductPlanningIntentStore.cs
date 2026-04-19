using PoTool.Shared.Planning;

namespace PoTool.Core.Planning;

public sealed record ProductPlanningIntentRecord(
    int ProductId,
    int EpicId,
    DateTime StartSprintStartDateUtc,
    int DurationInSprints,
    ProductPlanningRecoveryStatus? RecoveryStatus,
    DateTime UpdatedAtUtc);

public interface IProductPlanningIntentStore
{
    Task<IReadOnlyList<ProductPlanningIntentRecord>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);

    Task UpsertForProductAsync(int productId, IReadOnlyList<ProductPlanningIntentRecord> intents, CancellationToken cancellationToken = default);

    Task DeleteMissingEpicsAsync(int productId, IReadOnlyCollection<int> activeEpicIds, CancellationToken cancellationToken = default);

    Task ClearProductAsync(int productId, CancellationToken cancellationToken = default);
}
