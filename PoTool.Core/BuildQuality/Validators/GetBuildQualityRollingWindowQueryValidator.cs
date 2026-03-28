using FluentValidation;
using PoTool.Core.BuildQuality.Queries;

namespace PoTool.Core.BuildQuality.Validators;

/// <summary>
/// Validates rolling-window BuildQuality requests.
/// </summary>
public sealed class GetBuildQualityRollingWindowQueryValidator : AbstractValidator<GetBuildQualityRollingWindowQuery>
{
    public GetBuildQualityRollingWindowQueryValidator()
    {
        RuleFor(x => x.ProductOwnerId)
            .GreaterThan(0);

        RuleFor(x => x.EffectiveFilter.RangeStartUtc)
            .NotNull()
            .WithMessage("WindowStartUtc is required.");

        RuleFor(x => x.EffectiveFilter.RangeEndUtc)
            .NotNull()
            .WithMessage("WindowEndUtc is required.");

        RuleFor(x => x.EffectiveFilter.RangeEndUtc)
            .GreaterThan(x => x.EffectiveFilter.RangeStartUtc)
            .WithMessage("WindowEndUtc must be greater than WindowStartUtc.");
    }
}
