using FluentValidation;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validator for GetFilteredWorkItemsQuery.
/// Ensures filter string is valid.
/// </summary>
public sealed class GetFilteredWorkItemsQueryValidator : AbstractValidator<GetFilteredWorkItemsQuery>
{
    public GetFilteredWorkItemsQueryValidator()
    {
        RuleFor(x => x.Filter)
            .NotEmpty().WithMessage("Filter cannot be empty")
            .MaximumLength(1000).WithMessage("Filter must not exceed 1000 characters");
    }
}
