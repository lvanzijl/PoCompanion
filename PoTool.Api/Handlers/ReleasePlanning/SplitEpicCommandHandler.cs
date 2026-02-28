using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;
using PoTool.Shared.WorkItems;
using PoTool.Core.ReleasePlanning.Commands;

using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for SplitEpicCommand.
/// Splits an Epic into two Epics by creating a new Epic in TFS.
/// This is the only TFS write operation allowed from the Release Planning Board.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class SplitEpicCommandHandler : ICommandHandler<SplitEpicCommand, EpicSplitResultDto>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IReleasePlanningRepository _planningRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<SplitEpicCommandHandler> _logger;

    public SplitEpicCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository workItemRepository,
        IReleasePlanningRepository planningRepository,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<SplitEpicCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _workItemRepository = workItemRepository;
        _planningRepository = planningRepository;
        _productRepository = productRepository;
        _mediator = mediator;
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

            // 4. Load work items using product-scoped approach
            IEnumerable<WorkItemDto> allWorkItems;
            var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
            var productsList = allProducts.ToList();

            if (productsList.Count > 0)
            {
                var rootIds = productsList
                    .SelectMany(p => p.BacklogRootWorkItemIds)
                    .ToArray();

                if (rootIds.Length > 0)
                {
                    var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                    allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
                }
                else
                {
                    allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
                }
            }
            else
            {
                allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
            }
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
            _logger.LogInformation(
                "Epic split requested: Original Epic {OriginalId} ({OriginalTitle}) -> Extracted Epic ({ExtractedTitle}). " +
                "Features to move: {FeatureCount}. Effort distribution: {OriginalEffort}/{ExtractedEffort}",
                command.OriginalEpicId,
                originalEpic.Title,
                command.ExtractedEpicTitle,
                command.FeatureIdsForExtractedEpic.Count,
                originalEffort,
                extractedEffort);

            // Create the new Epic in TFS with the same parent Objective
            var createResult = await _tfsClient.CreateWorkItemAsync(new WorkItemCreateRequest
            {
                WorkItemType = "Epic",
                Title = command.ExtractedEpicTitle,
                ParentId = originalEpic.ParentTfsId,
                Effort = extractedEffort,
                AreaPath = originalEpic.AreaPath,
                IterationPath = originalEpic.IterationPath
            }, cancellationToken);

            if (!createResult.Success)
            {
                _logger.LogError("Failed to create extracted Epic in TFS: {Error}", createResult.ErrorMessage);
                return new EpicSplitResultDto
                {
                    Success = false,
                    ErrorMessage = $"Failed to create extracted Epic in TFS: {createResult.ErrorMessage}",
                    OriginalEpicId = command.OriginalEpicId,
                    OriginalEpicNewEffort = originalEffort,
                    ExtractedEpicEffort = extractedEffort
                };
            }

            var extractedEpicId = createResult.WorkItemId!.Value;
            _logger.LogInformation("Created extracted Epic {ExtractedEpicId} in TFS", extractedEpicId);

            // Move Features to the new Epic
            var moveFailures = new List<int>();
            foreach (var featureId in command.FeatureIdsForExtractedEpic)
            {
                var moveResult = await _tfsClient.UpdateWorkItemParentAsync(featureId, extractedEpicId, cancellationToken);
                if (!moveResult)
                {
                    _logger.LogWarning("Failed to move Feature {FeatureId} to extracted Epic {ExtractedEpicId}",
                        featureId, extractedEpicId);
                    moveFailures.Add(featureId);
                }
            }

            // Update effort on the original Epic
            if (originalEffort != originalEpic.Effort)
            {
                var effortUpdateResult = await _tfsClient.UpdateWorkItemEffortAsync(
                    command.OriginalEpicId, originalEffort, cancellationToken);
                if (!effortUpdateResult)
                {
                    _logger.LogWarning("Failed to update effort on original Epic {OriginalEpicId}", command.OriginalEpicId);
                }
            }

            if (moveFailures.Count > 0)
            {
                return new EpicSplitResultDto
                {
                    Success = true, // Partial success
                    ErrorMessage = $"Epic created but {moveFailures.Count} Features failed to move: {string.Join(", ", moveFailures)}",
                    OriginalEpicId = command.OriginalEpicId,
                    ExtractedEpicId = extractedEpicId,
                    OriginalEpicNewEffort = originalEffort,
                    ExtractedEpicEffort = extractedEffort
                };
            }

            return new EpicSplitResultDto
            {
                Success = true,
                OriginalEpicId = command.OriginalEpicId,
                ExtractedEpicId = extractedEpicId,
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
        List<WorkItemDto> allFeatures,
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
