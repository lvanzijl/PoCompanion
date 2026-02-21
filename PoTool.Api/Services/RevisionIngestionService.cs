using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Core.WorkItems;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Api.Services;

/// <summary>
/// Service for ingesting work item revisions from TFS.
/// Implements single-flight behavior to prevent concurrent ingestion for the same ProductOwner.
/// </summary>
public class RevisionIngestionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevisionIngestionService> _logger;
    private readonly RevisionIngestionDiagnostics _diagnostics;
    private readonly TfsRequestThrottler _throttler;
    private readonly IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> _persistenceOptions;
    private readonly IOptionsMonitor<RevisionIngestionPaginationOptions> _paginationOptions;
    private readonly IDataProtector _tokenProtector;

    private const string ContinuationTokenProtectorPurpose = "RevisionIngestionContinuationToken";
    private const int ContinuationTokenHashLength = 12;
    // Clamp backfill start dates to year 2000 to avoid pathological values from invalid data.
    private static readonly DateTimeOffset BackfillStartMinimumUtc =
        new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    private static readonly TimeSpan BackfillFallbackWindow = TimeSpan.FromDays(180);
    private static readonly TimeSpan InitialWindowDuration = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaximumWindowDuration = TimeSpan.FromDays(120);
    private static readonly TimeSpan PreferredMinimumWindowDuration = TimeSpan.FromDays(1);
    private static readonly TimeSpan MinimumWindowDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    private const int HeartbeatPageInterval = 100;
    private const int CappedWarningLimit = 10;

    private sealed record WorkItemDateSnapshot(
        int TfsId,
        int? ParentTfsId,
        DateTimeOffset? CreatedDate,
        DateTimeOffset? TfsChangedDate);

    // Concurrency control: one ingestion per ProductOwner
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _ingestionLocks = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeIngestions = new();

    public RevisionIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionIngestionService> logger,
        RevisionIngestionDiagnostics diagnostics,
        TfsRequestThrottler throttler,
        IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> persistenceOptions,
        IOptionsMonitor<RevisionIngestionPaginationOptions> paginationOptions,
        IDataProtectionProvider dataProtectionProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _diagnostics = diagnostics;
        _throttler = throttler;
        _persistenceOptions = persistenceOptions;
        _paginationOptions = paginationOptions;
        _tokenProtector = dataProtectionProvider.CreateProtector(ContinuationTokenProtectorPurpose);
    }

    /// <summary>
    /// Ingests work item revisions for a ProductOwner.
    /// Performs initial backfill if not done, otherwise incremental sync.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the ingestion operation.</returns>
    public async Task<RevisionIngestionResult> IngestRevisionsAsync(
        int productOwnerId,
        Action<RevisionIngestionProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _ingestionLocks.GetOrAdd(productOwnerId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock - if ingestion is already running, report and exit
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Revision ingestion already in progress for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.CompletedNormally,
                Success = false,
                IsAlreadyRunning = true,
                Message = "Revision ingestion already in progress"
            };
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeIngestions[productOwnerId] = cts;
        RevisionIngestionRunDiagnostics? runDiagnostics = null;
        var runSucceeded = false;
        string? failureStage = null;
        string? failureCause = null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            var revisionSource = scope.ServiceProvider.GetRequiredService<IWorkItemRevisionSource>();
            var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();
            var allowedWorkItemIds = await ResolveAllowedWorkItemIdsForProductOwnerAsync(
                context,
                tfsClient,
                productOwnerId,
                cts.Token);
            runDiagnostics = RevisionIngestionRunDiagnostics.Create(
                productOwnerId,
                allowedWorkItemIds,
                _paginationOptions.CurrentValue,
                CappedWarningLimit);
            var heartbeat = new IngestionHeartbeat(
                _logger,
                runDiagnostics.RunId,
                HeartbeatInterval,
                HeartbeatPageInterval);
            var diagnosticState = new RevisionIngestionDiagnosticState();
            var dbSnapshotStart = await CaptureRevisionDatabaseSnapshotAsync(context, cts.Token);
            LogDatabaseSnapshot("Start", dbSnapshotStart);

            // Get or create watermark record
            var watermark = await GetOrCreateWatermarkAsync(context, productOwnerId, cts.Token);

            _logger.LogInformation(
                "Starting revision ingestion for ProductOwner {ProductOwnerId}. IsBackfillComplete: {IsBackfillComplete}",
                productOwnerId, watermark.IsInitialBackfillComplete);

            // Update watermark to indicate ingestion started
            watermark.LastIngestionStartedAt = DateTimeOffset.UtcNow;
            watermark.LastErrorMessage = null;
            watermark.LastErrorAt = null;
            await context.SaveChangesAsync(cts.Token);

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = watermark.IsInitialBackfillComplete ? "Incremental Sync" : "Initial Backfill",
                PercentComplete = 0,
                RevisionsProcessed = 0
            });

            int totalRevisions = 0;
            var distinctWorkItemIds = new HashSet<int>();
            var distinctWorkItemCount = 0;
            DateTimeOffset? minChangedDate = null;
            DateTimeOffset? maxChangedDate = null;
            ReportingRevisionsTermination? termination = null;
            var runOutcome = RevisionIngestionRunOutcome.CompletedNormally;
            var paginationOptions = _paginationOptions.CurrentValue;
            // For incremental sync, use durable checkpoint when retry mode is enabled.
            // For backfill with no last sync time, infer from cached work items.
            DateTimeOffset? startDateTime = watermark.IsInitialBackfillComplete
                ? paginationOptions.RetryEnabled
                    ? watermark.LastStableChangedDateUtc ?? watermark.LastSyncStartDateTime
                    : watermark.LastSyncStartDateTime
                : await InferBackfillStartDateTimeAsync(context, productOwnerId, cts.Token);
            var fallbackUsed = false;
            string? lastStableContinuationTokenHash = null;
            WindowStallReason? lastStallReason = null;
            DateTimeOffset? durableCheckpoint = watermark.LastStableChangedDateUtc?.ToUniversalTime();
            DateTimeOffset? bestObservedMaxChanged = durableCheckpoint;
            var retryFinalOutcome = "NotRun";
            var retryIterationsExecuted = 0;
            DateTimeOffset? retryCursor = null;

            var runStartUtc = DateTimeOffset.UtcNow;
            using var runScope = _diagnostics.StartRun(
                productOwnerId,
                isBackfill: !watermark.IsInitialBackfillComplete,
                startDateTime,
                runStartUtc,
                _throttler.ReadConcurrency,
                _throttler.WriteConcurrency,
                hydrationConcurrency: 0,
                out var runContext);

            if (runContext.IsEnabled)
            {
                var dbProvider = context.Database.ProviderName ?? "Unknown";
                var connectionMode = GetConnectionMode(context);
                _diagnostics.LogRunDatabase(runContext, dbProvider, connectionMode);
            }

            // Record sync start time for next incremental sync
            var syncStartTime = DateTimeOffset.UtcNow;

            var normalizedStartDateTime = startDateTime ?? GetFallbackBackfillStartDate(
                productOwnerId,
                "No cached backfill start date available; using fallback window.");
            var backfillStartUtc = ClampBackfillStartDate(normalizedStartDateTime, productOwnerId, "BackfillStart");
            var backfillEndUtc = DateTimeOffset.UtcNow;
            if (backfillEndUtc <= backfillStartUtc)
            {
                backfillEndUtc = backfillStartUtc.AddMinutes(1);
            }

            var windowRunResult = await ProcessWindowsAsync(
                context,
                revisionSource,
                allowedWorkItemIds,
                watermark,
                backfillStartUtc,
                backfillEndUtc,
                paginationOptions,
                runContext,
                runDiagnostics,
                heartbeat,
                diagnosticState,
                progressCallback,
                cts.Token);

            totalRevisions = windowRunResult.TotalPersisted;
            termination = windowRunResult.LastTermination;
            runOutcome = windowRunResult.RunOutcome;
            distinctWorkItemIds = new HashSet<int>(windowRunResult.DistinctWorkItemIds);
            distinctWorkItemCount = distinctWorkItemIds.Count;
            minChangedDate = windowRunResult.MinChangedDate;
            maxChangedDate = windowRunResult.MaxChangedDate;
            lastStableContinuationTokenHash = windowRunResult.LastStableContinuationTokenHash;
            lastStallReason = windowRunResult.LastStallReason;

            FallbackIngestionResult? fallbackResult = null;
            if (ShouldActivateFallback(paginationOptions, windowRunResult))
            {
                _logger.LogWarning(
                    "Reporting revisions pagination anomaly detected; activating fallback per-work-item retrieval. ProductOwnerId={ProductOwnerId} TerminationReason={TerminationReason} WindowsProcessed={WindowsProcessed}",
                    productOwnerId,
                    windowRunResult.LastTermination?.Reason,
                    windowRunResult.WindowsProcessed);

                fallbackResult = await RunFallbackIngestionAsync(
                    context,
                    revisionSource,
                    allowedWorkItemIds,
                    watermark,
                    runContext,
                    paginationOptions.FallbackBatchSize,
                    cts.Token);

                fallbackUsed = fallbackResult.Success;
                if (fallbackResult.Success)
                {
                    totalRevisions += fallbackResult.PersistedCount;
                    distinctWorkItemIds.UnionWith(fallbackResult.DistinctWorkItemIds);
                    distinctWorkItemCount = distinctWorkItemIds.Count;
                    minChangedDate = GetWindowRawMin(minChangedDate, fallbackResult.MinChangedDate);
                    maxChangedDate = GetWindowRawMax(maxChangedDate, fallbackResult.MaxChangedDate);
                    runOutcome = RevisionIngestionRunOutcome.CompletedWithFallback;
                }
                else
                {
                    runOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly;
                }
            }

            if (termination == null &&
                runOutcome == RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly &&
                lastStallReason.HasValue)
            {
                termination = CreateTerminationFromStall(lastStallReason.Value);
            }

            if (paginationOptions.RetryEnabled &&
                runOutcome == RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly)
            {
                var overlap = TimeSpan.FromMinutes(Math.Max(0, paginationOptions.RetryOverlapMinutes));
                var epsilon = TimeSpan.FromSeconds(Math.Max(0, paginationOptions.ProgressEpsilonSeconds));
                var observedMaxChanged = await GetScopedMaxChangedDateAsync(context, allowedWorkItemIds, cts.Token);
                if (observedMaxChanged.HasValue &&
                    (!bestObservedMaxChanged.HasValue || observedMaxChanged > bestObservedMaxChanged))
                {
                    bestObservedMaxChanged = observedMaxChanged.Value;
                }
                retryCursor = (durableCheckpoint ?? startDateTime ?? backfillStartUtc).ToUniversalTime().Subtract(overlap);

                // Durable checkpoint is protected and only updated after a fully consistent run.
                // Retry overlap is required because continuation-token anomalies can leave a short hidden gap near boundaries.
                // Monotonic progress (bestObservedMaxChanged + epsilon) prevents endless retries on repeated pages/tokens.
                for (var iteration = 1; iteration <= Math.Max(1, paginationOptions.RetryMaxIterations); iteration++)
                {
                    retryIterationsExecuted = iteration;
                    _logger.LogWarning(
                        "Revision ingestion retry iteration started. ProductOwnerId={ProductOwnerId} Iteration={Iteration} DurableCheckpoint={DurableCheckpoint} RetryCursor={RetryCursor} BestObservedMaxChanged={BestObservedMaxChanged} Overlap={Overlap}",
                        productOwnerId,
                        iteration,
                        durableCheckpoint,
                        retryCursor,
                        bestObservedMaxChanged,
                        overlap);

                    var retryResult = await ProcessWindowsAsync(
                        context,
                        revisionSource,
                        allowedWorkItemIds,
                        watermark,
                        retryCursor ?? backfillStartUtc,
                        DateTimeOffset.UtcNow,
                        paginationOptions,
                        runContext,
                        runDiagnostics,
                        heartbeat,
                        diagnosticState,
                        progressCallback,
                        cts.Token);

                    totalRevisions += retryResult.TotalPersisted;
                    distinctWorkItemIds.UnionWith(retryResult.DistinctWorkItemIds);
                    distinctWorkItemCount = distinctWorkItemIds.Count;
                    minChangedDate = GetWindowRawMin(minChangedDate, retryResult.MinChangedDate);
                    maxChangedDate = GetWindowRawMax(maxChangedDate, retryResult.MaxChangedDate);
                    runOutcome = retryResult.RunOutcome;
                    termination = retryResult.LastTermination;
                    lastStallReason = retryResult.LastStallReason;
                    lastStableContinuationTokenHash = retryResult.LastStableContinuationTokenHash ?? lastStableContinuationTokenHash;

                    var newMaxChangedInDb = await GetScopedMaxChangedDateAsync(context, allowedWorkItemIds, cts.Token);
                    var retryProgressDetected = newMaxChangedInDb.HasValue &&
                                               (!bestObservedMaxChanged.HasValue || newMaxChangedInDb > bestObservedMaxChanged.Value.Add(epsilon));

                    _logger.LogWarning(
                        "Revision ingestion retry iteration completed. ProductOwnerId={ProductOwnerId} Iteration={Iteration} RetryCursor={RetryCursor} BestObservedMaxChanged={BestObservedMaxChanged} NewMaxChangedInDb={NewMaxChangedInDb} ProgressDetected={ProgressDetected} Outcome={Outcome}",
                        productOwnerId,
                        iteration,
                        retryCursor,
                        bestObservedMaxChanged,
                        newMaxChangedInDb,
                        retryProgressDetected,
                        runOutcome);

                    if (runOutcome == RevisionIngestionRunOutcome.CompletedNormally &&
                        await ValidateRetryConsistencyAsync(scope.ServiceProvider, context, allowedWorkItemIds, cts.Token))
                    {
                        if (newMaxChangedInDb.HasValue)
                        {
                            bestObservedMaxChanged = newMaxChangedInDb.Value;
                        }

                        retryFinalOutcome = "Success";
                        break;
                    }

                    if (!retryProgressDetected)
                    {
                        retryFinalOutcome = "FailedWithNoProgress";
                        runOutcome = RevisionIngestionRunOutcome.Failed;
                        break;
                    }

                    bestObservedMaxChanged = newMaxChangedInDb;
                    retryCursor = bestObservedMaxChanged!.Value.Subtract(overlap);
                    retryFinalOutcome = "FailedAfterMaxIterations";
                }

                if (retryFinalOutcome != "Success")
                {
                    runOutcome = RevisionIngestionRunOutcome.Failed;
                }

                _logger.LogWarning(
                    "Revision ingestion retry loop completed. ProductOwnerId={ProductOwnerId} Outcome={FinalOutcome} Iterations={Iterations} DurableCheckpoint={DurableCheckpoint} RetryCursor={RetryCursor} BestObservedMaxChanged={BestObservedMaxChanged} OverlapMinutes={OverlapMinutes}",
                    productOwnerId,
                    retryFinalOutcome,
                    retryIterationsExecuted,
                    durableCheckpoint,
                    retryCursor,
                    bestObservedMaxChanged,
                    paginationOptions.RetryOverlapMinutes);
            }

            var paginationWarning = runOutcome == RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly;
            var hasProgress = totalRevisions > 0 || fallbackUsed;
            var successWithWarnings = paginationWarning && hasProgress;
            var warningMessage = paginationWarning
                ? termination != null
                    ? $"Reporting revisions pagination ended early ({termination.Reason}): {termination.Message}"
                    : lastStallReason.HasValue
                        ? $"Reporting revisions pagination stalled ({lastStallReason})"
                        : "Reporting revisions pagination anomaly detected."
                : null;

            // Mark ingestion complete
            context.ChangeTracker.Clear();
            context.RevisionIngestionWatermarks.Attach(watermark);
            watermark.LastIngestionCompletedAt = DateTimeOffset.UtcNow;
            watermark.LastIngestionRevisionCount = totalRevisions;
            watermark.LastRunOutcome = runOutcome.ToString();
            watermark.LastStableContinuationTokenHash = lastStableContinuationTokenHash;
            watermark.FallbackUsedLastRun = fallbackUsed;
            watermark.ContinuationToken = null; // Windowed ingestion does not reuse tokens across runs

            var fullSuccess = runOutcome == RevisionIngestionRunOutcome.CompletedNormally;
            var canAdvanceDurableCheckpoint = !paginationOptions.RetryEnabled
                ? fullSuccess || runOutcome == RevisionIngestionRunOutcome.CompletedWithFallback || successWithWarnings
                : fullSuccess;
            if (canAdvanceDurableCheckpoint)
            {
                var durableCheckpointCandidate = paginationOptions.RetryEnabled
                    ? bestObservedMaxChanged ?? maxChangedDate
                    : maxChangedDate;
                watermark.LastStableChangedDateUtc = durableCheckpointCandidate?.ToUniversalTime();
            }

            if (termination != null)
            {
                watermark.LastErrorMessage = $"Reporting revisions pagination ended early ({termination.Reason}): {termination.Message}";
                watermark.LastErrorAt = DateTimeOffset.UtcNow;
            }
            else if (runOutcome == RevisionIngestionRunOutcome.CompletedWithFallback)
            {
                watermark.LastErrorMessage = "Reporting revisions pagination anomaly recovered via fallback ingestion.";
                watermark.LastErrorAt = DateTimeOffset.UtcNow;
            }
            else if (warningMessage != null)
            {
                watermark.LastErrorMessage = warningMessage;
                watermark.LastErrorAt = DateTimeOffset.UtcNow;
            }
            else if (runOutcome == RevisionIngestionRunOutcome.Failed &&
                     paginationOptions.RetryEnabled &&
                     retryFinalOutcome != "NotRun")
            {
                watermark.LastErrorMessage = $"Incremental retry loop failed ({retryFinalOutcome}).";
                watermark.LastErrorAt = DateTimeOffset.UtcNow;
            }

            if (windowRunResult.BackfillComplete && runOutcome == RevisionIngestionRunOutcome.CompletedNormally)
            {
                watermark.IsInitialBackfillComplete = true;
                _logger.LogInformation(
                    "Initial backfill completed for ProductOwner {ProductOwnerId}. WindowsProcessed={WindowsProcessed} WindowsMarkedUnretrievable={WindowsMarkedUnretrievable}",
                    productOwnerId,
                    windowRunResult.WindowsProcessed,
                    windowRunResult.WindowsMarkedUnretrievable);
            }

            if (canAdvanceDurableCheckpoint)
            {
                // Durable checkpoint doubles as the next incremental sync start.
                watermark.LastSyncStartDateTime = watermark.LastStableChangedDateUtc ?? syncStartTime;
            }

            context.Entry(watermark).State = EntityState.Modified;
            await context.SaveChangesAsync(cts.Token);
            context.ChangeTracker.Clear();

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = "Complete",
                PercentComplete = 100,
                RevisionsProcessed = totalRevisions,
                IsComplete = true
            });

            _logger.LogInformation(
                "Revision ingestion completed for ProductOwner {ProductOwnerId}: {TotalRevisions} revisions. Outcome={Outcome} WindowsProcessed={WindowsProcessed} WindowsMarkedUnretrievable={WindowsMarkedUnretrievable} BackfillComplete={BackfillComplete}",
                productOwnerId,
                totalRevisions,
                runOutcome,
                windowRunResult.WindowsProcessed,
                windowRunResult.WindowsMarkedUnretrievable,
                windowRunResult.BackfillComplete);

            LogCreationSnapshotSummary(diagnosticState, allowedWorkItemIds);
            var dbSnapshotEnd = await CaptureRevisionDatabaseSnapshotAsync(context, cts.Token);
            LogDatabaseSnapshot("End", dbSnapshotEnd);

            _logger.LogInformation(
                "Revision ingestion run outcome. ProductOwnerId={ProductOwnerId} Outcome={Outcome} TotalPersisted={TotalPersisted} DistinctWorkItems={DistinctWorkItems} MinChangedDate={MinChangedDate} MaxChangedDate={MaxChangedDate} FallbackUsed={FallbackUsed}",
                productOwnerId,
                runOutcome,
                totalRevisions,
                distinctWorkItemCount,
                minChangedDate,
                maxChangedDate,
                fallbackUsed);

            var success = runOutcome is RevisionIngestionRunOutcome.CompletedNormally or RevisionIngestionRunOutcome.CompletedWithFallback || successWithWarnings;
            var errorMessage = success
                ? null
                : runOutcome == RevisionIngestionRunOutcome.Failed &&
                  paginationOptions.RetryEnabled &&
                  retryFinalOutcome != "NotRun"
                    ? $"Incremental retry loop failed ({retryFinalOutcome})."
                : termination != null
                    ? $"Reporting revisions pagination ended early ({termination.Reason}): {termination.Message}"
                    : lastStallReason.HasValue
                        ? $"Reporting revisions pagination stalled ({lastStallReason})"
                        : "Reporting revisions pagination anomaly detected.";
            var finalMessage = successWithWarnings
                ? warningMessage ?? $"Ingestion completed with warnings after {totalRevisions} revisions"
                : success
                    ? runOutcome == RevisionIngestionRunOutcome.CompletedWithFallback
                        ? $"Reporting revisions fallback used; ingested {totalRevisions} revisions"
                        : $"Successfully ingested {totalRevisions} revisions"
                    : termination != null
                        ? $"Ingestion terminated early after {totalRevisions} revisions: {termination.Message}"
                        : errorMessage ?? $"Ingestion ended with outcome {runOutcome} after {totalRevisions} revisions";
            runSucceeded = success;
            if (!success)
            {
                failureStage = "RunOutcome";
                failureCause = errorMessage ?? finalMessage;
            }

            return new RevisionIngestionResult
            {
                RunOutcome = runOutcome,
                Success = success,
                HasWarnings = successWithWarnings,
                ErrorMessage = errorMessage,
                WarningMessage = warningMessage,
                FallbackUsed = fallbackUsed,
                RevisionsIngested = totalRevisions,
                DistinctWorkItemsIngested = distinctWorkItemCount,
                PagesProcessed = windowRunResult.PagesProcessed,
                WasTerminatedEarly = runOutcome != RevisionIngestionRunOutcome.CompletedNormally,
                TerminationReason = termination?.Reason,
                TerminationMessage = termination?.Message,
                MinChangedDateIngested = minChangedDate,
                MaxChangedDateIngested = maxChangedDate,
                Message = finalMessage,
                BackfillComplete = windowRunResult.BackfillComplete && runOutcome == RevisionIngestionRunOutcome.CompletedNormally
            };
        }
        catch (OperationCanceledException)
        {
            failureStage = "Cancelled";
            failureCause = "Operation cancelled.";
            _logger.LogInformation("Revision ingestion cancelled for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly,
                Success = false,
                WasCancelled = true,
                Message = "Ingestion was cancelled"
            };
        }
        catch (Exception ex)
        {
            failureStage = "UnhandledException";
            failureCause = ex.Message;
            _logger.LogError(ex, "Revision ingestion failed for ProductOwner {ProductOwnerId}", productOwnerId);

            // Record error in watermark
            try
            {
                using var errorScope = _scopeFactory.CreateScope();
                var errorContext = errorScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var watermark = await errorContext.RevisionIngestionWatermarks
                    .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, CancellationToken.None);

                if (watermark != null)
                {
                    watermark.LastErrorMessage = ex.Message;
                    watermark.LastErrorAt = DateTimeOffset.UtcNow;
                    await errorContext.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception recordEx)
            {
                _logger.LogWarning(recordEx, "Failed to record ingestion error for ProductOwner {ProductOwnerId}", productOwnerId);
            }

            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.Failed,
                Success = false,
                ErrorMessage = ex.Message,
                Message = $"Ingestion failed: {ex.Message}"
            };
        }
        finally
        {
            runDiagnostics?.LogRunEnd(_logger, runSucceeded, failureStage, failureCause);
            _activeIngestions.TryRemove(productOwnerId, out _);
            semaphore.Release();
        }
    }

    /// <summary>
    /// Cancels an in-progress ingestion.
    /// </summary>
    public void CancelIngestion(int productOwnerId)
    {
        if (_activeIngestions.TryGetValue(productOwnerId, out var cts))
        {
            _logger.LogInformation("Cancelling revision ingestion for ProductOwner {ProductOwnerId}", productOwnerId);
            cts.Cancel();
        }
    }

    /// <summary>
    /// Checks if ingestion is currently running.
    /// </summary>
    public bool IsIngestionRunning(int productOwnerId)
    {
        return _activeIngestions.ContainsKey(productOwnerId);
    }

    /// <summary>
    /// Infers the backfill start date from the earliest cached work item for a product owner.
    /// Returns null if no cached work items exist (will scan all history).
    /// Subtracts a 1-day buffer to ensure we don't miss revisions.
    /// </summary>
    private async Task<DateTimeOffset?> InferBackfillStartDateTimeAsync(
        PoToolDbContext context,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        // Get the product owner and their product roots
        var productOwner = await context.Profiles
            .Include(profile => profile.Products)
            .FirstOrDefaultAsync(profile => profile.Id == productOwnerId, cancellationToken);

        if (productOwner == null || productOwner.Products.Count == 0)
        {
            _logger.LogDebug("No products found for ProductOwner {ProductOwnerId}, backfill will scan all history", productOwnerId);
            return null;
        }

        var rootWorkItemIds = productOwner.Products
            .Select(product => product.BacklogRootWorkItemId)
            .Where(rootId => rootId > 0)
            .Distinct()
            .ToList();

        if (rootWorkItemIds.Count == 0)
        {
            _logger.LogDebug("No valid backlog root IDs for ProductOwner {ProductOwnerId}, backfill will scan all history", productOwnerId);
            return null;
        }

        // Query cached work items and find the earliest date on the client side
        // Note: SQLite doesn't support Min/OrderBy on DateTimeOffset, so we need client-side evaluation
        // To minimize memory usage, we select only the date fields and use ToListAsync with a small projection
        var workItemDates = await context.WorkItems
            .Select(wi => new WorkItemDateSnapshot(wi.TfsId, wi.ParentTfsId, wi.CreatedDate, wi.TfsChangedDate))
            .ToListAsync(cancellationToken);

        if (workItemDates.Count == 0)
        {
            _logger.LogWarning(
                "No cached work items available for ProductOwner {ProductOwnerId}, backfill will scan all history",
                productOwnerId);
            return null;
        }

        var scopedWorkItems = FilterWorkItemsToScope(workItemDates, rootWorkItemIds);

        if (scopedWorkItems.Count == 0)
        {
            _logger.LogWarning(
                "No cached work items found within product root hierarchy for ProductOwner {ProductOwnerId}, backfill will scan all history",
                productOwnerId);
            return null;
        }

        // Find the earliest CreatedDate on the client side
        var earliestCreatedDate = scopedWorkItems
            .Where(wi => wi.CreatedDate.HasValue)
            .Select(wi => wi.CreatedDate!.Value.ToUniversalTime())
            .OrderBy(d => d)
            .FirstOrDefault();

        if (earliestCreatedDate != default(DateTimeOffset))
        {
            // Subtract 1 day buffer to ensure we don't miss revisions
            var inferredStartDate = ClampBackfillStartDate(earliestCreatedDate.AddDays(-1), productOwnerId, "CreatedDate");
            _logger.LogInformation(
                "Inferred backfill start date from earliest CreatedDate for ProductOwner {ProductOwnerId}: {StartDate} (1 day before {EarliestDate})",
                productOwnerId,
                inferredStartDate,
                earliestCreatedDate);
            return inferredStartDate;
        }

        // Fallback to TfsChangedDate if CreatedDate is not available
        var earliestChangedDate = scopedWorkItems
            .Where(wi => wi.TfsChangedDate.HasValue)
            .Select(wi => wi.TfsChangedDate!.Value.ToUniversalTime())
            .OrderBy(d => d)
            .FirstOrDefault();

        if (earliestChangedDate != default(DateTimeOffset))
        {
            // Subtract 1 day buffer to ensure we don't miss revisions
            var inferredStart = ClampBackfillStartDate(earliestChangedDate.AddDays(-1), productOwnerId, "TfsChangedDate");
            _logger.LogInformation(
                "Inferred backfill start date from earliest TfsChangedDate for ProductOwner {ProductOwnerId}: {StartDate} (1 day before {EarliestDate})",
                productOwnerId,
                inferredStart,
                earliestChangedDate);
            return inferredStart;
        }

        _logger.LogWarning(
            "No valid CreatedDate or TfsChangedDate values found in scoped work items for ProductOwner {ProductOwnerId}, backfill will scan all history",
            productOwnerId);
        return null;
    }

    private static IReadOnlyList<WorkItemDateSnapshot> FilterWorkItemsToScope(
        IReadOnlyList<WorkItemDateSnapshot> workItems,
        IReadOnlyCollection<int> rootWorkItemIds)
    {
        var childrenLookup = workItems
            .Where(wi => wi.ParentTfsId.HasValue)
            .GroupBy(wi => wi.ParentTfsId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var scopedIds = new HashSet<int>(rootWorkItemIds);
        var queue = new Queue<int>(rootWorkItemIds);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenLookup.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (scopedIds.Add(child.TfsId))
                {
                    queue.Enqueue(child.TfsId);
                }
            }
        }

        return workItems
            .Where(item => scopedIds.Contains(item.TfsId))
            .ToList();
    }

    private static async Task<DateTimeOffset?> GetScopedMaxChangedDateAsync(
        PoToolDbContext context,
        IReadOnlyCollection<int> allowedWorkItemIds,
        CancellationToken cancellationToken)
    {
        if (allowedWorkItemIds.Count == 0)
        {
            return null;
        }

        var maxChangedDate = await context.RevisionHeaders
            .AsNoTracking()
            .Where(header => allowedWorkItemIds.Contains(header.WorkItemId))
            .Select(header => (DateTimeOffset?)header.ChangedDate)
            .MaxAsync(cancellationToken);

        return maxChangedDate?.ToUniversalTime();
    }

    private static async Task<bool> ValidateRetryConsistencyAsync(
        IServiceProvider serviceProvider,
        PoToolDbContext context,
        IReadOnlyCollection<int> allowedWorkItemIds,
        CancellationToken cancellationToken)
    {
        if (allowedWorkItemIds.Count == 0)
        {
            return true;
        }

        var candidateWorkItemIds = await context.RevisionHeaders
            .AsNoTracking()
            .Where(header => allowedWorkItemIds.Contains(header.WorkItemId))
            .OrderByDescending(header => header.ChangedDate)
            .Select(header => header.WorkItemId)
            .Distinct()
            .Take(5)
            .ToListAsync(cancellationToken);

        if (candidateWorkItemIds.Count == 0)
        {
            return true;
        }

        var existingWorkItemIds = await context.WorkItems
            .AsNoTracking()
            .Where(workItem => candidateWorkItemIds.Contains(workItem.TfsId))
            .Select(workItem => workItem.TfsId)
            .ToListAsync(cancellationToken);

        if (existingWorkItemIds.Count == 0)
        {
            return true;
        }

        var cacheManagementService = serviceProvider.GetService<CacheManagementService>();
        if (cacheManagementService == null)
        {
            return true;
        }
        foreach (var workItemId in existingWorkItemIds)
        {
            var validationResult = await cacheManagementService.ValidateSingleWorkItemAsync(
                workItemId,
                includeTimeline: false,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private DateTimeOffset GetFallbackBackfillStartDate(int productOwnerId, string reason)
    {
        var fallback = DateTimeOffset.UtcNow.Subtract(BackfillFallbackWindow);
        var normalizedFallback = ClampBackfillStartDate(fallback, productOwnerId, "Fallback");
        _logger.LogWarning(
            "Using fallback backfill start date for ProductOwner {ProductOwnerId}: {StartDate}. Reason: {Reason}",
            productOwnerId,
            normalizedFallback,
            reason);
        return normalizedFallback;
    }

    private DateTimeOffset ClampBackfillStartDate(DateTimeOffset dateTime, int productOwnerId, string source)
    {
        var utc = dateTime.ToUniversalTime();
        if (utc < BackfillStartMinimumUtc)
        {
            _logger.LogWarning(
                "Inferred backfill start date {StartDate} from {Source} for ProductOwner {ProductOwnerId} is earlier than minimum {Minimum}. Clamping.",
                utc,
                source,
                productOwnerId,
                BackfillStartMinimumUtc);
            return BackfillStartMinimumUtc;
        }

        return utc;
    }

    private static bool ShouldActivateFallback(
        RevisionIngestionPaginationOptions paginationOptions,
        WindowRunResult windowRunResult)
    {
        return paginationOptions.AnomalyPolicy == PaginationAnomalyPolicy.Fallback &&
               (windowRunResult.RunOutcome == RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly ||
                windowRunResult.WindowsMarkedUnretrievable > 0 ||
                windowRunResult.LastTermination != null);
    }

    private async Task<FallbackIngestionResult> RunFallbackIngestionAsync(
        PoToolDbContext context,
        IWorkItemRevisionSource revisionSource,
        HashSet<int> allowedWorkItemIds,
        RevisionIngestionWatermarkEntity watermark,
        RevisionIngestionRunContext runContext,
        int batchSize,
        CancellationToken cancellationToken)
    {
        _ = runContext;
        var orderedWorkItemIds = allowedWorkItemIds.OrderBy(id => id).ToList();
        var startIndex = Math.Clamp(watermark.FallbackResumeIndex ?? 0, 0, Math.Max(0, orderedWorkItemIds.Count - 1));
        var persisted = 0;
        var distinctIds = new HashSet<int>();
        DateTimeOffset? minChangedDate = null;
        DateTimeOffset? maxChangedDate = null;

        _logger.LogInformation(
            "Starting fallback per-work-item ingestion. TotalWorkItems={TotalWorkItems} StartIndex={StartIndex}",
            orderedWorkItemIds.Count,
            startIndex);

        context.RevisionIngestionWatermarks.Attach(watermark);

        for (var index = startIndex; index < orderedWorkItemIds.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workItemId = orderedWorkItemIds[index];

            watermark.FallbackResumeIndex = index;
            await context.SaveChangesAsync(cancellationToken);

            var revisions = await _throttler.ExecuteReadAsync(
                () => revisionSource.GetWorkItemRevisionsAsync(workItemId, cancellationToken),
                cancellationToken);

            if (revisions.Count > 0)
            {
                var persistedCount = await PersistRevisionsAsync(context, revisions, null, cancellationToken);
                persisted += persistedCount;
                if (persistedCount > 0)
                {
                    distinctIds.Add(workItemId);
                    var localMin = revisions.Min(r => r.ChangedDate).ToUniversalTime();
                    var localMax = revisions.Max(r => r.ChangedDate).ToUniversalTime();
                    minChangedDate = GetWindowRawMin(minChangedDate, localMin);
                    maxChangedDate = GetWindowRawMax(maxChangedDate, localMax);
                }
            }

            if ((index + 1) % Math.Max(1, batchSize) == 0)
            {
                context.ChangeTracker.Clear();
                context.RevisionIngestionWatermarks.Attach(watermark);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Fallback ingestion progress. Index={Index}/{Total} WorkItemId={WorkItemId} TotalPersisted={TotalPersisted}",
                    index + 1,
                    orderedWorkItemIds.Count,
                    workItemId,
                    persisted);
            }
        }

        watermark.FallbackResumeIndex = null;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Fallback ingestion completed. Persisted={Persisted} DistinctWorkItems={DistinctWorkItems} MinChangedDate={MinChangedDate} MaxChangedDate={MaxChangedDate}",
            persisted,
            distinctIds.Count,
            minChangedDate,
            maxChangedDate);

        return new FallbackIngestionResult(
            Success: true,
            PersistedCount: persisted,
            DistinctWorkItemCount: distinctIds.Count,
            DistinctWorkItemIds: distinctIds.ToArray(),
            MinChangedDate: minChangedDate,
            MaxChangedDate: maxChangedDate);
    }

    private async Task<WindowRunResult> ProcessWindowsAsync(
        PoToolDbContext context,
        IWorkItemRevisionSource revisionSource,
        HashSet<int> allowedWorkItemIds,
        RevisionIngestionWatermarkEntity watermark,
        DateTimeOffset backfillStartUtc,
        DateTimeOffset backfillEndUtc,
        RevisionIngestionPaginationOptions paginationOptions,
        RevisionIngestionRunContext runContext,
        RevisionIngestionRunDiagnostics runDiagnostics,
        IngestionHeartbeat heartbeat,
        RevisionIngestionDiagnosticState diagnosticState,
        Action<RevisionIngestionProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var totalPersisted = 0;
        var scopedFieldDumpLogged = false;
        var windowsProcessed = 0;
        var windowsMarkedUnretrievable = 0;
        var pagesProcessed = 0;
        var lastTermination = (ReportingRevisionsTermination?)null;
        var windowSequence = 0;
        var distinctWorkItemIds = new HashSet<int>();
        DateTimeOffset? minChangedDate = null;
        DateTimeOffset? maxChangedDate = null;
        string? lastStableContinuationTokenHash = null;
        WindowStallReason? lastStallReason = null;
        var nextWindowStart = backfillStartUtc;
        var currentWindowDuration = InitialWindowDuration;
        var windowQueue = new Queue<RevisionIngestionWindow>();

        void EnqueueNextBaseWindow()
        {
            if (nextWindowStart >= backfillEndUtc)
            {
                return;
            }

            var nextWindowEnd = nextWindowStart + currentWindowDuration;
            if (nextWindowEnd > backfillEndUtc)
            {
                nextWindowEnd = backfillEndUtc;
            }

            windowQueue.Enqueue(new RevisionIngestionWindow(
                nextWindowStart,
                nextWindowEnd,
                Depth: 0,
                Sequence: Interlocked.Increment(ref windowSequence)));
            nextWindowStart = nextWindowEnd;
        }

        EnqueueNextBaseWindow();

        while (windowQueue.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var window = windowQueue.Dequeue();
            var windowResult = await ProcessSingleWindowAsync(
                window,
                context,
                watermark,
                revisionSource,
                allowedWorkItemIds,
                paginationOptions,
                runContext,
                runDiagnostics,
                heartbeat,
                diagnosticState,
                progressCallback,
                totalPersisted,
                scopedFieldDumpLogged,
                cancellationToken);

            scopedFieldDumpLogged = windowResult.ScopedFieldDumpLogged;
            windowsProcessed++;
            pagesProcessed += windowResult.PagesProcessed;
            totalPersisted += windowResult.PersistedCount;
            lastTermination ??= windowResult.Termination;
            if (windowResult.MinChangedDate.HasValue)
            {
                minChangedDate = minChangedDate == null || windowResult.MinChangedDate < minChangedDate
                    ? windowResult.MinChangedDate
                    : minChangedDate;
            }

            if (windowResult.MaxChangedDate.HasValue)
            {
                maxChangedDate = maxChangedDate == null || windowResult.MaxChangedDate > maxChangedDate
                    ? windowResult.MaxChangedDate
                    : maxChangedDate;
            }

            if (windowResult.PersistedWorkItemIds.Count > 0)
            {
                distinctWorkItemIds.UnionWith(windowResult.PersistedWorkItemIds);
            }

            lastStableContinuationTokenHash = windowResult.LastTokenHash ?? lastStableContinuationTokenHash;

            if (windowResult.Outcome == WindowProcessingOutcome.Stalled)
            {
                if (paginationOptions.AnomalyPolicy == PaginationAnomalyPolicy.Fallback)
                {
                    break;
                }
                windowResult = windowResult with { Outcome = WindowProcessingOutcome.MarkedUnretrievableAtMinimumChunk };
                lastStallReason ??= windowResult.StallReason;
            }

            if (windowResult.Outcome == WindowProcessingOutcome.MarkedUnretrievableAtMinimumChunk)
            {
                windowsMarkedUnretrievable++;
                break;
            }
            else if (windowResult.Outcome is WindowProcessingOutcome.CompletedNormally or WindowProcessingOutcome.CompletedRawEmpty)
            {
                currentWindowDuration = GrowWindowDuration(currentWindowDuration);
            }
            else
            {
                lastStallReason ??= windowResult.StallReason;
                currentWindowDuration = ShrinkWindowDuration(currentWindowDuration);
            }

            LogWindowOutcome(window, windowResult);
            heartbeat.LogProgress(runDiagnostics, forced: true, cancellationToken: cancellationToken);

            if (windowQueue.Count == 0)
            {
                EnqueueNextBaseWindow();
            }
        }

        var backfillComplete = windowQueue.Count == 0 && nextWindowStart >= backfillEndUtc;
        var runOutcome = windowsMarkedUnretrievable > 0 || lastTermination != null
            ? RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly
            : RevisionIngestionRunOutcome.CompletedNormally;

        LogRunSummary(
            backfillStartUtc,
            backfillEndUtc,
            windowsProcessed,
            windowsMarkedUnretrievable,
            totalPersisted,
            backfillComplete);

        return new WindowRunResult(
            totalPersisted,
            pagesProcessed,
            windowsProcessed,
            windowsMarkedUnretrievable,
            backfillComplete,
            scopedFieldDumpLogged,
            lastTermination,
            runOutcome,
            distinctWorkItemIds.Count,
            distinctWorkItemIds,
            minChangedDate,
            maxChangedDate,
            lastStableContinuationTokenHash,
            lastStallReason);
    }

    private static void InsertWindowsAtFront(
        Queue<RevisionIngestionWindow> queue,
        IReadOnlyList<RevisionIngestionWindow> children)
    {
        if (children.Count == 0)
        {
            return;
        }

        var reordered = new Queue<RevisionIngestionWindow>(children.Count + queue.Count);
        foreach (var child in children)
        {
            reordered.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            reordered.Enqueue(queue.Dequeue());
        }

        while (reordered.Count > 0)
        {
            queue.Enqueue(reordered.Dequeue());
        }
    }

    private async Task<WindowProcessingResult> ProcessSingleWindowAsync(
        RevisionIngestionWindow window,
        PoToolDbContext context,
        RevisionIngestionWatermarkEntity watermark,
        IWorkItemRevisionSource revisionSource,
        HashSet<int> allowedWorkItemIds,
        RevisionIngestionPaginationOptions paginationOptions,
        RevisionIngestionRunContext runContext,
        RevisionIngestionRunDiagnostics runDiagnostics,
        IngestionHeartbeat heartbeat,
        RevisionIngestionDiagnosticState diagnosticState,
        Action<RevisionIngestionProgress>? progressCallback,
        int totalPersistedBeforeWindow,
        bool scopedFieldDumpLogged,
        CancellationToken cancellationToken)
    {
        var pageTracker = new ReportingRevisionsPageTracker();
        var continuationToken = (string?)null;
        var pagesProcessed = 0;
        var windowPersisted = 0;
        var outcome = WindowProcessingOutcome.CompletedRawEmpty;
        var stallReason = (WindowStallReason?)null;
        var termination = (ReportingRevisionsTermination?)null;
        var hasSeenWindowOverlap = false;
        var maxTotalPages = Math.Max(1, paginationOptions.MaxTotalPages);
        DateTimeOffset? windowRawMinChangedDate = null;
        DateTimeOffset? windowRawMaxChangedDate = null;
        var windowRawInWindow = false;
        var persistedWorkItemIds = new HashSet<int>();
        DateTimeOffset? minChangedDate = null;
        DateTimeOffset? maxChangedDate = null;
        string? lastTokenHash = null;
        var retryAttempt = 0;
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var minScopeId = allowedWorkItemIds.Count > 0 ? allowedWorkItemIds.Min() : 0;
            var maxScopeId = allowedWorkItemIds.Count > 0 ? allowedWorkItemIds.Max() : 0;
            _logger.LogDebug(
                "Revision ingestion OData scope prepared. ScopeSize={ScopeSize} MinWorkItemId={MinWorkItemId} MaxWorkItemId={MaxWorkItemId}",
                allowedWorkItemIds.Count,
                minScopeId,
                maxScopeId);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var pageIndex = pageTracker.NextPageIndex();
            if (pageIndex > maxTotalPages)
            {
                termination = new ReportingRevisionsTermination(
                    ReportingRevisionsTerminationReason.MaxTotalPages,
                    $"Exceeded maximum total pages ({maxTotalPages}) on page {pageIndex}.");
                LogEarlyTermination(
                    "MaxTotalPages",
                    pageIndex,
                    null,
                    totalPersistedBeforeWindow + windowPersisted);
                stallReason = WindowStallReason.MaxPageLimit;
                outcome = WindowProcessingOutcome.Stalled;
                break;
            }

            pagesProcessed = pageIndex;
            _logger.LogDebug(
                "Fetching revision page {PageNumber} for ProductOwner window {WindowStart} - {WindowEnd}",
                pageIndex,
                window.StartUtc,
                window.EndUtc);

            var logPerPageSummary = runContext.IsEnabled && runContext.LogPerPageSummary;
            var pageStartTimestamp = logPerPageSummary ? Stopwatch.GetTimestamp() : 0;
            using var pageScope = _diagnostics.BeginPageScope(runContext, pageIndex);
            var pageWorkItemIds = logPerPageSummary ? new HashSet<int>() : null;

            var pageRequestStartTimestamp = Stopwatch.GetTimestamp();
            var pageSegmentIndex = runDiagnostics.ResolveSegmentIndex(continuationToken);
            ReportingRevisionsResult result;
            try
            {
                result = await revisionSource.GetRevisionsForScopeAsync(
                    allowedWorkItemIds,
                    window.StartUtc,
                    continuationToken,
                    expandMode: ReportingExpandMode.None,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                termination = new ReportingRevisionsTermination(
                    ReportingRevisionsTerminationReason.ProgressWithoutData,
                    $"Pagination fetch failed on page {pageIndex}: {ex.Message}");
                runDiagnostics.RecordAnomaly("fetchFailed", ex.Message);
                LogEarlyTermination(
                    "FetchFailed",
                    pageIndex,
                    null,
                    totalPersistedBeforeWindow + windowPersisted);
                stallReason = WindowStallReason.RawZeroWithHasMore;
                outcome = WindowProcessingOutcome.Stalled;
                break;
            }

            var rawRevisionCount = result.Revisions.Count;
            var rawMinChangedDate = rawRevisionCount > 0 ? result.Revisions.Min(revision => revision.ChangedDate) : (DateTimeOffset?)null;
            var rawMaxChangedDate = rawRevisionCount > 0 ? result.Revisions.Max(revision => revision.ChangedDate) : (DateTimeOffset?)null;
            var scopedRevisions = result.Revisions
                .Where(revision => allowedWorkItemIds.Contains(revision.WorkItemId))
                .ToList();
            diagnosticState.RecordRevisions(scopedRevisions);
            var scopedInWindow = scopedRevisions
                .Where(revision => revision.ChangedDate >= window.StartUtc && revision.ChangedDate < window.EndUtc)
                .ToList();
            var scopedRevisionCount = scopedRevisions.Count;
            var candidatePersistCount = scopedInWindow.Count;
            var outsideWindowCount = scopedRevisionCount - candidatePersistCount;
            var scopedRevisionsPersistedCount = 0;
            var pageContinuationToken = result.ContinuationToken;
            var pageRequestDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(pageRequestStartTimestamp);
            runDiagnostics.RecordPage(
                pageSegmentIndex,
                rawRevisionCount,
                scopedRevisionCount,
                pageRequestDurationMs,
                result.HttpStatusCode);
            var tokenAdvanced = pageTracker.IsTokenAdvanced(pageContinuationToken);
            var tokenTracking = pageTracker.TrackToken(pageContinuationToken);
            var hasMoreResults = result.HasMoreResults;
            var pagePosition = ClassifyPage(rawMinChangedDate, rawMaxChangedDate, window);
            windowRawMinChangedDate = GetWindowRawMin(windowRawMinChangedDate, rawMinChangedDate);
            windowRawMaxChangedDate = GetWindowRawMax(windowRawMaxChangedDate, rawMaxChangedDate);
            windowRawInWindow |= DoesRawOverlapWindow(rawMinChangedDate, rawMaxChangedDate, window);

            if (!diagnosticState.HasLoggedFirstPageRaw && pageIndex == 1)
            {
                var distinctRawWorkItemIds = result.Revisions.Select(revision => revision.WorkItemId).Distinct().Count();
                var rawScopedCount = result.Revisions.Count(revision => allowedWorkItemIds.Contains(revision.WorkItemId));
                _logger.LogDebug(
                    "Reporting revisions page 1 raw snapshot. RawRevisionCount={RawRevisionCount} DistinctWorkItemCount={DistinctWorkItemCount} MinChangedDateRaw={MinChangedDateRaw} MaxChangedDateRaw={MaxChangedDateRaw} RawScopedWorkItems={RawScopedWorkItems}",
                    rawRevisionCount,
                    distinctRawWorkItemIds,
                    rawMinChangedDate,
                    rawMaxChangedDate,
                    rawScopedCount);
                diagnosticState.HasLoggedFirstPageRaw = true;
            }

            if (result.Termination != null)
            {
                termination = result.Termination;
                runDiagnostics.LogCappedWarning(
                    "paginationTermination",
                    () => _logger.LogWarning(
                        "Reporting revisions pagination terminated early. Reason={Reason} Message={Message}",
                        termination.Reason,
                        termination.Message),
                    $"{termination.Reason}:{termination.Message}");
            }

            if (!scopedFieldDumpLogged && scopedRevisionCount > 0)
            {
                LogScopedRevisionFieldSnapshot(pageIndex, scopedRevisions);
                scopedFieldDumpLogged = true;
            }

            var persistMetrics = new PersistMetrics();
            var persistedCount = await PersistRevisionsAsync(context, scopedInWindow, persistMetrics, cancellationToken);
            scopedRevisionsPersistedCount = persistedCount;
            windowPersisted += persistedCount;
            var totalPersistedAfterPage = totalPersistedBeforeWindow + windowPersisted;
            if (persistedCount > 0)
            {
                foreach (var revision in scopedInWindow)
                {
                    persistedWorkItemIds.Add(revision.WorkItemId);
                    var changedUtc = revision.ChangedDate.ToUniversalTime();
                    minChangedDate = minChangedDate == null || changedUtc < minChangedDate ? changedUtc : minChangedDate;
                    maxChangedDate = maxChangedDate == null || changedUtc > maxChangedDate ? changedUtc : maxChangedDate;
                }
            }

            var persistDurationMs = logPerPageSummary ? persistMetrics.PersistDurationMs : 0;
            if (logPerPageSummary && _logger.IsEnabled(LogLevel.Debug))
            {
                var memoryMb = GC.GetTotalMemory(false) / (1024d * 1024d);
                _logger.LogDebug(
                    "Reporting revisions page summary. WindowStartUtc={WindowStartUtc} WindowEndUtc={WindowEndUtc} PageIndex={PageIndex} RawRevisionCount={RawRevisionCount} ScopedRevisionCount={ScopedRevisionCount} DistinctWorkItemCount={DistinctWorkItemCount} PersistedCount={PersistedCount} HasMoreResults={HasMoreResults} ContinuationTokenHash={ContinuationTokenHash} TokenAdvanced={TokenAdvanced} SeenTokenRepeated={SeenTokenRepeated} DurationMs={DurationMs} TotalPersistedWindow={TotalPersistedWindow} TotalPersistedRun={TotalPersistedRun} MinChangedDateRaw={MinChangedDateRaw} MaxChangedDateRaw={MaxChangedDateRaw} MemoryMb={MemoryMb}",
                    window.StartUtc,
                    window.EndUtc,
                    pageIndex,
                    rawRevisionCount,
                    scopedRevisionCount,
                    pageWorkItemIds?.Count ?? 0,
                    scopedRevisionsPersistedCount,
                    hasMoreResults,
                    tokenTracking.TokenHash,
                    tokenAdvanced,
                    tokenTracking.SeenTokenRepeated,
                    pageRequestDurationMs,
                    windowPersisted,
                    totalPersistedAfterPage,
                    rawMinChangedDate,
                    rawMaxChangedDate,
                    memoryMb);
            }

            foreach (var revision in scopedRevisions)
            {
                pageWorkItemIds?.Add(revision.WorkItemId);
            }

            _logger.LogDebug(
                "Persisted {Count} revisions (page {Page}) for window {WindowStart} - {WindowEnd}",
                scopedRevisionsPersistedCount,
                pageIndex,
                window.StartUtc,
                window.EndUtc);

            _logger.LogDebug(
                "Revision ingestion page persist. TransactionUsed={TransactionUsed} PersistDurationMs={PersistDurationMs} SaveChangesDurationMs={SaveChangesDurationMs} CommitDurationMs={CommitDurationMs} RevisionHeaderCount={RevisionHeaderCount} FieldDeltaCount={FieldDeltaCount} RelationDeltaCount={RelationDeltaCount}",
                persistMetrics.TransactionUsed,
                persistMetrics.PersistDurationMs,
                persistMetrics.SaveChangesDurationMs,
                persistMetrics.CommitDurationMs ?? -1,
                persistMetrics.RevisionHeaderCount,
                persistMetrics.FieldDeltaCount,
                persistMetrics.RelationDeltaCount);

            var duplicatesDropped = persistMetrics.DuplicateCount;
            var missingRequiredDropped = persistMetrics.MissingRequiredCount;
            var dbConstraintDrops = persistMetrics.DbConstraintCount;
            var otherDrops = Math.Max(0, candidatePersistCount - (scopedRevisionsPersistedCount + duplicatesDropped + missingRequiredDropped + dbConstraintDrops));
            var boundedOutsideWindowCount = Math.Max(0, outsideWindowCount);

            _logger.LogDebug(
                "Revision ingestion drop accounting. WindowStartUtc={WindowStartUtc} WindowEndUtc={WindowEndUtc} PageIndex={PageIndex} RawRevisionCount={RawRevisionCount} ScopedRevisionCount={ScopedRevisionCount} CandidatePersistCount={CandidatePersistCount} PersistedCount={PersistedCount} DropsAlreadyExists={DropsAlreadyExists} DropsOutsideWindow={DropsOutsideWindow} DropsMissingRequiredField={DropsMissingRequiredField} DropsDbConstraint={DropsDbConstraint} DropsOther={DropsOther}",
                window.StartUtc,
                window.EndUtc,
                pageIndex,
                rawRevisionCount,
                scopedRevisionCount,
                candidatePersistCount,
                scopedRevisionsPersistedCount,
                duplicatesDropped,
                boundedOutsideWindowCount,
                missingRequiredDropped,
                dbConstraintDrops,
                otherDrops);

            _logger.LogDebug(
                "Revision deduplication key WorkItemId+RevisionNumber. RevisionNumberUsed=True ChangedDateUsed=False DuplicatesSkipped={DuplicatesSkipped}",
                duplicatesDropped);

            if (runContext.LogEfSaveChangesDetails)
            {
                _diagnostics.LogSaveChangesDetails(
                    runContext,
                    persistMetrics.SaveChangesDurationMs,
                    persistMetrics.RevisionHeaderCount,
                    persistMetrics.FieldDeltaCount,
                    persistMetrics.RelationDeltaCount);
            }

            if (runContext.IsEnabled &&
                runContext.LogGcStatsEveryNPages > 0 &&
                pageIndex % runContext.LogGcStatsEveryNPages == 0)
            {
                var trackedEntries = context.ChangeTracker.Entries().Count();
                _diagnostics.LogGcStats(
                    runContext,
                    GC.GetTotalMemory(false),
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2),
                    trackedEntries);
            }

            context.ChangeTracker.Clear();
            context.RevisionIngestionWatermarks.Attach(watermark);

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = watermark.IsInitialBackfillComplete ? "Incremental Sync" : "Initial Backfill",
                PercentComplete = 0,
                RevisionsProcessed = totalPersistedAfterPage,
                CurrentPage = pageIndex
            });

            if (logPerPageSummary)
            {
                var continuationTokenPresent = !string.IsNullOrEmpty(result.ContinuationToken);
                var continuationTokenLength = continuationTokenPresent ? result.ContinuationToken!.Length : 0;
                var httpDurationMs = result.HttpDurationMs ?? 0;
                var dbSlow = persistDurationMs >= runContext.SlowDbThresholdMs;
                var httpSlow = httpDurationMs >= runContext.SlowHttpThresholdMs;
                var totalPageDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(pageStartTimestamp);
                var pageSlow = totalPageDurationMs >= runContext.SlowPageThresholdMs;
                var memoryBytes = GC.GetTotalMemory(false);

                _diagnostics.LogPageRequest(
                    runContext,
                    continuationTokenPresent,
                    continuationTokenLength,
                    result.HttpDurationMs,
                    result.HttpStatusCode,
                    result.ParseDurationMs,
                    result.TransformDurationMs);

                _diagnostics.LogPageCounts(
                    runContext,
                    rawRevisionCount,
                    scopedRevisionCount,
                    pageWorkItemIds?.Count ?? 0,
                    persistMetrics?.RevisionHeaderCount ?? 0,
                    persistMetrics?.FieldDeltaCount ?? 0,
                    persistMetrics?.RelationDeltaCount ?? 0);

                _diagnostics.LogPagePersistence(
                    runContext,
                    persistDurationMs,
                    totalPageDurationMs,
                    pageSlow,
                    httpSlow,
                    dbSlow,
                    memoryBytes);
            }
            heartbeat.LogProgress(runDiagnostics, forced: false, cancellationToken: cancellationToken);

            var paginationAnomaly = termination != null;
            var retryable = termination == null;
            var stalled = false;
            if (paginationAnomaly)
            {
                stalled = true;
                stallReason = WindowStallReason.ClientTermination;
            }
            else if (tokenTracking.SeenTokenRepeated)
            {
                termination = new ReportingRevisionsTermination(
                    ReportingRevisionsTerminationReason.RepeatedContinuationToken,
                    $"Continuation token repeated on page {pageIndex}.");
                LogEarlyTermination(
                    "TokenRepeated",
                    pageIndex,
                    tokenTracking.TokenHash,
                    totalPersistedAfterPage);
                paginationAnomaly = true;
                stallReason = WindowStallReason.TokenRepeated;
                stalled = true;
            }
            else if (rawRevisionCount == 0 && (hasMoreResults || !tokenAdvanced))
            {
                termination = new ReportingRevisionsTermination(
                    ReportingRevisionsTerminationReason.ProgressWithoutData,
                    $"Reporting revisions returned no data on page {pageIndex} while indicating more results.");
                LogEarlyTermination(
                    "DeadPageNoData",
                    pageIndex,
                    tokenTracking.TokenHash,
                    totalPersistedAfterPage);
                paginationAnomaly = true;
                stallReason = WindowStallReason.RawZeroWithHasMore;
                stalled = true;
            }
            else if (!tokenAdvanced && hasMoreResults)
            {
                termination = new ReportingRevisionsTermination(
                    ReportingRevisionsTerminationReason.RepeatedContinuationToken,
                    $"Continuation token did not advance on page {pageIndex}.");
                LogEarlyTermination(
                    "TokenNotAdvanced",
                    pageIndex,
                    tokenTracking.TokenHash,
                    totalPersistedAfterPage);
                paginationAnomaly = true;
                stallReason = WindowStallReason.TokenNotAdvancing;
                stalled = true;
            }

            if (stalled)
            {
                if (retryable && retryAttempt < paginationOptions.MaxPageRetries)
                {
                    var backoff = GetRetryDelay(paginationOptions, retryAttempt);
                    retryAttempt++;
                    runDiagnostics.RecordRetry(
                        "Pagination",
                        retryAttempt,
                        backoff.TotalMilliseconds,
                        pageRequestDurationMs);
                    runDiagnostics.LogCappedWarning(
                        "paginationRetry",
                        () => _logger.LogWarning(
                            "Pagination anomaly detected; retrying page. PageIndex={PageIndex} Attempt={Attempt}/{MaxAttempts} StallReason={StallReason} BackoffMs={BackoffMs}",
                            pageIndex,
                            retryAttempt,
                            paginationOptions.MaxPageRetries,
                            stallReason,
                            backoff.TotalMilliseconds),
                        $"Page={pageIndex};Stall={stallReason}");
                    pageTracker.RewindPageIndex();
                    pageTracker.RollbackToken(tokenTracking);
                    await Task.Delay(backoff, cancellationToken);
                    continue;
                }

                outcome = WindowProcessingOutcome.Stalled;
                break;
            }

            retryAttempt = 0;
            var windowComplete = false;

            if (pagePosition == PagePosition.OverlapsWindow)
            {
                hasSeenWindowOverlap = true;
            }
            else if (pagePosition == PagePosition.OlderThanWindow ||
                (pagePosition == PagePosition.NewerThanWindow && hasSeenWindowOverlap))
            {
                windowComplete = true;
            }

            if (!hasMoreResults)
            {
                windowComplete = true;
            }

            continuationToken = pageContinuationToken;
            pageTracker.CommitToken(pageContinuationToken);
            lastTokenHash = tokenTracking.TokenHash ?? lastTokenHash;

            if (windowComplete)
            {
                outcome = windowPersisted > 0 ? WindowProcessingOutcome.CompletedNormally : WindowProcessingOutcome.CompletedRawEmpty;
                break;
            }
        }

        LogWindowRawRange(window, windowRawMinChangedDate, windowRawMaxChangedDate, windowRawInWindow);

        return new WindowProcessingResult(
            outcome,
            pagesProcessed,
            windowPersisted,
            stallReason,
            termination,
            scopedFieldDumpLogged,
            persistedWorkItemIds,
            minChangedDate,
            maxChangedDate,
            lastTokenHash);
    }

    private static ReportingRevisionsTermination CreateTerminationFromStall(WindowStallReason stallReason)
    {
        return stallReason switch
        {
            WindowStallReason.TokenRepeated or WindowStallReason.TokenNotAdvancing => new ReportingRevisionsTermination(
                ReportingRevisionsTerminationReason.RepeatedContinuationToken,
                "Continuation token did not advance."),
            WindowStallReason.RawZeroWithHasMore => new ReportingRevisionsTermination(
                ReportingRevisionsTerminationReason.ProgressWithoutData,
                "Reporting revisions returned no data while indicating more results."),
            WindowStallReason.MaxPageLimit => new ReportingRevisionsTermination(
                ReportingRevisionsTerminationReason.MaxTotalPages,
                "Maximum total pages reached."),
            WindowStallReason.ClientTermination => new ReportingRevisionsTermination(
                ReportingRevisionsTerminationReason.ProgressWithoutData,
                "Reporting client terminated pagination."),
            _ => new ReportingRevisionsTermination(
                ReportingRevisionsTerminationReason.ProgressWithoutData,
                "Pagination anomaly detected.")
        };
    }

    private static TimeSpan GetRetryDelay(
        RevisionIngestionPaginationOptions paginationOptions,
        int retryAttempt)
    {
        var baseDelaySeconds = Math.Max(0, paginationOptions.RetryBackoffSeconds);
        var jitterSeconds = Math.Max(0, paginationOptions.RetryBackoffJitterSeconds);
        var exponentialSeconds = baseDelaySeconds * Math.Pow(2, Math.Max(0, retryAttempt));
        var jitter = jitterSeconds == 0 ? 0 : Random.Shared.NextDouble() * jitterSeconds;
        var totalSeconds = Math.Max(0, exponentialSeconds + jitter);
        return TimeSpan.FromSeconds(totalSeconds);
    }

    private static TimeSpan GrowWindowDuration(TimeSpan current)
    {
        var doubled = TimeSpan.FromTicks(current.Ticks * 2);
        if (doubled > MaximumWindowDuration)
        {
            return MaximumWindowDuration;
        }

        return doubled;
    }

    private static TimeSpan ShrinkWindowDuration(TimeSpan current)
    {
        if (current <= PreferredMinimumWindowDuration)
        {
            return MinimumWindowDuration;
        }

        var halved = TimeSpan.FromTicks(current.Ticks / 2);
        if (halved < PreferredMinimumWindowDuration)
        {
            return PreferredMinimumWindowDuration;
        }

        return halved;
    }

    private static IReadOnlyList<RevisionIngestionWindow> SplitWindow(
        RevisionIngestionWindow window,
        ref int windowSequence)
    {
        var duration = window.EndUtc - window.StartUtc;
        if (duration <= MinimumWindowDuration)
        {
            return Array.Empty<RevisionIngestionWindow>();
        }

        if (duration <= PreferredMinimumWindowDuration)
        {
            var start = window.StartUtc;
            var end = window.EndUtc;
            var mid = start.Add(duration / 2);
            return new[]
            {
                new RevisionIngestionWindow(start, mid, window.Depth + 1, Interlocked.Increment(ref windowSequence)),
                new RevisionIngestionWindow(mid, end, window.Depth + 1, Interlocked.Increment(ref windowSequence))
            };
        }

        var splitPoint = window.StartUtc + TimeSpan.FromTicks(duration.Ticks / 2);
        return new[]
        {
            new RevisionIngestionWindow(window.StartUtc, splitPoint, window.Depth + 1, Interlocked.Increment(ref windowSequence)),
            new RevisionIngestionWindow(splitPoint, window.EndUtc, window.Depth + 1, Interlocked.Increment(ref windowSequence))
        };
    }

    private static PagePosition ClassifyPage(
        DateTimeOffset? minChangedDate,
        DateTimeOffset? maxChangedDate,
        RevisionIngestionWindow window)
    {
        if (minChangedDate == null || maxChangedDate == null)
        {
            return PagePosition.Unknown;
        }

        if (minChangedDate.Value >= window.EndUtc)
        {
            return PagePosition.NewerThanWindow;
        }

        if (maxChangedDate.Value < window.StartUtc)
        {
            return PagePosition.OlderThanWindow;
        }

        return PagePosition.OverlapsWindow;
    }

    private void LogWindowOutcome(
        RevisionIngestionWindow window,
        WindowProcessingResult result)
    {
        _logger.LogInformation(
            "Revision ingestion window outcome. WindowStartUtc={WindowStartUtc} WindowEndUtc={WindowEndUtc} PagesProcessed={PagesProcessed} PersistedCount={PersistedCount} Outcome={Outcome} StallReason={StallReason}",
            window.StartUtc,
            window.EndUtc,
            result.PagesProcessed,
            result.PersistedCount,
            result.Outcome,
            result.StallReason);
    }

    private void LogRunSummary(
        DateTimeOffset backfillStartUtc,
        DateTimeOffset backfillEndUtc,
        int windowsProcessed,
        int windowsMarkedUnretrievable,
        int totalPersisted,
        bool backfillComplete)
    {
        _logger.LogInformation(
            "Revision ingestion run summary. BackfillStartUtc={BackfillStartUtc} BackfillEndUtc={BackfillEndUtc} WindowsProcessed={WindowsProcessed} WindowsMarkedUnretrievable={WindowsMarkedUnretrievable} TotalPersisted={TotalPersisted} BackfillComplete={BackfillComplete}",
            backfillStartUtc,
            backfillEndUtc,
            windowsProcessed,
            windowsMarkedUnretrievable,
            totalPersisted,
            backfillComplete);
    }

    private async Task<DatabaseRevisionSnapshot> CaptureRevisionDatabaseSnapshotAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        var totalRows = await context.RevisionHeaders.CountAsync(cancellationToken);
        var distinctWorkItemIds = await context.RevisionHeaders
            .Select(header => header.WorkItemId)
            .Distinct()
            .CountAsync(cancellationToken);
        var changedDates = await context.RevisionHeaders
            .AsNoTracking()
            .Select(header => header.ChangedDate)
            .ToListAsync(cancellationToken);
        var minChangedDate = changedDates.Count > 0 ? changedDates.Min() : (DateTimeOffset?)null;
        var maxChangedDate = changedDates.Count > 0 ? changedDates.Max() : (DateTimeOffset?)null;

        return new DatabaseRevisionSnapshot(totalRows, distinctWorkItemIds, minChangedDate, maxChangedDate);
    }

    private void LogDatabaseSnapshot(string stage, DatabaseRevisionSnapshot snapshot)
    {
        _logger.LogInformation(
            "Revision ingestion DB snapshot. Stage={Stage} TotalRows={TotalRows} DistinctWorkItemIds={DistinctWorkItemIds} MinChangedDate={MinChangedDate} MaxChangedDate={MaxChangedDate}",
            stage,
            snapshot.TotalRows,
            snapshot.DistinctWorkItemIds,
            snapshot.MinChangedDate,
            snapshot.MaxChangedDate);
    }

    private void LogCreationSnapshotSummary(
        RevisionIngestionDiagnosticState diagnosticState,
        IReadOnlyCollection<int> scopedWorkItemIds)
    {
        var (withCreation, withoutCreation) = diagnosticState.GetCreationSnapshotSummary(scopedWorkItemIds);
        _logger.LogInformation(
            "Creation snapshot coverage. ScopedWorkItems={ScopedWorkItems} WithCreationObserved={WithCreationObserved} WithoutCreationObserved={WithoutCreationObserved}",
            scopedWorkItemIds.Count,
            withCreation,
            withoutCreation);
    }

    private void LogWindowRawRange(
        RevisionIngestionWindow window,
        DateTimeOffset? minChangedDateRaw,
        DateTimeOffset? maxChangedDateRaw,
        bool hasRawInsideWindow)
    {
        _logger.LogInformation(
            "Revision ingestion window raw range. WindowStartUtc={WindowStartUtc} WindowEndUtc={WindowEndUtc} MinChangedDateRaw={MinChangedDateRaw} MaxChangedDateRaw={MaxChangedDateRaw} RawInsideWindow={RawInsideWindow}",
            window.StartUtc,
            window.EndUtc,
            minChangedDateRaw,
            maxChangedDateRaw,
            hasRawInsideWindow);

        if (!hasRawInsideWindow)
        {
            _logger.LogWarning(
                "No raw revisions fell inside the window bounds. WindowStartUtc={WindowStartUtc} WindowEndUtc={WindowEndUtc}",
                window.StartUtc,
                window.EndUtc);
        }
    }

    private static DateTimeOffset? GetWindowRawMin(DateTimeOffset? current, DateTimeOffset? candidate)
    {
        if (candidate == null)
        {
            return current;
        }

        if (current == null || candidate.Value < current.Value)
        {
            return candidate;
        }

        return current;
    }

    private static DateTimeOffset? GetWindowRawMax(DateTimeOffset? current, DateTimeOffset? candidate)
    {
        if (candidate == null)
        {
            return current;
        }

        if (current == null || candidate.Value > current.Value)
        {
            return candidate;
        }

        return current;
    }

    private static bool DoesRawOverlapWindow(
        DateTimeOffset? minChangedDate,
        DateTimeOffset? maxChangedDate,
        RevisionIngestionWindow window)
    {
        if (minChangedDate == null || maxChangedDate == null)
        {
            return false;
        }

        return maxChangedDate.Value >= window.StartUtc && minChangedDate.Value < window.EndUtc;
    }


    private async Task<RevisionIngestionWatermarkEntity> GetOrCreateWatermarkAsync(
        PoToolDbContext context,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var watermark = await context.RevisionIngestionWatermarks
            .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, cancellationToken);

        if (watermark == null)
        {
            watermark = new RevisionIngestionWatermarkEntity
            {
                ProductOwnerId = productOwnerId,
                IsInitialBackfillComplete = false
            };
            context.RevisionIngestionWatermarks.Add(watermark);
            await context.SaveChangesAsync(cancellationToken);
        }

        return watermark;
    }

    private async Task<HashSet<int>> ResolveAllowedWorkItemIdsForProductOwnerAsync(
        PoToolDbContext context,
        ITfsClient tfsClient,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var productOwner = await context.Profiles
            .Include(profile => profile.Products)
            .FirstOrDefaultAsync(profile => profile.Id == productOwnerId, cancellationToken);

        if (productOwner == null)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} was not found.");
        }

        if (productOwner.Products.Count == 0)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} has no products configured.");
        }

        var rootWorkItemIds = productOwner.Products
            .Select(product => product.BacklogRootWorkItemId)
            .Where(rootId => rootId > 0)
            .Distinct()
            .ToArray();

        if (rootWorkItemIds.Length == 0)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} has no valid backlog root work item IDs.");
        }

        var workItems = await tfsClient.GetWorkItemsByRootIdsAsync(
            rootWorkItemIds,
            null,
            null,
            cancellationToken);

        var descendantWorkItems = WorkItemHierarchyHelper.FilterDescendants(rootWorkItemIds, workItems);
        var allowedWorkItemIds = descendantWorkItems
            .Select(workItem => workItem.TfsId)
            .ToHashSet();

        if (allowedWorkItemIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"ProductOwner {productOwnerId} has no work items under configured backlog roots.");
        }

        return allowedWorkItemIds;
    }

    private void LogScopedRevisionFieldSnapshot(int pageIndex, IReadOnlyCollection<WorkItemRevision> revisions)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        foreach (var revision in revisions)
        {
            _logger.LogInformation(
                "Scoped revision field snapshot. PageIndex={PageIndex} WorkItemId={WorkItemId} RevisionNumber={RevisionNumber} Fields={Fields}",
                pageIndex,
                revision.WorkItemId,
                revision.RevisionNumber,
                new
                {
                    revision.WorkItemType,
                    revision.Title,
                    revision.State,
                    revision.Reason,
                    revision.IterationPath,
                    revision.AreaPath,
                    revision.CreatedDate,
                    revision.ChangedDate,
                    revision.ClosedDate,
                    revision.Effort,
                    revision.BusinessValue,
                    revision.Tags,
                    revision.Severity,
                    revision.ChangedBy
                });
        }
    }

    private async Task<int> PersistRevisionsAsync(
        PoToolDbContext context,
        IReadOnlyList<WorkItemRevision> revisions,
        PersistMetrics? metrics,
        CancellationToken cancellationToken)
    {
        if (revisions.Count == 0)
        {
            return 0;
        }

        var persistedCount = 0;
        var persistStart = Stopwatch.GetTimestamp();
        var options = _persistenceOptions.CurrentValue;
        var autoDetectChangesEnabled = context.ChangeTracker.AutoDetectChangesEnabled;
        IDbContextTransaction? transaction = null;
        var transactionUsed = false;
        List<int>? workItemIds = null;
        HashSet<(int WorkItemId, int RevisionNumber)>? existingKeys = null;
        List<RevisionHeaderEntity>? headers = null;

        try
        {
            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                transactionUsed = true;
            }

            if (options.Enabled)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
            }

            workItemIds = revisions.Select(revision => revision.WorkItemId).Distinct().ToList();
            existingKeys = new HashSet<(int WorkItemId, int RevisionNumber)>();

            if (workItemIds.Count > 0)
            {
                var existingRevisions = await context.RevisionHeaders.AsNoTracking()
                    .Where(header => workItemIds.Contains(header.WorkItemId))
                    .Select(header => new { header.WorkItemId, header.RevisionNumber })
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingRevisions)
                {
                    existingKeys.Add((existing.WorkItemId, existing.RevisionNumber));
                }
            }

            headers = new List<RevisionHeaderEntity>(revisions.Count);

            foreach (var revision in revisions)
            {
                if (existingKeys.Contains((revision.WorkItemId, revision.RevisionNumber)))
                {
                    metrics?.IncrementDuplicate();
                    continue;
                }

                if (!HasRequiredRevisionFields(revision))
                {
                    metrics?.IncrementMissingRequired();
                    continue;
                }

                var header = new RevisionHeaderEntity
                {
                    WorkItemId = revision.WorkItemId,
                    RevisionNumber = revision.RevisionNumber,
                    WorkItemType = revision.WorkItemType,
                    Title = revision.Title,
                    State = revision.State,
                    Reason = revision.Reason,
                    IterationPath = revision.IterationPath,
                    AreaPath = revision.AreaPath,
                    CreatedDate = revision.CreatedDate,
                    ChangedDate = revision.ChangedDate,
                    ClosedDate = revision.ClosedDate,
                    Effort = revision.Effort,
                    BusinessValue = revision.BusinessValue,
                    Tags = revision.Tags,
                    Severity = revision.Severity,
                    ChangedBy = revision.ChangedBy,
                    IngestedAt = DateTimeOffset.UtcNow
                };

                headers.Add(header);
                metrics?.IncrementRevisionHeader();

                persistedCount++;
            }

            if (headers != null && headers.Count > 0)
            {
                context.RevisionHeaders.AddRange(headers);
            }

            if (options.Enabled)
            {
                context.ChangeTracker.DetectChanges();
            }

            var saveStart = Stopwatch.GetTimestamp();
            await context.SaveChangesAsync(cancellationToken);

            if (metrics != null)
            {
                metrics.SaveChangesDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(saveStart);
            }

            if (transaction != null)
            {
                var commitStart = Stopwatch.GetTimestamp();
                await transaction.CommitAsync(cancellationToken);
                if (metrics != null)
                {
                    metrics.CommitDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(commitStart);
                }
            }
            else if (metrics != null)
            {
                metrics.CommitDurationMs = null;
            }
        }
        catch (OperationCanceledException)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (options.Enabled)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
            }

            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }

            if (metrics != null)
            {
                metrics.PersistDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(persistStart);
                metrics.TransactionUsed = transactionUsed;
            }

            headers?.Clear();
            workItemIds?.Clear();
            existingKeys?.Clear();
        }

        return persistedCount;
    }

    private static bool HasRequiredRevisionFields(WorkItemRevision revision)
    {
        // Only WorkItemId is business-required; WorkItemId+ChangedDate+(Revision if needed) are infra-required.
        return revision.WorkItemId > 0
            && revision.RevisionNumber > 0
            && revision.ChangedDate != default;
    }

    private static string? GetConnectionMode(PoToolDbContext context)
    {
        var connectionString = context.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue("Mode", out var mode))
            {
                return mode?.ToString();
            }

            if (builder.TryGetValue("ApplicationIntent", out var intent))
            {
                return intent?.ToString();
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private string? ProtectContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        return _tokenProtector.Protect(continuationToken);
    }

    private string? UnprotectContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        try
        {
            return _tokenProtector.Unprotect(continuationToken);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning(
                "Failed to unprotect continuation token; ignoring stored token and restarting pagination from the last sync start.");
            return null;
        }
    }

    private void LogEarlyTermination(
        string reason,
        int pageIndex,
        string? continuationTokenHash,
        int totalPersistedSoFar)
    {
        if (!_logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        _logger.LogWarning(
            "Reporting revisions pagination terminated early. Reason={Reason} PageIndex={PageIndex} ContinuationTokenHash={ContinuationTokenHash} TotalPersistedSoFar={TotalPersistedSoFar}",
            reason,
            pageIndex,
            continuationTokenHash,
            totalPersistedSoFar);
    }

    private sealed class ReportingRevisionsPageTracker
    {
        private readonly HashSet<string> _seenTokenHashes = new(StringComparer.Ordinal);
        private string? _previousToken;

        public int PageIndex { get; private set; }

        public int NextPageIndex()
        {
            PageIndex++;
            return PageIndex;
        }

        public bool IsTokenAdvanced(string? newToken)
        {
            return !string.Equals(newToken, _previousToken, StringComparison.Ordinal);
        }

        public TokenTrackingSnapshot TrackToken(string? newToken)
        {
            var tokenHash = HashContinuationToken(newToken);
            var addedToSeen = false;
            var seenTokenRepeated = false;
            if (tokenHash != null)
            {
                addedToSeen = _seenTokenHashes.Add(tokenHash);
                seenTokenRepeated = !addedToSeen;
            }

            return new TokenTrackingSnapshot(tokenHash, seenTokenRepeated, addedToSeen);
        }

        public void CommitToken(string? newToken)
        {
            _previousToken = newToken;
        }

        public void RewindPageIndex()
        {
            if (PageIndex > 0)
            {
                PageIndex--;
            }
        }

        public void RollbackToken(TokenTrackingSnapshot snapshot)
        {
            if (snapshot.TokenHash != null && snapshot.AddedToSeen)
            {
                _seenTokenHashes.Remove(snapshot.TokenHash);
            }
        }

        private static string? HashContinuationToken(string? continuationToken)
        {
            if (string.IsNullOrWhiteSpace(continuationToken))
            {
                return null;
            }

            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(continuationToken));
            var hashHex = Convert.ToHexString(hashBytes);
            // SHA256 hashes are always 32 bytes; hex encoding uses two characters per byte (64 chars total).
            // 12 characters keeps logs compact while still offering low collision risk for diagnostics.
            return hashHex[..ContinuationTokenHashLength];
        }
    }

    private sealed record TokenTrackingSnapshot(string? TokenHash, bool SeenTokenRepeated, bool AddedToSeen);

    private sealed class RevisionIngestionDiagnosticState
    {
        private readonly Dictionary<int, CreationObservation> _creationByWorkItem = new();

        public bool HasLoggedFirstPageRaw { get; set; }

        public void RecordRevisions(IEnumerable<WorkItemRevision> revisions)
        {
            foreach (var revision in revisions)
            {
                var changedUtc = revision.ChangedDate.ToUniversalTime();
                var createdUtc = revision.CreatedDate?.ToUniversalTime();

                if (!_creationByWorkItem.TryGetValue(revision.WorkItemId, out var observation))
                {
                    observation = new CreationObservation(false, changedUtc);
                }

                var earliestChangedDate = observation.EarliestChangedDateUtc;
                if (changedUtc < earliestChangedDate)
                {
                    earliestChangedDate = changedUtc;
                }

                var creationObserved = observation.CreationObserved;
                if (createdUtc.HasValue && createdUtc.Value == changedUtc)
                {
                    creationObserved = true;
                }

                if (changedUtc <= earliestChangedDate)
                {
                    creationObserved = true;
                }

                _creationByWorkItem[revision.WorkItemId] = new CreationObservation(
                    creationObserved,
                    earliestChangedDate);
            }
        }

        public (int WithCreation, int WithoutCreation) GetCreationSnapshotSummary(IReadOnlyCollection<int> scopedWorkItemIds)
        {
            var withCreation = 0;

            foreach (var workItemId in scopedWorkItemIds)
            {
                if (_creationByWorkItem.TryGetValue(workItemId, out var observation) && observation.CreationObserved)
                {
                    withCreation++;
                }
            }

            var withoutCreation = scopedWorkItemIds.Count - withCreation;
            return (withCreation, withoutCreation);
        }
    }

    private sealed class RevisionIngestionRunDiagnostics
    {
        private readonly object _sync = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Dictionary<string, RetryAggregate> _retryByStage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AnomalyAggregate> _anomalyByCategory = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _segmentsWithScopedRevisions = new();
        private long _pagesTotal;
        private long _rawRevisionsTotal;
        private long _scopedRevisionsTotal;
        private long _pagesWithoutScopedRevisionsTotal;
        private long _httpRequestCount;
        private long _httpNon200Count;
        private long _totalHttpDurationMs;
        private int _lastKnownSegmentIndex = -1;

        private RevisionIngestionRunDiagnostics(
            Guid runId,
            DateTimeOffset startUtc,
            int scopeCount,
            int? scopeMin,
            int? scopeMax,
            int segmentCount,
            int totalSegmentSpan,
            int maxSegmentSpan,
            CappedWarningLogger cappedWarnings)
        {
            RunId = runId;
            StartUtc = startUtc;
            ScopeCount = scopeCount;
            ScopeMin = scopeMin;
            ScopeMax = scopeMax;
            SegmentCount = segmentCount;
            TotalSegmentSpan = totalSegmentSpan;
            MaxSegmentSpan = maxSegmentSpan;
            CappedWarnings = cappedWarnings;
        }

        public Guid RunId { get; }
        public DateTimeOffset StartUtc { get; }
        public int ScopeCount { get; }
        public int? ScopeMin { get; }
        public int? ScopeMax { get; }
        public int SegmentCount { get; }
        public int TotalSegmentSpan { get; }
        public int MaxSegmentSpan { get; }
        public CappedWarningLogger CappedWarnings { get; }
        public int PagesTotal => (int)Interlocked.Read(ref _pagesTotal);
        public int RawRevisionsTotal => (int)Interlocked.Read(ref _rawRevisionsTotal);
        public int ScopedRevisionsTotal => (int)Interlocked.Read(ref _scopedRevisionsTotal);
        public int PagesWithoutScopedRevisionsTotal => (int)Interlocked.Read(ref _pagesWithoutScopedRevisionsTotal);
        public int LastKnownSegmentIndex => Volatile.Read(ref _lastKnownSegmentIndex);

        public static RevisionIngestionRunDiagnostics Create(
            int productOwnerId,
            IReadOnlyCollection<int> scopedWorkItemIds,
            RevisionIngestionPaginationOptions options,
            int warningLimit)
        {
            _ = productOwnerId;
            var now = DateTimeOffset.UtcNow;
            var scopeCount = scopedWorkItemIds.Count;
            var scopeMin = scopeCount > 0 ? scopedWorkItemIds.Min() : (int?)null;
            var scopeMax = scopeCount > 0 ? scopedWorkItemIds.Max() : (int?)null;
            var segmentSpans = options.ODataScopeMode == ODataRevisionScopeMode.Range && scopeCount > 0
                ? BuildSegmentSpans(scopedWorkItemIds)
                : Array.Empty<int>();
            var segmentCount = segmentSpans.Length;
            var totalSegmentSpan = segmentSpans.Sum();
            var maxSegmentSpan = segmentCount == 0 ? 0 : segmentSpans.Max();
            return new RevisionIngestionRunDiagnostics(
                Guid.NewGuid(),
                now,
                scopeCount,
                scopeMin,
                scopeMax,
                segmentCount,
                totalSegmentSpan,
                maxSegmentSpan,
                new CappedWarningLogger(Math.Max(1, warningLimit)));
        }

        private static int[] BuildSegmentSpans(IEnumerable<int> scopedWorkItemIds)
        {
            var ordered = scopedWorkItemIds
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (ordered.Length == 0)
            {
                return Array.Empty<int>();
            }

            var spans = new List<int>();
            var segmentStart = ordered[0];
            var previous = ordered[0];
            for (var index = 1; index < ordered.Length; index++)
            {
                var current = ordered[index];
                if (current == previous + 1)
                {
                    previous = current;
                    continue;
                }

                spans.Add(previous - segmentStart + 1);
                segmentStart = current;
                previous = current;
            }

            spans.Add(previous - segmentStart + 1);
            return spans.ToArray();
        }

        public int? ResolveSegmentIndex(string? continuationToken)
        {
            if (SegmentCount == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(continuationToken))
            {
                return 1;
            }

            if (!continuationToken.StartsWith("seg:", StringComparison.Ordinal))
            {
                return LastKnownSegmentIndex > 0 ? LastKnownSegmentIndex : 1;
            }

            var separatorIndex = continuationToken.IndexOf('|');
            if (separatorIndex <= "seg:".Length)
            {
                return LastKnownSegmentIndex > 0 ? LastKnownSegmentIndex : 1;
            }

            var payload = continuationToken["seg:".Length..separatorIndex];
            if (int.TryParse(payload, out var zeroBased))
            {
                return Math.Clamp(zeroBased + 1, 1, Math.Max(1, SegmentCount));
            }

            return LastKnownSegmentIndex > 0 ? LastKnownSegmentIndex : 1;
        }

        public void RecordPage(
            int? segmentIndex,
            int rawRevisionCount,
            int scopedRevisionCount,
            long httpDurationMs,
            int? httpStatusCode)
        {
            if (segmentIndex.HasValue && segmentIndex.Value > 0)
            {
                Volatile.Write(ref _lastKnownSegmentIndex, segmentIndex.Value);
            }

            Interlocked.Increment(ref _pagesTotal);
            Interlocked.Add(ref _rawRevisionsTotal, rawRevisionCount);
            Interlocked.Add(ref _scopedRevisionsTotal, scopedRevisionCount);
            if (scopedRevisionCount == 0)
            {
                Interlocked.Increment(ref _pagesWithoutScopedRevisionsTotal);
            }

            Interlocked.Increment(ref _httpRequestCount);
            if (httpStatusCode.HasValue && httpStatusCode.Value != 200)
            {
                Interlocked.Increment(ref _httpNon200Count);
            }

            Interlocked.Add(ref _totalHttpDurationMs, Math.Max(0, httpDurationMs));

            if (segmentIndex.HasValue && segmentIndex.Value > 0 && scopedRevisionCount > 0)
            {
                lock (_sync)
                {
                    _segmentsWithScopedRevisions.Add(segmentIndex.Value);
                }
            }
        }

        public void RecordRetry(string stage, int attempt, double backoffMs, long retryDurationMs)
        {
            lock (_sync)
            {
                if (!_retryByStage.TryGetValue(stage, out var current))
                {
                    current = new RetryAggregate(0, 0, 0, 0);
                }

                _retryByStage[stage] = new RetryAggregate(
                    current.Count + 1,
                    Math.Max(current.MaxAttemptSeen, attempt),
                    current.TotalBackoffMs + Math.Max(0, (long)Math.Round(backoffMs)),
                    current.TotalRetryDurationMs + Math.Max(0, retryDurationMs));
            }
        }

        public void RecordAnomaly(string category, object? sampleContext = null)
        {
            lock (_sync)
            {
                if (!_anomalyByCategory.TryGetValue(category, out var current))
                {
                    current = new AnomalyAggregate(0, 0);
                }

                _anomalyByCategory[category] = new AnomalyAggregate(
                    current.Count + 1,
                    current.SampledCount + (sampleContext == null ? 0 : 1));
            }
        }

        public void LogCappedWarning(string category, Action logAction, object? sampleContext = null)
        {
            if (CappedWarnings.ShouldLog(category))
            {
                logAction();
                RecordAnomaly(category, sampleContext ?? "<sample>");
                return;
            }

            RecordAnomaly(category);
        }

        public void LogRunEnd(ILogger logger, bool succeeded, string? failureStage, string? failureCause)
        {
            var endUtc = DateTimeOffset.UtcNow;
            var duration = _stopwatch.Elapsed;
            var durationSeconds = Math.Max(duration.TotalSeconds, 0.001d);
            var safeDurationSeconds = Math.Max(durationSeconds, 0.001d);
            var pagesTotal = PagesTotal;
            var rawTotal = RawRevisionsTotal;
            var scopedTotal = ScopedRevisionsTotal;
            var emptyPagesTotal = PagesWithoutScopedRevisionsTotal;
            var requestCount = (int)Interlocked.Read(ref _httpRequestCount);
            var non200Count = (int)Interlocked.Read(ref _httpNon200Count);
            var totalHttpDurationMs = Interlocked.Read(ref _totalHttpDurationMs);

            string retriesSummary;
            string anomaliesSummary;
            int segmentsWithZeroScopedRevisions;
            lock (_sync)
            {
                var totalRetries = _retryByStage.Values.Sum(entry => entry.Count);
                var byStage = _retryByStage.Count == 0
                    ? "none"
                    : string.Join(" ", _retryByStage
                        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                        .Select(entry => $"{entry.Key}:{entry.Value.Count}"));
                retriesSummary = $"total={totalRetries} byStage={byStage}";

                anomaliesSummary = _anomalyByCategory.Count == 0
                    ? "none"
                    : string.Join(" ", _anomalyByCategory
                        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                        .Select(entry => $"{entry.Key}={entry.Value.Count} (sampled={entry.Value.SampledCount})"));

                segmentsWithZeroScopedRevisions = SegmentCount == 0
                    ? 0
                    : Math.Max(0, SegmentCount - _segmentsWithScopedRevisions.Count);
            }

            var failureSuffix = succeeded
                ? string.Empty
                : $" failureStage={failureStage ?? "unknown"} failureCause={failureCause ?? "unknown"}";

            logger.LogInformation(
                "REV_INGEST_RUN_END runId={RunId} succeeded={Succeeded} duration={Duration}{FailureSuffix}\n  scope: count={ScopeCount} min={ScopeMin} max={ScopeMax} segments={SegmentCount} totalSpan={TotalSegmentSpan} maxSegmentSpan={MaxSegmentSpan} segmentsWithZeroScopedRevisions={SegmentsWithZeroScopedRevisions}\n  odata: pages={PagesTotal} rawRevisions={RawRevisions} scopedRevisions={ScopedRevisions} pagesWithoutScoped={PagesWithoutScoped}\n  retries: {RetriesSummary}\n  anomalies: {AnomaliesSummary}\n  http: requestCount={RequestCount} non200={Non200} totalHttpDurationMs={TotalHttpDurationMs}\n  perf: rawRevPerSec={RawRate:F2} scopedRevPerSec={ScopedRate:F2} pagesPerSec={PagesRate:F2} startUtc={StartUtc} endUtc={EndUtc}",
                RunId,
                succeeded,
                duration,
                failureSuffix,
                ScopeCount,
                ScopeMin,
                ScopeMax,
                SegmentCount,
                TotalSegmentSpan,
                MaxSegmentSpan,
                segmentsWithZeroScopedRevisions,
                pagesTotal,
                rawTotal,
                scopedTotal,
                emptyPagesTotal,
                retriesSummary,
                anomaliesSummary,
                requestCount,
                non200Count,
                totalHttpDurationMs,
                rawTotal / safeDurationSeconds,
                scopedTotal / safeDurationSeconds,
                pagesTotal / safeDurationSeconds,
                StartUtc,
                endUtc);
        }
    }

    private sealed class IngestionHeartbeat
    {
        private readonly ILogger _logger;
        private readonly Guid _runId;
        private readonly TimeSpan _interval;
        private readonly int _pageInterval;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private TimeSpan _lastLoggedElapsed = TimeSpan.Zero;
        private int _lastLoggedPages;
        private int _lastLoggedRaw;

        public IngestionHeartbeat(
            ILogger logger,
            Guid runId,
            TimeSpan interval,
            int pageInterval)
        {
            _logger = logger;
            _runId = runId;
            _interval = interval;
            _pageInterval = Math.Max(1, pageInterval);
        }

        public void LogProgress(
            RevisionIngestionRunDiagnostics diagnostics,
            bool forced,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var nowElapsed = _stopwatch.Elapsed;
            var pages = diagnostics.PagesTotal;
            var raw = diagnostics.RawRevisionsTotal;
            if (!forced &&
                nowElapsed - _lastLoggedElapsed < _interval &&
                pages - _lastLoggedPages < _pageInterval)
            {
                return;
            }

            var deltaSeconds = Math.Max((nowElapsed - _lastLoggedElapsed).TotalSeconds, 0.001d);
            var safeDeltaSeconds = Math.Max(deltaSeconds, 0.001d);
            var deltaPages = pages - _lastLoggedPages;
            var deltaRaw = raw - _lastLoggedRaw;
            var segmentLabel = diagnostics.SegmentCount > 0
                ? $"{Math.Max(1, diagnostics.LastKnownSegmentIndex)}/{diagnostics.SegmentCount}"
                : "n/a";

            _logger.LogInformation(
                "REV_INGEST_PROGRESS runId={RunId} seg={Segment} pages={Pages} raw={Raw} scoped={Scoped} emptyPages={EmptyPages} elapsed={Elapsed} rateRaw={RateRaw:F1} rev/s ratePages={RatePages:F2} p/s",
                _runId,
                segmentLabel,
                pages,
                raw,
                diagnostics.ScopedRevisionsTotal,
                diagnostics.PagesWithoutScopedRevisionsTotal,
                nowElapsed,
                deltaRaw / safeDeltaSeconds,
                deltaPages / safeDeltaSeconds);

            _lastLoggedElapsed = nowElapsed;
            _lastLoggedPages = pages;
            _lastLoggedRaw = raw;
        }
    }

    private sealed class CappedWarningLogger
    {
        private readonly int _maxSamplesPerCategory;
        private readonly Dictionary<string, int> _categoryCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        public CappedWarningLogger(int maxSamplesPerCategory)
        {
            _maxSamplesPerCategory = Math.Max(1, maxSamplesPerCategory);
        }

        public bool ShouldLog(string category)
        {
            lock (_sync)
            {
                _categoryCounts.TryGetValue(category, out var current);
                current++;
                _categoryCounts[category] = current;
                return current <= _maxSamplesPerCategory;
            }
        }
    }

    private readonly record struct RetryAggregate(int Count, int MaxAttemptSeen, long TotalBackoffMs, long TotalRetryDurationMs);
    private readonly record struct AnomalyAggregate(int Count, int SampledCount);

    private sealed record CreationObservation(
        bool CreationObserved,
        DateTimeOffset EarliestChangedDateUtc);

    private sealed class PersistMetrics
    {
        public int RevisionHeaderCount { get; private set; }
        public int FieldDeltaCount { get; private set; }
        public int RelationDeltaCount { get; private set; }
        public int DuplicateCount { get; private set; }
        public int MissingRequiredCount { get; private set; }
        public int DbConstraintCount { get; set; }
        public long SaveChangesDurationMs { get; set; }
        public long? CommitDurationMs { get; set; }
        public long PersistDurationMs { get; set; }
        public bool TransactionUsed { get; set; }

        public void IncrementRevisionHeader() => RevisionHeaderCount++;
        public void IncrementFieldDelta() => FieldDeltaCount++;
        public void IncrementRelationDelta() => RelationDeltaCount++;
        public void IncrementDuplicate() => DuplicateCount++;
        public void IncrementMissingRequired() => MissingRequiredCount++;
    }

    private sealed record RevisionIngestionWindow(
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        int Depth,
        int Sequence);

    private sealed record WindowProcessingResult(
        WindowProcessingOutcome Outcome,
        int PagesProcessed,
        int PersistedCount,
        WindowStallReason? StallReason,
        ReportingRevisionsTermination? Termination,
        bool ScopedFieldDumpLogged,
        IReadOnlyCollection<int> PersistedWorkItemIds,
        DateTimeOffset? MinChangedDate,
        DateTimeOffset? MaxChangedDate,
        string? LastTokenHash);

    private sealed record WindowRunResult(
        int TotalPersisted,
        int PagesProcessed,
        int WindowsProcessed,
        int WindowsMarkedUnretrievable,
        bool BackfillComplete,
        bool ScopedFieldDumpLogged,
        ReportingRevisionsTermination? LastTermination,
        RevisionIngestionRunOutcome RunOutcome,
        int DistinctWorkItemCount,
        IReadOnlyCollection<int> DistinctWorkItemIds,
        DateTimeOffset? MinChangedDate,
        DateTimeOffset? MaxChangedDate,
        string? LastStableContinuationTokenHash,
        WindowStallReason? LastStallReason);

    private sealed record FallbackIngestionResult(
        bool Success,
        int PersistedCount,
        int DistinctWorkItemCount,
        IReadOnlyCollection<int> DistinctWorkItemIds,
        DateTimeOffset? MinChangedDate,
        DateTimeOffset? MaxChangedDate);

    private sealed record DatabaseRevisionSnapshot(
        int TotalRows,
        int DistinctWorkItemIds,
        DateTimeOffset? MinChangedDate,
        DateTimeOffset? MaxChangedDate);

    private enum WindowProcessingOutcome
    {
        CompletedNormally = 0,
        CompletedRawEmpty = 1,
        Stalled = 2,
        SplitScheduled = 3,
        MarkedUnretrievableAtMinimumChunk = 4
    }

    private enum WindowStallReason
    {
        TokenRepeated = 0,
        RawZeroWithHasMore = 1,
        TokenNotAdvancing = 2,
        MaxPageLimit = 3,
        ClientTermination = 4
    }

    private enum PagePosition
    {
        Unknown = 0,
        NewerThanWindow = 1,
        OverlapsWindow = 2,
        OlderThanWindow = 3
    }
}

