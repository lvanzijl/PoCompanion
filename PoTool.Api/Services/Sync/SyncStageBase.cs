using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Base class for sync stages providing structured logging, retry with exponential backoff,
/// and circuit breaker pattern for resilience.
/// </summary>
public abstract class SyncStageBase : ISyncStage
{
    private readonly ILogger _logger;
    private readonly object _circuitBreakerLock = new();
    
    // Retry configuration: 3 total attempts (1 initial + 2 retries)
    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
    
    // Circuit breaker configuration
    private const int FailureThreshold = 5;
    private static readonly TimeSpan CircuitBreakerRecoveryTime = TimeSpan.FromSeconds(30);
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenedAt;

    protected SyncStageBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string StageName { get; }
    public abstract int StageNumber { get; }

    public async Task<SyncStageResult> ExecuteAsync(SyncContext context, Action<int> progressCallback, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogInformation("Starting sync stage {StageNumber}: {StageName} for ProductOwner: {ProductOwnerId}", 
            StageNumber, StageName, context.ProductOwnerId);

        // Check circuit breaker
        if (IsCircuitOpen())
        {
            _logger.LogWarning("Circuit breaker is open for stage {StageName}. Skipping execution.", StageName);
            return SyncStageResult.CreateFailure("Circuit breaker is open - too many recent failures");
        }

        SyncStageResult? result = null;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (attempt > 1)
                {
                    var delayIndex = Math.Min(attempt - 2, RetryDelays.Length - 1);
                    var delay = RetryDelays[delayIndex];
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxAttempts} for stage {StageName} after {Delay}s delay",
                        attempt, MaxAttempts, StageName, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

                result = await ExecuteCoreAsync(context, progressCallback, cancellationToken);
                
                if (result.Success)
                {
                    ResetCircuitBreaker();
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    _logger.LogInformation("Completed sync stage {StageNumber}: {StageName} in {ElapsedMs}ms. Items: {ItemCount}",
                        StageNumber, StageName, elapsed.TotalMilliseconds, result.ItemCount);
                    return result;
                }
                
                // Stage returned failure but no exception - don't retry
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync stage {StageName} was cancelled", StageName);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} failed for stage {StageName}: {Message}",
                    attempt, MaxAttempts, StageName, ex.Message);
            }
        }

        // All attempts exhausted
        RecordFailure();
        
        var errorMessage = lastException?.Message ?? result?.ErrorMessage ?? "Unknown error";
        _logger.LogError("Sync stage {StageName} failed after {MaxAttempts} attempts: {Error}",
            StageName, MaxAttempts, errorMessage);
        
        return SyncStageResult.CreateFailure(errorMessage);
    }

    /// <summary>
    /// Override this method to implement the actual sync logic.
    /// </summary>
    protected abstract Task<SyncStageResult> ExecuteCoreAsync(SyncContext context, Action<int> progressCallback, CancellationToken cancellationToken);

    private bool IsCircuitOpen()
    {
        lock (_circuitBreakerLock)
        {
            if (_circuitOpenedAt == null)
                return false;

            if (DateTimeOffset.UtcNow - _circuitOpenedAt.Value > CircuitBreakerRecoveryTime)
            {
                // Recovery time elapsed, close the circuit (half-open state - allow one try)
                _logger.LogInformation("Circuit breaker recovery time elapsed for stage {StageName}. Attempting recovery.", StageName);
                _circuitOpenedAt = null;
                _consecutiveFailures = 0;
                return false;
            }

            return true;
        }
    }

    private void RecordFailure()
    {
        lock (_circuitBreakerLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= FailureThreshold && _circuitOpenedAt == null)
            {
                _circuitOpenedAt = DateTimeOffset.UtcNow;
                _logger.LogWarning("Circuit breaker opened for stage {StageName} after {FailureCount} consecutive failures",
                    StageName, _consecutiveFailures);
            }
        }
    }

    private void ResetCircuitBreaker()
    {
        lock (_circuitBreakerLock)
        {
            if (_consecutiveFailures > 0)
            {
                _consecutiveFailures = 0;
                _circuitOpenedAt = null;
            }
        }
    }
}
