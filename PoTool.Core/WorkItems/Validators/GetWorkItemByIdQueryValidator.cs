using FluentValidation;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validator for GetWorkItemByIdQuery.
/// Ensures work item ID is valid.
/// </summary>
public sealed class GetWorkItemByIdQueryValidator : AbstractValidator<GetWorkItemByIdQuery>
{
    public GetWorkItemByIdQueryValidator()
    {
        RuleFor(x => x.TfsId)
            .GreaterThan(0).WithMessage("Work item ID must be greater than 0");
    }
}
