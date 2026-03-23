using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts pipeline runs from TFS.
/// </summary>
public class PipelineSyncStage : ISyncStage
{
    private const int MaxBuildQualityBuildBatchSize = 200;
    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly ILogger<PipelineSyncStage> _logger;

    public string StageName => "SyncPipelines";
    public int StageNumber => 8;

    public PipelineSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        ILogger<PipelineSyncStage> logger)
    {
        _tfsClient = tfsClient;
        _context = context;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.PipelineDefinitionIds.Length == 0)
            {
                _logger.LogInformation(
                    "PIPELINE_INGEST_STAGE_SKIP: ProductOwner {ProductOwnerId} — reason: no pipeline definitions configured",
                    context.ProductOwnerId);
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0);
            }

            progressCallback(0);

            _logger.LogInformation(
                "PIPELINE_INGEST_STAGE_START: ProductOwner {ProductOwnerId}, pipelineDefs={PipelineCount} [{PipelineIds}], " +
                "dateWindow from={FromDate} to=now",
                context.ProductOwnerId,
                context.PipelineDefinitionIds.Length,
                string.Join(", ", context.PipelineDefinitionIds),
                context.PipelineWatermark?.ToString("O") ?? "null (full sync)");

            // Fetch pipeline runs from TFS
            var pipelineRuns = await _tfsClient.GetPipelineRunsAsync(
                context.PipelineDefinitionIds,
                branchName: null,
                minStartTime: context.PipelineWatermark,
                top: 100,
                cancellationToken);

            var runList = pipelineRuns.ToList();

            _logger.LogInformation(
                "Fetched {Count} pipeline runs from TFS for ProductOwner {ProductOwnerId}",
                runList.Count,
                context.ProductOwnerId);

            var pipelineIdMapping = await _context.PipelineDefinitions
                .Where(pd => context.PipelineDefinitionIds.Contains(pd.PipelineDefinitionId))
                .ToDictionaryAsync(pd => pd.PipelineDefinitionId, pd => pd.Id, cancellationToken);

            progressCallback(80);

            DateTimeOffset? maxDate = null;
            if (runList.Count != 0)
            {
                // Upsert pipeline runs to database so the build anchor exists before child facts are processed.
                maxDate = await UpsertPipelineRunsAsync(runList, context.ProductOwnerId, pipelineIdMapping, progressCallback, cancellationToken);
            }

            var buildQualityResult = await SyncBuildQualityFactsAsync(
                context.ProductOwnerId,
                pipelineIdMapping.Values.ToArray(),
                runList,
                cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {Count} pipeline runs for ProductOwner {ProductOwnerId}, new watermark: {Watermark}",
                runList.Count,
                context.ProductOwnerId,
                maxDate?.ToString("O") ?? "none");

            return SyncStageResult.CreateSuccess(
                runList.Count,
                maxDate ?? context.PipelineWatermark,
                buildQualityResult.HasWarnings,
                buildQualityResult.WarningMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTimeOffset?> UpsertPipelineRunsAsync(
        List<PipelineRunDto> runs,
        int productOwnerId,
        Dictionary<int, int> pipelineIdMapping,
        Action<int> progressCallback,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        DateTimeOffset? maxDate = null;

        // Build unique key set for existing runs
        var runKeys = runs
            .Where(r => pipelineIdMapping.ContainsKey(r.PipelineId))
            .Select(r => (ProductOwnerId: productOwnerId, PipelineDefId: pipelineIdMapping[r.PipelineId], TfsRunId: r.RunId))
            .ToList();

        var existingKeys = await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .Select(r => new { r.PipelineDefinitionId, r.TfsRunId })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys.Select(k => (k.PipelineDefinitionId, k.TfsRunId)).ToHashSet();
        var totalBatches = (int)Math.Ceiling(runs.Count / (double)batchSize);
        var processedBatches = 0;

        foreach (var batch in runs.Chunk(batchSize))
        {
            // Get batch keys that should be updated
            var batchKeysToUpdate = batch
                .Where(dto => pipelineIdMapping.TryGetValue(dto.PipelineId, out _))
                .Select(dto => (PipelineDefId: pipelineIdMapping[dto.PipelineId], dto.RunId))
                .Where(k => existingSet.Contains(k))
                .ToList();

            // Load all existing entities for this batch in a single query
            var runIds = batchKeysToUpdate.Select(k => k.RunId).ToList();
            var existingEntities = await _context.CachedPipelineRuns
                .Where(r => r.ProductOwnerId == productOwnerId && runIds.Contains(r.TfsRunId))
                .ToDictionaryAsync(r => (r.PipelineDefinitionId, r.TfsRunId), cancellationToken);

            foreach (var dto in batch)
            {
                if (!pipelineIdMapping.TryGetValue(dto.PipelineId, out var internalPipelineDefId))
                {
                    continue; // Skip runs for unknown pipelines
                }

                // Track max finish date for watermark
                if (dto.FinishTime.HasValue && (maxDate == null || dto.FinishTime > maxDate))
                {
                    maxDate = dto.FinishTime;
                }

                var key = (internalPipelineDefId, dto.RunId);
                if (existingEntities.TryGetValue(key, out var entity))
                {
                    // Update existing
                    UpdateEntity(entity, dto);
                }
                else if (!existingSet.Contains(key))
                {
                    // Insert new
                    var newEntity = MapToEntity(dto, productOwnerId, internalPipelineDefId);
                    await _context.CachedPipelineRuns.AddAsync(newEntity, cancellationToken);
                    existingSet.Add(key);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            processedBatches++;
            var percent = 80 + (int)((processedBatches / (double)totalBatches) * 20);
            progressCallback(Math.Min(percent, 99));
        }

        return maxDate;
    }

    private async Task<BuildQualitySyncResult> SyncBuildQualityFactsAsync(
        int productOwnerId,
        IReadOnlyCollection<int> pipelineDefinitionIds,
        IReadOnlyCollection<PipelineRunDto> runs,
        CancellationToken cancellationToken)
    {
        var childIngestionStopwatch = Stopwatch.StartNew();
        var testRunRetrievalElapsedMs = 0L;
        var coverageRetrievalElapsedMs = 0L;
        var testRunPersistenceElapsedMs = 0L;
        var coveragePersistenceElapsedMs = 0L;
        var requestedTestRunBuildCount = 0;
        var requestedCoverageBuildCount = 0;
        var returnedTestRunDtoCount = 0;
        var returnedCoverageDtoCount = 0;
        var persistedTestRunRowCount = 0;
        var persistedCoverageRowCount = 0;
        var warningCount = 0;

        if (pipelineDefinitionIds.Count == 0)
        {
            LogBuildQualityChildIngestSummary(
                productOwnerId,
                childIngestionStopwatch.ElapsedMilliseconds,
                requestedTestRunBuildCount,
                requestedCoverageBuildCount,
                returnedTestRunDtoCount,
                returnedCoverageDtoCount,
                persistedTestRunRowCount,
                persistedCoverageRowCount,
                testRunRetrievalElapsedMs,
                coverageRetrievalElapsedMs,
                testRunPersistenceElapsedMs,
                coveragePersistenceElapsedMs,
                warningCount);
            return BuildQualitySyncResult.None;
        }

        var requestedRunIds = runs
            .Select(run => run.RunId)
            .Distinct()
            .ToArray();

        var requestedRunIdSet = requestedRunIds.ToHashSet();

        var scopedRuns = _context.CachedPipelineRuns.Where(run =>
            run.ProductOwnerId == productOwnerId &&
            pipelineDefinitionIds.Contains(run.PipelineDefinitionId));

        var missingTestRunTfsRunIds = await scopedRuns
            .Where(run =>
                !_context.TestRuns.Any(testRun => testRun.BuildId == run.Id))
            .Select(run => run.TfsRunId)
            .ToListAsync(cancellationToken);

        var missingCoverageTfsRunIds = await scopedRuns
            .Where(run =>
                !_context.Coverages.Any(coverage => coverage.BuildId == run.Id))
            .Select(run => run.TfsRunId)
            .ToListAsync(cancellationToken);

        var missingBuildRunIds = missingTestRunTfsRunIds
            .Concat(missingCoverageTfsRunIds)
            .Distinct()
            .OrderBy(runId => runId)
            .ToArray();

        var backfillRunIds = missingBuildRunIds
            .Where(runId => !requestedRunIdSet.Contains(runId))
            .Take(MaxBuildQualityBuildBatchSize)
            .ToArray();

        if (backfillRunIds.Length != 0)
        {
            _logger.LogInformation(
                "BuildQuality backfill: {MissingBuildCount} incomplete builds added to ingestion batch",
                backfillRunIds.Length);
        }

        var buildIds = requestedRunIds
            .Concat(backfillRunIds)
            .Distinct()
            .ToArray();

        if (buildIds.Length == 0)
        {
            LogBuildQualityChildIngestSummary(
                productOwnerId,
                childIngestionStopwatch.ElapsedMilliseconds,
                requestedTestRunBuildCount,
                requestedCoverageBuildCount,
                returnedTestRunDtoCount,
                returnedCoverageDtoCount,
                persistedTestRunRowCount,
                persistedCoverageRowCount,
                testRunRetrievalElapsedMs,
                coverageRetrievalElapsedMs,
                testRunPersistenceElapsedMs,
                coveragePersistenceElapsedMs,
                warningCount);
            return BuildQualitySyncResult.None;
        }

        var buildAnchors = await _context.CachedPipelineRuns
            .Where(run => run.ProductOwnerId == productOwnerId && buildIds.Contains(run.TfsRunId))
            .Select(run => new { run.Id, run.TfsRunId })
            .ToDictionaryAsync(run => run.TfsRunId, run => run.Id, cancellationToken);

        if (buildAnchors.Count == 0)
        {
            LogBuildQualityChildIngestSummary(
                productOwnerId,
                childIngestionStopwatch.ElapsedMilliseconds,
                requestedTestRunBuildCount,
                requestedCoverageBuildCount,
                returnedTestRunDtoCount,
                returnedCoverageDtoCount,
                persistedTestRunRowCount,
                persistedCoverageRowCount,
                testRunRetrievalElapsedMs,
                coverageRetrievalElapsedMs,
                testRunPersistenceElapsedMs,
                coveragePersistenceElapsedMs,
                warningCount);
            return BuildQualitySyncResult.None;
        }

        var childFetchBuildIds = buildAnchors.Keys.ToArray();
        requestedTestRunBuildCount = childFetchBuildIds.Length;
        requestedCoverageBuildCount = childFetchBuildIds.Length;
        var testRunRetrievalStopwatch = Stopwatch.StartNew();
        var testRunsTask = _tfsClient.GetTestRunsByBuildIdsAsync(childFetchBuildIds, cancellationToken);
        var coverageRetrievalStopwatch = Stopwatch.StartNew();
        var coverageTask = _tfsClient.GetCoverageByBuildIdsAsync(childFetchBuildIds, cancellationToken);

        await Task.WhenAll(testRunsTask, coverageTask);
        testRunRetrievalElapsedMs = testRunRetrievalStopwatch.ElapsedMilliseconds;
        coverageRetrievalElapsedMs = coverageRetrievalStopwatch.ElapsedMilliseconds;

        var testRuns = (await testRunsTask).ToList();
        var coverage = (await coverageTask).ToList();
        returnedTestRunDtoCount = testRuns.Count;
        returnedCoverageDtoCount = coverage.Count;

        _logger.LogInformation(
            "BUILDQUALITY_TESTRUN_RETRIEVAL_SUMMARY: productOwnerId={ProductOwnerId}, requestedBuildCount={RequestedBuildCount}, returnedDtoCount={ReturnedDtoCount}, elapsedMs={ElapsedMs}",
            productOwnerId,
            requestedTestRunBuildCount,
            returnedTestRunDtoCount,
            testRunRetrievalElapsedMs);
        _logger.LogInformation(
            "BUILDQUALITY_COVERAGE_RETRIEVAL_SUMMARY: productOwnerId={ProductOwnerId}, requestedBuildCount={RequestedBuildCount}, returnedDtoCount={ReturnedDtoCount}, elapsedMs={ElapsedMs}",
            productOwnerId,
            requestedCoverageBuildCount,
            returnedCoverageDtoCount,
            coverageRetrievalElapsedMs);

        var testRunPersistenceStopwatch = Stopwatch.StartNew();
        var testRunPersistence = await UpsertTestRunsAsync(buildAnchors, testRuns, cancellationToken);
        testRunPersistenceElapsedMs = testRunPersistenceStopwatch.ElapsedMilliseconds;
        warningCount += testRunPersistence.WarningCount;
        persistedTestRunRowCount = testRunPersistence.PersistedRowCount;

        var coveragePersistenceStopwatch = Stopwatch.StartNew();
        var coveragePersistence = await ReplaceCoverageAsync(buildAnchors, coverage, cancellationToken);
        coveragePersistenceElapsedMs = coveragePersistenceStopwatch.ElapsedMilliseconds;
        warningCount += coveragePersistence.WarningCount;
        persistedCoverageRowCount = coveragePersistence.PersistedRowCount;

        _logger.LogInformation(
            "BUILDQUALITY_TESTRUN_PERSISTENCE_SUMMARY: productOwnerId={ProductOwnerId}, persistedRowCount={PersistedRowCount}, insertedRowCount={InsertedRowCount}, updatedRowCount={UpdatedRowCount}, removedRowCount={RemovedRowCount}, warningCount={WarningCount}, elapsedMs={ElapsedMs}",
            productOwnerId,
            testRunPersistence.PersistedRowCount,
            testRunPersistence.InsertedRowCount,
            testRunPersistence.UpdatedRowCount,
            testRunPersistence.RemovedRowCount,
            testRunPersistence.WarningCount,
            testRunPersistenceElapsedMs);
        _logger.LogInformation(
            "BUILDQUALITY_COVERAGE_PERSISTENCE_SUMMARY: productOwnerId={ProductOwnerId}, persistedRowCount={PersistedRowCount}, insertedRowCount={InsertedRowCount}, removedRowCount={RemovedRowCount}, warningCount={WarningCount}, elapsedMs={ElapsedMs}",
            productOwnerId,
            coveragePersistence.PersistedRowCount,
            coveragePersistence.InsertedRowCount,
            coveragePersistence.RemovedRowCount,
            coveragePersistence.WarningCount,
            coveragePersistenceElapsedMs);

        _logger.LogInformation(
            "Build quality ingestion synced {TestRunCount} test runs and {CoverageCount} coverage rows for ProductOwner {ProductOwnerId}",
            testRuns.Count,
            coverage.Count,
            productOwnerId);
        LogBuildQualityChildIngestSummary(
            productOwnerId,
            childIngestionStopwatch.ElapsedMilliseconds,
            requestedTestRunBuildCount,
            requestedCoverageBuildCount,
            returnedTestRunDtoCount,
            returnedCoverageDtoCount,
            persistedTestRunRowCount,
            persistedCoverageRowCount,
            testRunRetrievalElapsedMs,
            coverageRetrievalElapsedMs,
            testRunPersistenceElapsedMs,
            coveragePersistenceElapsedMs,
            warningCount);

        return warningCount == 0
            ? BuildQualitySyncResult.None
            : new BuildQualitySyncResult(true, "Build quality ingestion skipped invalid or unlinked child records.");
    }

    private async Task<BuildQualityPersistenceResult> UpsertTestRunsAsync(
        IReadOnlyDictionary<int, int> buildAnchors,
        IReadOnlyCollection<TestRunDto> testRuns,
        CancellationToken cancellationToken)
    {
        var affectedBuildIds = buildAnchors.Values.ToArray();
        var existingEntities = await _context.TestRuns
            .Where(testRun => affectedBuildIds.Contains(testRun.BuildId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingEntities
            .Where(testRun => testRun.ExternalId.HasValue)
            .ToDictionary(testRun => (testRun.BuildId, testRun.ExternalId!.Value));

        var incomingKeys = new HashSet<(int BuildId, int ExternalId)>();
        var warningCount = 0;
        var now = DateTimeOffset.UtcNow;
        var insertedRowCount = 0;
        var updatedRowCount = 0;

        foreach (var dto in testRuns)
        {
            if (!buildAnchors.TryGetValue(dto.BuildId, out var internalBuildId))
            {
                warningCount += LogSkippedTestRun(dto, "missing build linkage");
                continue;
            }

            if (dto.ExternalId is null)
            {
                warningCount += LogSkippedTestRun(dto, "missing stable external id");
                continue;
            }

            if (dto.TotalTests < 0 || dto.PassedTests < 0 || dto.NotApplicableTests < 0)
            {
                warningCount += LogSkippedTestRun(dto, "negative raw counters");
                continue;
            }

            var key = (internalBuildId, dto.ExternalId.Value);
            incomingKeys.Add(key);

            if (existingByKey.TryGetValue(key, out var entity))
            {
                entity.TotalTests = dto.TotalTests;
                entity.PassedTests = dto.PassedTests;
                entity.NotApplicableTests = dto.NotApplicableTests;
                entity.Timestamp = dto.Timestamp?.UtcDateTime;
                entity.CachedAt = now;
                updatedRowCount++;
            }
            else
            {
                await _context.TestRuns.AddAsync(new TestRunEntity
                {
                    BuildId = internalBuildId,
                    ExternalId = dto.ExternalId,
                    TotalTests = dto.TotalTests,
                    PassedTests = dto.PassedTests,
                    NotApplicableTests = dto.NotApplicableTests,
                    Timestamp = dto.Timestamp?.UtcDateTime,
                    CachedAt = now
                }, cancellationToken);
                insertedRowCount++;
            }
        }

        var staleEntities = existingEntities
            .Where(entity => !entity.ExternalId.HasValue || !incomingKeys.Contains((entity.BuildId, entity.ExternalId.Value)))
            .ToList();

        if (staleEntities.Count != 0)
        {
            _context.TestRuns.RemoveRange(staleEntities);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new BuildQualityPersistenceResult(
            warningCount,
            incomingKeys.Count,
            insertedRowCount,
            updatedRowCount,
            staleEntities.Count);
    }

    private async Task<BuildQualityPersistenceResult> ReplaceCoverageAsync(
        IReadOnlyDictionary<int, int> buildAnchors,
        IReadOnlyCollection<CoverageDto> coverageRows,
        CancellationToken cancellationToken)
    {
        var affectedBuildIds = buildAnchors.Values.ToArray();
        var existingEntities = await _context.Coverages
            .Where(coverage => affectedBuildIds.Contains(coverage.BuildId))
            .ToListAsync(cancellationToken);

        if (existingEntities.Count != 0)
        {
            _context.Coverages.RemoveRange(existingEntities);
        }

        var warningCount = 0;
        var now = DateTimeOffset.UtcNow;
        var insertedRowCount = 0;

        foreach (var dto in coverageRows)
        {
            if (!buildAnchors.TryGetValue(dto.BuildId, out var internalBuildId))
            {
                warningCount += LogSkippedCoverage(dto, "missing build linkage");
                continue;
            }

            if (dto.CoveredLines < 0 || dto.TotalLines < 0)
            {
                warningCount += LogSkippedCoverage(dto, "negative raw counters");
                continue;
            }

            await _context.Coverages.AddAsync(new CoverageEntity
            {
                BuildId = internalBuildId,
                CoveredLines = dto.CoveredLines,
                TotalLines = dto.TotalLines,
                Timestamp = dto.Timestamp?.UtcDateTime,
                CachedAt = now
            }, cancellationToken);
            insertedRowCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new BuildQualityPersistenceResult(
            warningCount,
            insertedRowCount,
            insertedRowCount,
            0,
            existingEntities.Count);
    }

    private int LogSkippedTestRun(TestRunDto dto, string reason)
    {
        _logger.LogWarning(
            "Skipping invalid test run fact for external build {BuildId} and external test run {ExternalId}: {Reason}",
            dto.BuildId,
            dto.ExternalId,
            reason);
        return 1;
    }

    private int LogSkippedCoverage(CoverageDto dto, string reason)
    {
        _logger.LogWarning(
            "Skipping invalid coverage fact for external build {BuildId}: {Reason}",
            dto.BuildId,
            reason);
        return 1;
    }

    private void LogBuildQualityChildIngestSummary(
        int productOwnerId,
        long childIngestionElapsedMs,
        int requestedTestRunBuildCount,
        int requestedCoverageBuildCount,
        int returnedTestRunDtoCount,
        int returnedCoverageDtoCount,
        int persistedTestRunRowCount,
        int persistedCoverageRowCount,
        long testRunRetrievalElapsedMs,
        long coverageRetrievalElapsedMs,
        long testRunPersistenceElapsedMs,
        long coveragePersistenceElapsedMs,
        int warningCount)
    {
        _logger.LogInformation(
            "BUILDQUALITY_CHILD_INGEST_SUMMARY: productOwnerId={ProductOwnerId}, childIngestionElapsedMs={ChildIngestionElapsedMs}, requestedTestRunBuildCount={RequestedTestRunBuildCount}, requestedCoverageBuildCount={RequestedCoverageBuildCount}, returnedTestRunDtoCount={ReturnedTestRunDtoCount}, returnedCoverageDtoCount={ReturnedCoverageDtoCount}, persistedTestRunRowCount={PersistedTestRunRowCount}, persistedCoverageRowCount={PersistedCoverageRowCount}, testRunRetrievalElapsedMs={TestRunRetrievalElapsedMs}, coverageRetrievalElapsedMs={CoverageRetrievalElapsedMs}, testRunPersistenceElapsedMs={TestRunPersistenceElapsedMs}, coveragePersistenceElapsedMs={CoveragePersistenceElapsedMs}, warningCount={WarningCount}",
            productOwnerId,
            childIngestionElapsedMs,
            requestedTestRunBuildCount,
            requestedCoverageBuildCount,
            returnedTestRunDtoCount,
            returnedCoverageDtoCount,
            persistedTestRunRowCount,
            persistedCoverageRowCount,
            testRunRetrievalElapsedMs,
            coverageRetrievalElapsedMs,
            testRunPersistenceElapsedMs,
            coveragePersistenceElapsedMs,
            warningCount);
    }

    private static void UpdateEntity(CachedPipelineRunEntity entity, PipelineRunDto dto)
    {
        entity.RunName = dto.PipelineName;
        entity.State = dto.FinishTime.HasValue ? "completed" : "running";
        entity.Result = dto.Result.ToString();
        entity.CreatedDate = dto.StartTime;
        entity.CreatedDateUtc = dto.StartTime?.UtcDateTime;
        entity.FinishedDate = dto.FinishTime;
        entity.FinishedDateUtc = dto.FinishTime?.UtcDateTime;
        entity.SourceBranch = dto.Branch;
        entity.CachedAt = DateTimeOffset.UtcNow;
    }

    private static CachedPipelineRunEntity MapToEntity(PipelineRunDto dto, int productOwnerId, int pipelineDefId)
    {
        return new CachedPipelineRunEntity
        {
            ProductOwnerId = productOwnerId,
            PipelineDefinitionId = pipelineDefId,
            TfsRunId = dto.RunId,
            RunName = dto.PipelineName,
            State = dto.FinishTime.HasValue ? "completed" : "running",
            Result = dto.Result.ToString(),
            CreatedDate = dto.StartTime,
            CreatedDateUtc = dto.StartTime?.UtcDateTime,
            FinishedDate = dto.FinishTime,
            FinishedDateUtc = dto.FinishTime?.UtcDateTime,
            SourceBranch = dto.Branch,
            CachedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record BuildQualitySyncResult(bool HasWarnings, string? WarningMessage)
    {
        public static BuildQualitySyncResult None { get; } = new(false, null);
    }

    private sealed record BuildQualityPersistenceResult(
        int WarningCount,
        int PersistedRowCount,
        int InsertedRowCount,
        int UpdatedRowCount,
        int RemovedRowCount);
}
