using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core.Configuration;

namespace PoTool.Integrations.Tfs.Diagnostics;

/// <summary>
/// Diagnostic logging helper for revision ingestion performance tracing.
/// </summary>
public sealed class RevisionIngestionDiagnostics
{
    private static readonly AsyncLocal<RevisionIngestionRunContext?> CurrentRun = new();
    private static readonly NullScope NullScopeInstance = new();

    private readonly ILogger<RevisionIngestionDiagnostics> _logger;
    private readonly IOptionsMonitor<RevisionIngestionDiagnosticsOptions> _optionsMonitor;

    private static readonly Action<ILogger, bool, DateTimeOffset?, DateTimeOffset, int, int, int, Exception?> RunStartLog =
        LoggerMessage.Define<bool, DateTimeOffset?, DateTimeOffset, int, int, int>(
            LogLevel.Information,
            new EventId(6200, nameof(RunStartLog)),
            "Revision ingestion run started. IsBackfill={IsBackfill} StartDateTime={StartDateTime} StartUtc={StartUtc} ReadConcurrency={ReadConcurrency} WriteConcurrency={WriteConcurrency} HydrationConcurrency={HydrationConcurrency}");

    private static readonly Action<ILogger, string, string?, Exception?> RunDatabaseLog =
        LoggerMessage.Define<string, string?>(
            LogLevel.Information,
            new EventId(6201, nameof(RunDatabaseLog)),
            "Revision ingestion database context. DbProvider={DbProvider} DbConnectionMode={DbConnectionMode}");

    private static readonly Action<ILogger, bool, int, long?, int?, long?, long?, Exception?> PageRequestLog =
        LoggerMessage.Define<bool, int, long?, int?, long?, long?>(
            LogLevel.Information,
            new EventId(6202, nameof(PageRequestLog)),
            "Revision ingestion page request. ContinuationTokenPresent={ContinuationTokenPresent} ContinuationTokenLength={ContinuationTokenLength} HttpDurationMs={HttpDurationMs} HttpStatusCode={HttpStatusCode} ParseDurationMs={ParseDurationMs} TransformDurationMs={TransformDurationMs}");

    private static readonly Action<ILogger, long, long, bool, bool, bool, long, Exception?> PagePersistLog =
        LoggerMessage.Define<long, long, bool, bool, bool, long>(
            LogLevel.Information,
            new EventId(6203, nameof(PagePersistLog)),
            "Revision ingestion page persistence. PersistDurationMs={PersistDurationMs} TotalPageDurationMs={TotalPageDurationMs} PageSlow={PageSlow} HttpSlow={HttpSlow} DbSlow={DbSlow} MemoryBytes={MemoryBytes}");

    private static readonly Action<ILogger, int, int, int, int, int, int, Exception?> PageCountsLog =
        LoggerMessage.Define<int, int, int, int, int, int>(
            LogLevel.Information,
            new EventId(6204, nameof(PageCountsLog)),
            "Revision ingestion page counts. RawRevisionCount={RawRevisionCount} ScopedRevisionCount={ScopedRevisionCount} DistinctWorkItemCount={DistinctWorkItemCount} RevisionHeaderCount={RevisionHeaderCount} FieldDeltaCount={FieldDeltaCount} RelationDeltaCount={RelationDeltaCount}");

    private static readonly Action<ILogger, long, int, int, int, Exception?> SaveChangesLog =
        LoggerMessage.Define<long, int, int, int>(
            LogLevel.Information,
            new EventId(6205, nameof(SaveChangesLog)),
            "Revision ingestion SaveChanges details. SaveChangesDurationMs={SaveChangesDurationMs} RevisionHeaderCount={RevisionHeaderCount} FieldDeltaCount={FieldDeltaCount} RelationDeltaCount={RelationDeltaCount}");

    private static readonly Action<ILogger, long, int, int, int, int, Exception?> GcStatsLog =
        LoggerMessage.Define<long, int, int, int, int>(
            LogLevel.Information,
            new EventId(6210, nameof(GcStatsLog)),
            "Revision ingestion GC stats. MemoryBytes={MemoryBytes} Gen0Count={Gen0Count} Gen1Count={Gen1Count} Gen2Count={Gen2Count} TrackedEntries={TrackedEntries}");

