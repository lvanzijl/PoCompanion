using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Provides throttling for TFS/Azure DevOps API requests to prevent overwhelming the server.
/// Uses separate concurrency limits for read vs write operations.
/// </summary>
public sealed class TfsRequestThrottler : IDisposable
{
    private readonly SemaphoreSlim _readSemaphore;
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly ILogger<TfsRequestThrottler> _logger;
    private readonly RevisionIngestionDiagnostics? _diagnostics;

    // Default concurrency limits
    private const int DefaultReadConcurrency = 6;
    private const int DefaultWriteConcurrency = 2;

    public TfsRequestThrottler(
        ILogger<TfsRequestThrottler> logger,
        int readConcurrency = DefaultReadConcurrency,
        int writeConcurrency = DefaultWriteConcurrency,
        RevisionIngestionDiagnostics? diagnostics = null)
    {
        _logger = logger;
        _diagnostics = diagnostics;
        _readSemaphore = new SemaphoreSlim(readConcurrency, readConcurrency);
        _writeSemaphore = new SemaphoreSlim(writeConcurrency, writeConcurrency);
        ReadConcurrency = readConcurrency;
        WriteConcurrency = writeConcurrency;

        _logger.LogInformation(
            "TFS request throttler initialized with ReadConcurrency={ReadConcurrency}, WriteConcurrency={WriteConcurrency}",
            readConcurrency, writeConcurrency);
    }

    public int ReadConcurrency { get; }

    public int WriteConcurrency { get; }

    /// <summary>
    /// Executes a read operation with throttling applied.
    /// </summary>
    public async Task<T> ExecuteReadAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        await _readSemaphore.WaitAsync(cancellationToken);
        var waitTime = sw.Elapsed;

        if (waitTime.TotalMilliseconds > 100)
        {
            _logger.LogDebug("Read operation waited {WaitMs}ms for throttle", waitTime.TotalMilliseconds);
        }

        if (waitTime.TotalMilliseconds > 100 &&
            _diagnostics?.TryGetCurrentRun(out var runContext) == true)
        {
            _diagnostics.LogThrottleWait(runContext, "Read", (long)waitTime.TotalMilliseconds);
        }

        try
        {
            return await operation();
        }
        finally
        {
            _readSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes a write operation with throttling applied.
    /// </summary>
    public async Task<T> ExecuteWriteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        await _writeSemaphore.WaitAsync(cancellationToken);
        var waitTime = sw.Elapsed;

        if (waitTime.TotalMilliseconds > 100)
        {
            _logger.LogDebug("Write operation waited {WaitMs}ms for throttle", waitTime.TotalMilliseconds);
        }

        if (waitTime.TotalMilliseconds > 100 &&
            _diagnostics?.TryGetCurrentRun(out var runContext) == true)
        {
            _diagnostics.LogThrottleWait(runContext, "Write", (long)waitTime.TotalMilliseconds);
        }

        try
        {
            return await operation();
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes a write operation (non-generic).
    /// </summary>
    public async Task ExecuteWriteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _readSemaphore.Dispose();
        _writeSemaphore.Dispose();
    }
}
