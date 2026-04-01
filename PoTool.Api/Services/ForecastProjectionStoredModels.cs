using PoTool.Core.Domain.Forecasting.Models;

namespace PoTool.Api.Services;

internal sealed record StoredForecastProjectionVariant(
    int MaxSprintsForVelocity,
    int WorkItemId,
    string WorkItemType,
    double TotalScopeStoryPoints,
    double CompletedScopeStoryPoints,
    double RemainingScopeStoryPoints,
    double EstimatedVelocity,
    int SprintsRemaining,
    DateTimeOffset? EstimatedCompletionDate,
    ForecastConfidenceLevel Confidence,
    DateTimeOffset LastUpdated,
    IReadOnlyList<CompletionProjection> ForecastByDate);
