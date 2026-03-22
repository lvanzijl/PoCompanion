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

        RuleFor(x => x.WindowStartUtc)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("WindowStartUtc must be UTC.");

        RuleFor(x => x.WindowEndUtc)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("WindowEndUtc must be UTC.");

        RuleFor(x => x.WindowEndUtc)
            .GreaterThan(x => x.WindowStartUtc)
            .WithMessage("WindowEndUtc must be greater than WindowStartUtc.");
    }
}
