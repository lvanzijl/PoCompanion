using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEpicCompletionForecastQuery.
/// Reads persisted forecast projections and maps them to the shared DTO.
/// </summary>
public sealed class GetEpicCompletionForecastQueryHandler
    : IQueryHandler<GetEpicCompletionForecastQuery, EpicCompletionForecastDto?>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly PoToolDbContext _context;
    private readonly ILogger<GetEpicCompletionForecastQueryHandler> _logger;

    public GetEpicCompletionForecastQueryHandler(
        PoToolDbContext context,
        ILogger<GetEpicCompletionForecastQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask<EpicCompletionForecastDto?> Handle(
        GetEpicCompletionForecastQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEpicCompletionForecastQuery for Epic: {EpicId}", query.EpicId);

        var workItem = await _context.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TfsId == query.EpicId, cancellationToken);

        if (workItem == null)
        {
            _logger.LogDebug("Epic not found: {EpicId}", query.EpicId);
            return null;
        }

        var projectionEntity = await _context.ForecastProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.WorkItemId == query.EpicId, cancellationToken);

        if (projectionEntity == null)
        {
            _logger.LogDebug("No persisted forecast projection found for Epic: {EpicId}", query.EpicId);
            return null;
        }

        var selectedVariant = SelectVariant(projectionEntity.ProjectionVariantsJson, query.MaxSprintsForVelocity ?? 5);
        if (selectedVariant == null)
        {
            _logger.LogWarning("Persisted forecast projection for Epic {EpicId} did not contain a usable variant.", query.EpicId);
            return null;
        }

        return new EpicCompletionForecastDto(
            EpicId: workItem.TfsId,
            Title: workItem.Title,
            Type: workItem.Type,
            TotalStoryPoints: selectedVariant.TotalScopeStoryPoints,
            DoneStoryPoints: selectedVariant.CompletedScopeStoryPoints,
            RemainingStoryPoints: selectedVariant.RemainingScopeStoryPoints,
            EstimatedVelocity: selectedVariant.EstimatedVelocity,
            SprintsRemaining: selectedVariant.SprintsRemaining,
            EstimatedCompletionDate: selectedVariant.EstimatedCompletionDate,
            Confidence: MapConfidence(selectedVariant.Confidence),
            ForecastByDate: selectedVariant.ForecastByDate
                .Select(projection => new SprintForecast(
                    projection.SprintName,
                    projection.IterationPath,
                    projection.SprintStartDate,
                    projection.SprintEndDate,
                    projection.ExpectedCompletedStoryPoints,
                    projection.RemainingStoryPointsAfterSprint,
                    projection.ProgressPercentage))
                .ToList(),
            AreaPath: workItem.AreaPath ?? "Unknown",
            AnalysisTimestamp: selectedVariant.LastUpdated);
    }

    private static StoredForecastProjectionVariant? SelectVariant(string variantsJson, int maxSprintsForVelocity)
    {
        if (string.IsNullOrWhiteSpace(variantsJson))
        {
            return null;
        }

        var variants = JsonSerializer.Deserialize<List<StoredForecastProjectionVariant>>(variantsJson, SerializerOptions);
        if (variants == null || variants.Count == 0)
        {
            return null;
        }

        return variants
            .OrderBy(variant => variant.MaxSprintsForVelocity)
            .FirstOrDefault(variant => variant.MaxSprintsForVelocity == maxSprintsForVelocity)
            ?? variants[^1];
    }

    private static ForecastConfidence MapConfidence(PoTool.Core.Domain.Forecasting.Models.ForecastConfidenceLevel confidence)
    {
        return confidence switch
        {
            PoTool.Core.Domain.Forecasting.Models.ForecastConfidenceLevel.Low => ForecastConfidence.Low,
            PoTool.Core.Domain.Forecasting.Models.ForecastConfidenceLevel.Medium => ForecastConfidence.Medium,
            _ => ForecastConfidence.High
        };
    }
}
