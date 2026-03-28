using FluentValidation;
using PoTool.Core.BuildQuality.Queries;

namespace PoTool.Core.BuildQuality.Validators;

/// <summary>
/// Validates sprint BuildQuality requests.
/// </summary>
public sealed class GetBuildQualitySprintQueryValidator : AbstractValidator<GetBuildQualitySprintQuery>
{
    public GetBuildQualitySprintQueryValidator()
    {
        RuleFor(x => x.ProductOwnerId)
            .GreaterThan(0);

        RuleFor(x => x.EffectiveFilter.SprintId)
            .NotNull()
            .GreaterThan(0);
    }
}