    private static readonly Action<ILogger, int, int, long, double, int, int, Exception?> RelationHydrationSummaryLog =
        LoggerMessage.Define<int, int, long, double, int, int>(
            LogLevel.Information,
            new EventId(6206, nameof(RelationHydrationSummaryLog)),
            "Relation hydration summary. WorkItemCount={WorkItemCount} CallCount={CallCount} TotalDurationMs={TotalDurationMs} AvgCallMs={AvgCallMs} RelationDeltasWritten={RelationDeltasWritten} WorkItemsSkipped={WorkItemsSkipped}");

    private static readonly Action<ILogger, int, int, bool, long, int, Exception?> RelationWorkItemLog =
        LoggerMessage.Define<int, int, bool, long, int>(
            LogLevel.Information,
            new EventId(6207, nameof(RelationWorkItemLog)),
            "Relation hydration work item. WorkItemId={WorkItemId} RevisionsFetched={RevisionsFetched} RelationsPresent={RelationsPresent} DurationMs={DurationMs} DeltasProduced={DeltasProduced}");

    private static readonly Action<ILogger, string, int, int, double, int?, long, Exception?> RetryAttemptLog =
        LoggerMessage.Define<string, int, int, double, int?, long>(
            LogLevel.Warning,
            new EventId(6208, nameof(RetryAttemptLog)),
            "Revision ingestion retry. Stage={Stage} Attempt={Attempt} MaxRetries={MaxRetries} BackoffMs={BackoffMs} StatusCode={StatusCode} DurationMs={DurationMs}");

    private static readonly Action<ILogger, string, long, Exception?> ThrottleWaitLog =
        LoggerMessage.Define<string, long>(
            LogLevel.Debug,
            new EventId(6209, nameof(ThrottleWaitLog)),
            "Revision ingestion throttle wait. Operation={Operation} WaitMs={WaitMs}");

