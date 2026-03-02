using Mediator;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="GetValidationFixSessionQuery"/>.
/// Delegates to <see cref="GetAllWorkItemsWithValidationQuery"/> for data loading, then
/// filters to work items that have at least one issue matching the requested rule ID.
/// Rule title and category label are sourced from <see cref="ValidationRuleDescriptions"/> (single source of truth).
/// </summary>
public sealed class GetValidationFixSessionQueryHandler
    : IQueryHandler<GetValidationFixSessionQuery, ValidationFixSessionDto>
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetValidationFixSessionQueryHandler> _logger;

    public GetValidationFixSessionQueryHandler(
        IMediator mediator,
        ILogger<GetValidationFixSessionQueryHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<ValidationFixSessionDto> Handle(
        GetValidationFixSessionQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetValidationFixSessionQuery for rule {RuleId} / category {CategoryKey} with ProductIds: {ProductIds}",
            query.RuleId, query.CategoryKey,
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null (all products)");

        // Reuse existing validated work-item loading to avoid duplication
        var workItems = await _mediator.Send(
            new GetAllWorkItemsWithValidationQuery(query.ProductIds),
            cancellationToken);

        var ruleTitle = ValidationRuleDescriptions.GetTitle(query.RuleId);
        var categoryLabel = ValidationRuleDescriptions.GetCategoryLabel(query.CategoryKey);

        // Build the fix session item list — single pass per work item: find the first matching
        // issue and use it for both the membership check and the violation message projection.
        var items = workItems
            .Select(wi =>
            {
                if (wi.ValidationIssues is null) return null;
                var match = wi.ValidationIssues.FirstOrDefault(i =>
                    string.Equals(i.RuleId, query.RuleId, StringComparison.OrdinalIgnoreCase));
                return match is null ? null : new ValidationFixItemDto(
                    wi.TfsId,
                    wi.Type,
                    wi.Title,
                    wi.ParentTfsId,
                    wi.State,
                    wi.AreaPath,
                    wi.IterationPath,
                    wi.Effort,
                    wi.Description,
                    match.Message);
            })
            .Where(item => item is not null)
            .OrderBy(item => item!.TfsId)
            .Cast<ValidationFixItemDto>()
            .ToList();

        _logger.LogDebug(
            "Fix session for rule {RuleId}: {Count} items found",
            query.RuleId, items.Count);

        return new ValidationFixSessionDto(query.RuleId, ruleTitle, query.CategoryKey, categoryLabel, items);
    }
}
