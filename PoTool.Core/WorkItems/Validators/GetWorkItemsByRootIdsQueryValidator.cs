using FluentValidation;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Validator for GetWorkItemsByRootIdsQuery.
/// Ensures root IDs array is valid and non-empty.
/// </summary>
public sealed class GetWorkItemsByRootIdsQueryValidator : AbstractValidator<GetWorkItemsByRootIdsQuery>
{
    public GetWorkItemsByRootIdsQueryValidator()
    {
        RuleFor(x => x.RootIds)
            .NotNull().WithMessage("Root IDs array cannot be null")
            .NotEmpty().WithMessage("At least one root ID must be provided")
            .Must(ids => ids.Length <= 100).WithMessage("Cannot query more than 100 root IDs at once");

        RuleForEach(x => x.RootIds)
            .GreaterThan(0).WithMessage("Each root ID must be greater than 0");
    }
}
