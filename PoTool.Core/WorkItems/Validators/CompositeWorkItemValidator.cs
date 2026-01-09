using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Composite validator that runs multiple validators and aggregates their results.
/// </summary>
public class CompositeWorkItemValidator : IWorkItemValidator
{
    private readonly IEnumerable<IWorkItemValidator> _validators;

    public CompositeWorkItemValidator(IEnumerable<IWorkItemValidator> validators)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    /// <inheritdoc/>
    public Dictionary<int, List<ValidationIssue>> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var result = new Dictionary<int, List<ValidationIssue>>();

        foreach (var validator in _validators)
        {
            var validationResults = validator.ValidateWorkItems(workItems);

            foreach (var kvp in validationResults)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = new List<ValidationIssue>();
                }

                result[kvp.Key].AddRange(kvp.Value);
            }
        }

        return result;
    }
}
