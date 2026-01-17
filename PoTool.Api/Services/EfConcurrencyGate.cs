using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Scoped EF Core concurrency gate that serializes all EF operations within the same request/scope.
/// 
/// This prevents "A second operation was started on this context instance before a previous operation completed"
/// exceptions when services that use DbContext are called from parallel code paths (Task.WhenAll, throttlers, etc.).
/// 
/// Key characteristics:
/// - Scoped lifetime: One instance per HTTP request or service scope
/// - Serialization: Only within the same scope (no cross-request blocking)
/// - Thread-safe: Uses SemaphoreSlim internally
/// - Disposable: Properly cleans up resources
/// 
/// Performance impact:
/// - Minimal: Only serializes DB operations, network I/O remains parallel
/// - Typical wait time: 1-50ms per operation (DB is fast)
/// - No cross-request contention
/// </summary>
public sealed class EfConcurrencyGate : IEfConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<EfConcurrencyGate> _logger;
    private volatile bool _disposed;

    public EfConcurrencyGate(ILogger<EfConcurrencyGate> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes an operation with EF concurrency protection, returning a result.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EfConcurrencyGate));
        }

        var sw = Stopwatch.StartNew();
        await _semaphore.WaitAsync(cancellationToken);
        var waitTime = sw.Elapsed;

        // Log if operation had to wait (indicates concurrent access)
        if (waitTime.TotalMilliseconds > 10)
        {
            _logger.LogDebug("EF operation waited {WaitMs}ms for concurrency gate", waitTime.TotalMilliseconds);
        }

        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Executes an operation with EF concurrency protection, without returning a result.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EfConcurrencyGate));
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Releases resources used by the concurrency gate.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _semaphore.Dispose();
    }
}
