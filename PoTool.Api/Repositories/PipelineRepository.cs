using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

using PoTool.Core.Pipelines;

namespace PoTool.Api.Repositories;

/// <summary>
/// In-memory repository implementation for pipeline persistence.
/// Uses in-memory storage for V1 - pipeline data is exploratory and read-only.
/// Can be upgraded to database persistence in future versions if needed.
/// </summary>
public class PipelineRepository : IPipelineRepository
{
    private readonly object _lock = new();
    private List<PipelineDto> _pipelines = new();
    private List<PipelineRunDto> _runs = new();
    private DateTimeOffset? _lastSyncTime;

    public Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<PipelineDto>>(_pipelines.ToList());
        }
    }

    public Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var pipeline = _pipelines.FirstOrDefault(p => p.Id == pipelineId);
            return Task.FromResult(pipeline);
        }
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var runs = _runs
                .Where(r => r.PipelineId == pipelineId)
                .OrderByDescending(r => r.StartTime)
                .Take(top)
                .ToList();
            return Task.FromResult<IEnumerable<PipelineRunDto>>(runs);
        }
    }

    public Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<PipelineRunDto>>(_runs.ToList());
        }
    }

    public Task SaveAsync(PipelineSyncResult syncResult, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pipelines = syncResult.Pipelines.ToList();
            _runs = syncResult.Runs.ToList();
            _lastSyncTime = syncResult.SyncedAt;
            return Task.CompletedTask;
        }
    }

    public Task<DateTimeOffset?> GetLastSyncTimeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_lastSyncTime);
        }
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pipelines.Clear();
            _runs.Clear();
            _lastSyncTime = null;
            return Task.CompletedTask;
        }
    }
}
