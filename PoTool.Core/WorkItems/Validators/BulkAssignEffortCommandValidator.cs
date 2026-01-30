using FluentValidation;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validator for BulkAssignEffortCommand.
/// Ensures effort assignments are valid.
/// </summary>
public sealed class BulkAssignEffortCommandValidator : AbstractValidator<BulkAssignEffortCommand>
{
    public BulkAssignEffortCommandValidator()
    {
        RuleFor(x => x.Assignments)
            .NotNull().WithMessage("Assignments list cannot be null")
            .NotEmpty().WithMessage("At least one assignment must be provided")
            .Must(assignments => assignments.Count <= 500).WithMessage("Cannot process more than 500 assignments at once");

        RuleForEach(x => x.Assignments)
            .ChildRules(assignment =>
            {
                assignment.RuleFor(a => a.WorkItemId)
                    .GreaterThan(0).WithMessage("Work item ID must be greater than 0");

                assignment.RuleFor(a => a.EffortValue)
                    .GreaterThan(0).WithMessage("Effort value must be greater than 0")
                    .LessThanOrEqualTo(999).WithMessage("Effort value must not exceed 999");
            });
    }
}
