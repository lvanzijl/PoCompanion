using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for SplitEpicCommand.
/// Splits an Epic into two Epics by creating a new Epic in TFS.
/// This is the only TFS write operation allowed from the Release Planning Board.
/// </summary>
public sealed class SplitEpicCommandHandler : ICommandHandler<SplitEpicCommand, EpicSplitResultDto>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IReleasePlanningRepository _planningRepository;
    private readonly ILogger<SplitEpicCommandHandler> _logger;

    public SplitEpicCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository workItemRepository,
        IReleasePlanningRepository planningRepository,
        ILogger<SplitEpicCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _workItemRepository = workItemRepository;
        _planningRepository = planningRepository;
        _logger = logger;
    }

    public async ValueTask<EpicSplitResultDto> Handle(
        SplitEpicCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling SplitEpicCommand for Epic {EpicId}", command.OriginalEpicId);

        try
        {
            // 1. Validate the original Epic exists
            var originalEpic = await _workItemRepository.GetByTfsIdAsync(command.OriginalEpicId, cancellationToken);
            if (originalEpic == null)
            {
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = $"Epic with ID {command.OriginalEpicId} not found",
                    OriginalEpicId = command.OriginalEpicId
                };
            }

            if (!originalEpic.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase))
            {
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = $"Work item {command.OriginalEpicId} is not an Epic",
                    OriginalEpicId = command.OriginalEpicId
                };
            }

            // 2. Validate extracted epic title
            if (string.IsNullOrWhiteSpace(command.ExtractedEpicTitle))
            {
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = "Extracted Epic title is required",
                    OriginalEpicId = command.OriginalEpicId
                };
            }

            // 3. Validate features to extract
            if (command.FeatureIdsForExtractedEpic.Count == 0)
            {
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = "At least one Feature must be selected for the extracted Epic",
                    OriginalEpicId = command.OriginalEpicId
                };
            }

            // 4. Get all Features of the original Epic
            var allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
            var epicFeatures = allWorkItems
                .Where(w => w.ParentTfsId == command.OriginalEpicId 
                            && w.Type.Equals("Feature", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Validate all selected features belong to this Epic
            var extractedFeatureIds = command.FeatureIdsForExtractedEpic.ToHashSet();
            var invalidIds = extractedFeatureIds.Except(epicFeatures.Select(f => f.TfsId)).ToList();
            if (invalidIds.Count > 0)
            {
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = $"Features {string.Join(", ", invalidIds)} do not belong to Epic {command.OriginalEpicId}",
                    OriginalEpicId = command.OriginalEpicId
                };
            }

            // 5. Calculate effort distribution
            var (originalEffort, extractedEffort) = CalculateEffortDistribution(
                originalEpic.Effort ?? 0,
                epicFeatures,
                extractedFeatureIds);

            // 6. Create the new Epic in TFS
            // Note: This is a placeholder - actual TFS creation would use the TFS client
            // The TFS client interface would need to support creating work items
            _logger.LogInformation(
                "Epic split requested: Original Epic {OriginalId} ({OriginalTitle}) -> Extracted Epic ({ExtractedTitle}). " +
                "Features to move: {FeatureCount}. Effort distribution: {OriginalEffort}/{ExtractedEffort}",
                command.OriginalEpicId,
                originalEpic.Title,
                command.ExtractedEpicTitle,
                command.FeatureIdsForExtractedEpic.Count,
                originalEffort,
                extractedEffort);

            // For now, return a "not implemented" result since TFS creation is not yet implemented
            // In a full implementation, we would:
            // 1. Create the new Epic in TFS with the same parent Objective
            // 2. Update the Features to point to the new Epic
            // 3. Update effort values on both Epics
            // 4. Sync the changes back to the local database

            return new EpicSplitResultDto
            {
                Success = false,
                ErrorMessage = "Epic split is not yet available. This feature requires TFS write access which is coming in a future update. The requested split has been validated successfully.",
                OriginalEpicId = command.OriginalEpicId,
                OriginalEpicNewEffort = originalEffort,
                ExtractedEpicEffort = extractedEffort
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting Epic {EpicId}", command.OriginalEpicId);
            return new EpicSplitResultDto
            {
                Success = false,
                ErrorMessage = $"Error splitting Epic: {ex.Message}",
                OriginalEpicId = command.OriginalEpicId
            };
        }
    }

    /// <summary>
    /// Calculates effort distribution between original and extracted Epics.
    /// </summary>
    private static (int OriginalEffort, int ExtractedEffort) CalculateEffortDistribution(
        int totalEpicEffort,
        List<Core.WorkItems.WorkItemDto> allFeatures,
        HashSet<int> extractedFeatureIds)
    {
        if (totalEpicEffort == 0)
        {
            return (0, 0);
        }

        var originalFeatures = allFeatures.Where(f => !extractedFeatureIds.Contains(f.TfsId)).ToList();
        var extractedFeatures = allFeatures.Where(f => extractedFeatureIds.Contains(f.TfsId)).ToList();

        // Case A: Features have effort - calculate ratios based on Feature effort
        var totalFeatureEffort = allFeatures.Sum(f => f.Effort ?? 0);
        
        if (totalFeatureEffort > 0)
        {
            var originalFeatureEffort = originalFeatures.Sum(f => f.Effort ?? 0);
            var extractedFeatureEffort = extractedFeatures.Sum(f => f.Effort ?? 0);

            var originalRatio = (double)originalFeatureEffort / totalFeatureEffort;
            var extractedRatio = (double)extractedFeatureEffort / totalFeatureEffort;

            var calculatedOriginal = (int)Math.Ceiling(totalEpicEffort * originalRatio);
            var calculatedExtracted = (int)Math.Ceiling(totalEpicEffort * extractedRatio);

            // Adjust to preserve total (rounding may cause total to exceed)
            if (calculatedOriginal + calculatedExtracted > totalEpicEffort)
            {
                // Reduce the larger one to preserve total
                if (calculatedOriginal >= calculatedExtracted)
                {
                    calculatedOriginal = totalEpicEffort - calculatedExtracted;
                }
                else
                {
                    calculatedExtracted = totalEpicEffort - calculatedOriginal;
                }
            }

            return (calculatedOriginal, calculatedExtracted);
        }
        else
        {
            // Case B: Features have no effort - distribute evenly across Features
            var totalFeatureCount = allFeatures.Count;
            if (totalFeatureCount == 0)
            {
                return (totalEpicEffort, 0);
            }

            var originalCount = originalFeatures.Count;
            var extractedCount = extractedFeatures.Count;

            var effortPerFeature = (double)totalEpicEffort / totalFeatureCount;

            var calculatedOriginal = (int)Math.Ceiling(effortPerFeature * originalCount);
            var calculatedExtracted = (int)Math.Ceiling(effortPerFeature * extractedCount);

            // Adjust to preserve total
            if (calculatedOriginal + calculatedExtracted > totalEpicEffort)
            {
                if (calculatedOriginal >= calculatedExtracted)
                {
                    calculatedOriginal = totalEpicEffort - calculatedExtracted;
                }
                else
                {
                    calculatedExtracted = totalEpicEffort - calculatedOriginal;
                }
            }

            return (calculatedOriginal, calculatedExtracted);
        }
    }
}
