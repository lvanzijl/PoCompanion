using System.Collections.Concurrent;
using PoTool.Core.Domain.Planning;

namespace PoTool.Core.Planning;

/// <summary>
/// Stores the current in-memory planning state for active product planning sessions.
/// </summary>
public interface IProductPlanningSessionStore
{
    bool TryGetState(int productId, out PlanningState state);

    void SetState(int productId, PlanningState state);

    void Reset(int productId);
}

/// <summary>
/// Process-local in-memory planning session store keyed by product identifier.
/// </summary>
public sealed class InMemoryProductPlanningSessionStore : IProductPlanningSessionStore
{
    private readonly ConcurrentDictionary<int, PlanningSessionEntry> _sessions = new();

    public bool TryGetState(int productId, out PlanningState state)
    {
        if (_sessions.TryGetValue(productId, out var session))
        {
            state = session.State;
            return true;
        }

        state = PlanningState.Empty;
        return false;
    }

    public void SetState(int productId, PlanningState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _sessions[productId] = new PlanningSessionEntry(productId, state, DateTimeOffset.UtcNow);
    }

    public void Reset(int productId)
    {
        _sessions.TryRemove(productId, out _);
    }

    private sealed record PlanningSessionEntry(
        int ProductId,
        PlanningState State,
        DateTimeOffset UpdatedAtUtc);
}