    public RevisionIngestionDiagnostics(
        ILogger<RevisionIngestionDiagnostics> logger,
        IOptionsMonitor<RevisionIngestionDiagnosticsOptions> optionsMonitor)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public IDisposable StartRun(
        int productOwnerId,
        bool isBackfill,
        DateTimeOffset? startDateTime,
        DateTimeOffset startUtc,
        int readConcurrency,
        int writeConcurrency,
        int hydrationConcurrency,
        out RevisionIngestionRunContext runContext)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!IsEnabled(options) || !ShouldSample(options.SampleRate))
        {
            runContext = default;
            return NullScopeInstance;
        }

        runContext = new RevisionIngestionRunContext(
            Guid.NewGuid(),
            productOwnerId,
            options.LogPerPageSummary,
            options.LogPerWorkItemHydration,
            options.LogEfSaveChangesDetails,
            options.LogGcStatsEveryNPages,
            options.SlowPageThresholdMs,
            options.SlowDbThresholdMs,
            options.SlowHttpThresholdMs,
            options.MaxParseWarningsPerPage);

        var previous = CurrentRun.Value;
        CurrentRun.Value = runContext;

        var scope = _logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>("RunId", runContext.RunId),
            new KeyValuePair<string, object?>("ProductOwnerId", productOwnerId)
        }) ?? NullScopeInstance;

        RunStartLog(_logger, isBackfill, startDateTime, startUtc, readConcurrency, writeConcurrency, hydrationConcurrency, null);

        return new RunScope(scope, previous);
    }

    public void LogRunDatabase(RevisionIngestionRunContext runContext, string dbProvider, string? connectionMode)
    {
        if (!runContext.IsEnabled)
        {
            return;
        }

        RunDatabaseLog(_logger, dbProvider, connectionMode, null);
    }

    public bool TryGetCurrentRun(out RevisionIngestionRunContext runContext)
    {
        if (CurrentRun.Value is { IsEnabled: true } current)
        {
            runContext = current;
            return true;
        }

        runContext = default;
        return false;
    }

    public IDisposable BeginPageScope(RevisionIngestionRunContext runContext, int pageIndex)
    {
        if (!runContext.IsEnabled)
        {
            return NullScopeInstance;
        }

        return _logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>("PageIndex", pageIndex)
        }) ?? NullScopeInstance;
    }

    public void LogPageRequest(
        RevisionIngestionRunContext runContext,
        bool continuationTokenPresent,
        int continuationTokenLength,
        long? httpDurationMs,
        int? httpStatusCode,
        long? parseDurationMs,
        long? transformDurationMs)
    {
        if (!runContext.IsEnabled || !runContext.LogPerPageSummary)
        {
            return;
        }

        PageRequestLog(_logger, continuationTokenPresent, continuationTokenLength, httpDurationMs, httpStatusCode, parseDurationMs, transformDurationMs, null);
    }

    public void LogPagePersistence(
        RevisionIngestionRunContext runContext,
        long persistDurationMs,
        long totalPageDurationMs,
        bool pageSlow,
        bool httpSlow,
        bool dbSlow,
        long memoryBytes)
    {
        if (!runContext.IsEnabled || !runContext.LogPerPageSummary)
        {
            return;
        }

        PagePersistLog(_logger, persistDurationMs, totalPageDurationMs, pageSlow, httpSlow, dbSlow, memoryBytes, null);
    }

    public void LogPageCounts(
        RevisionIngestionRunContext runContext,
        int rawRevisionCount,
        int scopedRevisionCount,
        int distinctWorkItemCount,
        int revisionHeaderCount,
        int fieldDeltaCount,
        int relationDeltaCount)
    {
        if (!runContext.IsEnabled || !runContext.LogPerPageSummary)
        {
            return;
        }

        PageCountsLog(
            _logger,
            rawRevisionCount,
            scopedRevisionCount,
            distinctWorkItemCount,
            revisionHeaderCount,
            fieldDeltaCount,
            relationDeltaCount,
            null);
    }

    public void LogPagePagination(
        RevisionIngestionRunContext runContext,
        int pageIndex,
        int revisionCount,
        bool hasMoreResults,
        bool continuationTokenPresent,
        string? continuationTokenHash,
        bool tokenAdvanced,
        int consecutiveEmptyPages,
        int consecutiveSameTokenPages)
    {
        if (!runContext.IsEnabled || !runContext.LogPerPageSummary)
        {
            return;
        }

        _logger.LogInformation(
            "Revision ingestion page pagination. PageIndex={PageIndex} RevisionCount={RevisionCount} HasMoreResults={HasMoreResults} ContinuationTokenPresent={ContinuationTokenPresent} ContinuationTokenHash={ContinuationTokenHash} TokenAdvanced={TokenAdvanced} ConsecutiveEmptyPages={ConsecutiveEmptyPages} ConsecutiveSameTokenPages={ConsecutiveSameTokenPages}",
            pageIndex,
            revisionCount,
            hasMoreResults,
            continuationTokenPresent,
            continuationTokenHash,
            tokenAdvanced,
            consecutiveEmptyPages,
            consecutiveSameTokenPages);
    }

    public void LogSaveChangesDetails(
        RevisionIngestionRunContext runContext,
        long saveChangesDurationMs,
        int revisionHeaderCount,
        int fieldDeltaCount,
        int relationDeltaCount)
    {
        if (!runContext.IsEnabled || !runContext.LogEfSaveChangesDetails)
        {
            return;
        }

        SaveChangesLog(_logger, saveChangesDurationMs, revisionHeaderCount, fieldDeltaCount, relationDeltaCount, null);
    }

    public void LogGcStats(
        RevisionIngestionRunContext runContext,
        long memoryBytes,
        int gen0Count,
        int gen1Count,
        int gen2Count,
        int trackedEntries)
    {
        if (!runContext.IsEnabled || runContext.LogGcStatsEveryNPages <= 0)
        {
            return;
        }

        GcStatsLog(_logger, memoryBytes, gen0Count, gen1Count, gen2Count, trackedEntries, null);
    }

    public void LogRelationHydrationSummary(
        RevisionIngestionRunContext runContext,
        int workItemCount,
        int callCount,
        long totalDurationMs,
        double avgCallMs,
        int relationDeltasWritten,
        int workItemsSkipped)
    {
        if (!runContext.IsEnabled)
        {
            return;
        }

        RelationHydrationSummaryLog(
            _logger,
            workItemCount,
            callCount,
            totalDurationMs,
            avgCallMs,
            relationDeltasWritten,
            workItemsSkipped,
            null);
    }

    public void LogRelationWorkItem(
        RevisionIngestionRunContext runContext,
        int workItemId,
        int revisionsFetched,
        bool relationsPresent,
        long durationMs,
        int deltasProduced)
    {
        if (!runContext.IsEnabled || !runContext.LogPerWorkItemHydration)
        {
            return;
        }

        RelationWorkItemLog(_logger, workItemId, revisionsFetched, relationsPresent, durationMs, deltasProduced, null);
    }

    public void LogRetryAttempt(
        RevisionIngestionRunContext runContext,
        string stage,
        int attempt,
        int maxRetries,
        double backoffMs,
        int? statusCode,
        long durationMs,
        Exception exception)
    {
        if (!runContext.IsEnabled)
        {
            return;
        }

        RetryAttemptLog(_logger, stage, attempt, maxRetries, backoffMs, statusCode, durationMs, exception);
    }

    public void LogThrottleWait(
        RevisionIngestionRunContext runContext,
        string operation,
        long waitMs)
    {
        if (!runContext.IsEnabled)
        {
            return;
        }

        ThrottleWaitLog(_logger, operation, waitMs, null);
    }

    public int GetMaxParseWarningsPerPage()
    {
        return Math.Max(0, _optionsMonitor.CurrentValue.MaxParseWarningsPerPage);
    }

    public static long GetElapsedMilliseconds(long startTimestamp)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        return (long)(elapsedTicks * 1000d / Stopwatch.Frequency);
    }

    private static bool IsEnabled(RevisionIngestionDiagnosticsOptions options)
    {
        return options.Enabled && options.SampleRate > 0d;
    }

    private static bool ShouldSample(double sampleRate)
    {
        var normalized = Math.Clamp(sampleRate, 0d, 1d);
        if (normalized <= 0d)
        {
            return false;
        }

        if (normalized >= 1d)
        {
            return true;
        }

        var threshold = (int)(normalized * int.MaxValue);
        return RandomNumberGenerator.GetInt32(0, int.MaxValue) < threshold;
    }

    private sealed class RunScope : IDisposable
    {
        private readonly IDisposable _scope;
        private readonly RevisionIngestionRunContext? _previous;
        private bool _disposed;

        public RunScope(IDisposable scope, RevisionIngestionRunContext? previous)
        {
            _scope = scope;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scope.Dispose();
            CurrentRun.Value = _previous;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public readonly record struct RevisionIngestionRunContext
{
    public RevisionIngestionRunContext(
        Guid runId,
        int productOwnerId,
        bool logPerPageSummary,
        bool logPerWorkItemHydration,
        bool logEfSaveChangesDetails,
        int logGcStatsEveryNPages,
        int slowPageThresholdMs,
        int slowDbThresholdMs,
        int slowHttpThresholdMs,
        int maxParseWarningsPerPage)
    {
        RunId = runId;
        ProductOwnerId = productOwnerId;
        LogPerPageSummary = logPerPageSummary;
        LogPerWorkItemHydration = logPerWorkItemHydration;
        LogEfSaveChangesDetails = logEfSaveChangesDetails;
        LogGcStatsEveryNPages = logGcStatsEveryNPages;
        SlowPageThresholdMs = slowPageThresholdMs;
        SlowDbThresholdMs = slowDbThresholdMs;
        SlowHttpThresholdMs = slowHttpThresholdMs;
        MaxParseWarningsPerPage = maxParseWarningsPerPage;
        IsEnabled = true;
    }

    public Guid RunId { get; }
    public int ProductOwnerId { get; }
    public bool LogPerPageSummary { get; }
    public bool LogPerWorkItemHydration { get; }
    public bool LogEfSaveChangesDetails { get; }
    public int LogGcStatsEveryNPages { get; }
    public int SlowPageThresholdMs { get; }
    public int SlowDbThresholdMs { get; }
    public int SlowHttpThresholdMs { get; }
    public int MaxParseWarningsPerPage { get; }
    public bool IsEnabled { get; }
}