public enum RevisionIngestionRunOutcome
{
    CompletedNormally = 0,
    CompletedWithPaginationAnomaly = 1,
    CompletedWithFallback = 2,
    Failed = 3
}

/// <summary>
/// Result of a revision ingestion operation.
/// </summary>
public record RevisionIngestionResult
{
    public RevisionIngestionRunOutcome RunOutcome { get; init; } = RevisionIngestionRunOutcome.CompletedNormally;
    public bool Success { get; init; }
    public bool HasWarnings { get; init; }
    public bool IsAlreadyRunning { get; init; }
    public bool WasCancelled { get; init; }
    public bool WasTerminatedEarly { get; init; }
    public int RevisionsIngested { get; init; }
    public int DistinctWorkItemsIngested { get; init; }
    public int PagesProcessed { get; init; }
    public bool FallbackUsed { get; init; }
    public DateTimeOffset? MinChangedDateIngested { get; init; }
    public DateTimeOffset? MaxChangedDateIngested { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public ReportingRevisionsTerminationReason? TerminationReason { get; init; }
    public string? TerminationMessage { get; init; }
    public bool BackfillComplete { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Progress update during revision ingestion.
/// </summary>
public record RevisionIngestionProgress
{
    public required string Stage { get; init; }
    public int PercentComplete { get; init; }
    public int RevisionsProcessed { get; init; }
    public int CurrentPage { get; init; }
    public bool IsComplete { get; init; }
}
